using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Vostok.Commons.Helpers.Observable;
using Vostok.Commons.Threading;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Helpers;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;
using Waiter = System.Threading.Tasks.TaskCompletionSource<org.apache.zookeeper.ZooKeeper>;

namespace Vostok.ZooKeeper.Client
{
    internal class ClientHolder : IDisposable
    {
        private readonly ILog log;
        private readonly ZooKeeperClientSetup setup;
        private readonly object sync = new object();
        private volatile ZooKeeperNetExClient client;
        private volatile Waiter connectWaiter = new Waiter(TaskCreationOptions.RunContinuationsAsynchronously);
        private volatile ConnectionWatcher connectionWatcher;
        private DateTime lastConnectionStateChanged = DateTime.Now;
        private bool disposed;
        private readonly ConcurrentQueue<ConnectionEvent> events = new ConcurrentQueue<ConnectionEvent>();
        private readonly AsyncManualResetEvent eventSignal = new AsyncManualResetEvent(false);
        private readonly CancellationToken cancellationToken = new CancellationToken();

        public ClientHolder(ILog log, ZooKeeperClientSetup setup)
        {
            this.log = log;
            this.setup = setup;
            LoggerHelper.InjectLogging(log);

            StartProcessingEventsTask();
        }

        public CachingObservable<ConnectionState> OnConnectionStateChanged { get; } = new CachingObservable<ConnectionState>(ConnectionState.Disconnected);

        public ConnectionState ConnectionState { get; private set; } = ConnectionState.Disconnected;

        public long SessionId => GetSessionIdInternal();

        public byte[] SessionPassword => GetSessionPasswordInternal();

        public async Task<ZooKeeperNetExClient> GetConnectedClient()
        {
            Waiter localWaiter;

            lock (sync)
            {
                if (disposed)
                    return null;

                ResetClientIfNeeded();

                if (ConnectionState == ConnectionState.Connected)
                    return client;

                localWaiter = connectWaiter;
            }

            if (!await WaitWithTimeout(localWaiter).ConfigureAwait(false))
                return null;

            lock (sync)
            {
                return ConnectionState == ConnectionState.Connected ? client : null;
            }
        }

        public void InitializeConnection()
        {
            lock (sync)
            {
                ResetClientIfNeeded();
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;
                disposed = true;

                ChangeStateToDisconnectedIfNeeded();
                OnConnectionStateChanged.Complete();

                client.Dispose();
                connectionWatcher?.Dispose();
            }
        }

        private void ChangeStateToDisconnectedIfNeeded()
        {
            if (ConnectionState == ConnectionState.Disconnected)
                return;

            ConnectionState = ConnectionState.Disconnected;
            OnConnectionStateChanged.Next(ConnectionState.Disconnected);
        }

        private async Task<bool> WaitWithTimeout(Waiter localWaiter)
        {
            using (var cts = new CancellationTokenSource())
            {
                var delay = Task.Delay(setup.Timeout, cts.Token);

                var result = await Task.WhenAny(localWaiter.Task, delay).ConfigureAwait(false);
                if (result == delay)
                {
                    return false;
                }

                cts.Cancel();
            }

            return true;
        }

        private void ResetClientIfNeeded()
        {
            lock (sync)
            {
                if (client == null ||
                    ConnectionState != ConnectionState.Connected && DateTime.Now - lastConnectionStateChanged > setup.Timeout)
                    ResetClient();
            }
        }

        private void ResetClient()
        {
            lock (sync)
            {
                log.Debug($"Reseting client. Current state: {ConnectionState}.");

                if (disposed)
                    return;

                if (client != null)
                {
                    client.Dispose();
                    connectionWatcher.Dispose();
                }

                ChangeStateToDisconnectedIfNeeded();

                connectionWatcher = new ConnectionWatcher(log, EnqueueEvent);
                client = new ZooKeeperNetExClient(
                    setup.GetConnectionString(),
                    setup.ToZooKeeperConnectionTimeout(),
                    connectionWatcher);

                lastConnectionStateChanged = DateTime.Now;
            }
        }

        private void EnqueueEvent(ConnectionEvent connectionEvent)
        {
            events.Enqueue(connectionEvent);
            eventSignal.Set();
        }

        private void StartProcessingEventsTask()
        {
            Task.Run(
                async () =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await eventSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
                        eventSignal.Reset();

                        while (!cancellationToken.IsCancellationRequested && events.TryDequeue(out var connectionEvent))
                        {
                            log.Debug($"Processing event {connectionEvent}");
                            try
                            {
                                ProcessEvent(connectionEvent);
                            }
                            catch (Exception e)
                            {
                                // TODO(kungurtsev): what to do?
                                log.Error(e, $"Failed to process event {connectionEvent}");
                            }
                        }
                    }
                }, cancellationToken);
        }

        private void ProcessEvent(ConnectionEvent connectionEvent)
        {
            lock (sync)
            {
                if (disposed || !ReferenceEquals(connectionEvent.EventFrom, connectionWatcher))
                    return;

                var newConnectionState = connectionEvent.NewConnectionState;
                var oldConnectionState = ConnectionState;
                log.Debug($"Changing holder state {oldConnectionState} -> {newConnectionState}.");
                if (newConnectionState == oldConnectionState)
                    return;

                lastConnectionStateChanged = DateTime.Now;
                
                ConnectionState = newConnectionState;
                OnConnectionStateChanged.Next(newConnectionState);

                if (oldConnectionState == ConnectionState.Connected)
                    connectWaiter = new Waiter(TaskCreationOptions.RunContinuationsAsynchronously);
                if (newConnectionState == ConnectionState.Connected)
                    connectWaiter.TrySetResult(client);
                if (newConnectionState == ConnectionState.Expired)
                    ResetClient();
            }
        }

        private long GetSessionIdInternal()
        {
            lock (sync)
            {
                return disposed ? 0 : client.GetSessionId();
            }
        }

        private byte[] GetSessionPasswordInternal()
        {
            lock (sync)
            {
                return disposed ? null : client.GetSessionPassword();
            }
        }
    }
}