using System;
using System.Threading.Tasks;
using org.apache.zookeeper;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;

namespace Vostok.ZooKeeper.Client
{
    internal class ConnectionWatcher : Watcher, IDisposable
    {
        public bool Disposed;
        private readonly ILog log;
        private readonly Action<ConnectionState, ConnectionWatcher> action;

        public ConnectionWatcher(ILog log, Action<ConnectionState, ConnectionWatcher> action)
        {
            this.log = log;
            this.action = action;
        }

        public override Task process(WatchedEvent @event)
        {
            if (Disposed)
                return Task.CompletedTask;

            log.Debug($"Recieved event {@event}");

            if (@event.get_Type() != Event.EventType.None)
                return Task.CompletedTask;

            action(@event.getState().FromZooKeeperState(), this);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}