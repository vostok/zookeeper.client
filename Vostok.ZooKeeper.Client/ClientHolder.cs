using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using org.apache.zookeeper;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client
{
    internal class ClientHolder : IDisposable
    {
        private readonly ILog log;
        private readonly ZooKeeperClientSetup setup;
        private ZooKeeperNetExClient client;
        
        public ClientHolder(ILog log, ZooKeeperClientSetup setup)
        {
            this.log = log;
            this.setup = setup;
        }

        public async Task<ZooKeeperNetExClient> GetConnectedClient()
        {
            ResetClientIfNeeded();

            if (ConnectionState == ConnectionState.Connected)
                return client;

            try
            {
                await OnConnectionStateChanged
                    .Where(state => state == ConnectionState.Connected)
                    .Timeout(setup.Timeout)
                    .FirstAsync()
                    .ToTask()
                    .ConfigureAwait(false);
            }
            catch (TimeoutException e)
            {
                log.Warn(e, $"Failed to get connected client in {setup.Timeout}.");
                return null;
            }

            return client;
        }

        private void ResetClientIfNeeded()
        {
            if (client == null)
                ResetClient();
        }

        private void ResetClient()
        {
            // TODO(kungurtsev): dispose old client with watchers
            var connectionWathcher = new ConnectionWatcher(log, ProcessEvent);
            client = new ZooKeeperNetExClient(
                setup.ToZooKeeperConnectionString(),
                setup.ToZooKeeperConnectionTimeout(),
                connectionWathcher);
        }

        private void ProcessEvent(WatchedEvent @event)
        {
            if (@event.get_Type() != Watcher.Event.EventType.None)
                return;

            var oldConnectionState = ConnectionState;
            var newConnectionState = GetNewConnectionState(@event);
            if (newConnectionState != oldConnectionState)
            {
                ConnectionState = newConnectionState;
                OnConnectionStateChanged.OnNext(newConnectionState);
            }
        }

        private ConnectionState GetNewConnectionState(WatchedEvent @event)
        {
            switch (@event.getState())
            {
                case Watcher.Event.KeeperState.SyncConnected:
                    return ConnectionState.Connected;
                case Watcher.Event.KeeperState.ConnectedReadOnly:
                    return ConnectionState.ConnectedReadonly;
                case Watcher.Event.KeeperState.Expired:
                    ResetClient();
                    return ConnectionState.Expired;
                case Watcher.Event.KeeperState.AuthFailed:
                case Watcher.Event.KeeperState.Disconnected:
                default:
                    return ConnectionState.Disconnected;
            }
        }

        public Subject<ConnectionState> OnConnectionStateChanged { get; } = new Subject<ConnectionState>();

        public ConnectionState ConnectionState { get; set; } = ConnectionState.Disconnected;

        public void Dispose()
        {
        }
    }
}