using System;
using System.Threading;
using System.Threading.Tasks;
using org.apache.zookeeper;
using Vostok.Commons.Helpers.Observable;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
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
            ZooKeeperHelper.InjectLogging(log);
        }

        public CachingObservable<ConnectionState> OnConnectionStateChanged { get; } = new CachingObservable<ConnectionState>();

        public ConnectionState ConnectionState { get; private set; } = ConnectionState.Disconnected;

        public long SessionId => GetSessionIdInternal();

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

                if (ConnectionState == ConnectionState.Connected)
                {
                    OnConnectionStateChanged.Next(ConnectionState.Disconnected);
                    ConnectionState = ConnectionState.Disconnected;
                }

                client.Dispose();
                connectionWatcher?.Dispose();
            }
        }

        private static ConnectionState GetNewConnectionState(WatchedEvent @event)
        {
            switch (@event.getState())
            {
                case Watcher.Event.KeeperState.SyncConnected:
                    return ConnectionState.Connected;
                case Watcher.Event.KeeperState.ConnectedReadOnly:
                    return ConnectionState.ConnectedReadonly;
                case Watcher.Event.KeeperState.Expired:
                    return ConnectionState.Expired;
                case Watcher.Event.KeeperState.AuthFailed:
                case Watcher.Event.KeeperState.Disconnected:
                default:
                    return ConnectionState.Disconnected;
            }
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

                connectionWatcher = new ConnectionWatcher(log, ProcessEvent);
                client = new ZooKeeperNetExClient(
                    setup.ToZooKeeperConnectionString(),
                    setup.ToZooKeeperConnectionTimeout(),
                    connectionWatcher);

                lastConnectionStateChanged = DateTime.Now;
            }
        }

        private void ProcessEvent(WatchedEvent @event, ConnectionWatcher eventFrom)
        {
            lock (sync)
            {
                if (disposed || eventFrom.Disposed)
                    return;

                if (@event.get_Type() != Watcher.Event.EventType.None)
                    return;

                var oldConnectionState = ConnectionState;
                var newConnectionState = GetNewConnectionState(@event);
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
    }
}