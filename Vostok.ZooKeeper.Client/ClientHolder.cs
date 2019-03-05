using System;
using System.Threading;
using System.Threading.Tasks;
using Vostok.Commons.Helpers.Observable;
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

        public ClientHolder(ILog log, ZooKeeperClientSetup setup)
        {
            this.log = log;
            this.setup = setup;
            LoggerHelper.InjectLogging(log);
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

                client.Dispose();
                connectionWatcher?.Dispose();
            }
        }

        private void ChangeStateToDisconnectedIfNeeded()
        {
            if (ConnectionState == ConnectionState.Disconnected)
                return;

            OnConnectionStateChanged.Next(ConnectionState.Disconnected);
            OnConnectionStateChanged.Complete();
            ConnectionState = ConnectionState.Disconnected;
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

                connectionWatcher = new ConnectionWatcher(log, ProcessEvent);
                client = new ZooKeeperNetExClient(
                    setup.GetConnectionString(),
                    setup.ToZooKeeperConnectionTimeout(),
                    connectionWatcher);

                lastConnectionStateChanged = DateTime.Now;
            }
        }

        private void ProcessEvent(ConnectionState newConnectionState, ConnectionWatcher eventFrom)
        {
            lock (sync)
            {
                if (disposed || eventFrom.Disposed)
                    return;

                var oldConnectionState = ConnectionState;
                log.Debug($"Changing holder state {oldConnectionState} -> {newConnectionState}.");
                if (newConnectionState == oldConnectionState)
                    return;

                if (oldConnectionState == ConnectionState.Connected)
                    connectWaiter = new Waiter(TaskCreationOptions.RunContinuationsAsynchronously);
                if (newConnectionState == ConnectionState.Connected)
                    connectWaiter.TrySetResult(client);
                if (newConnectionState == ConnectionState.Expired)
                    ResetClient();

                ConnectionState = newConnectionState;
                lastConnectionStateChanged = DateTime.Now;
                OnConnectionStateChanged.Next(newConnectionState);
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