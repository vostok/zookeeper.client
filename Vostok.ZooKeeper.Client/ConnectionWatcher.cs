using System;
using System.Threading.Tasks;
using org.apache.zookeeper;
using Vostok.Logging.Abstractions;

namespace Vostok.ZooKeeper.Client
{
    internal class ConnectionWatcher : Watcher, IDisposable
    {
        private bool disposed;
        private readonly ILog log;
        private readonly Action<ConnectionEvent> action;

        public ConnectionWatcher(ILog log, Action<ConnectionEvent> action)
        {
            this.log = log;
            this.action = action;
        }

        public override Task process(WatchedEvent @event)
        {
            if (disposed)
                return Task.CompletedTask;

            log.Debug($"Recieved event {@event}");

            if (@event.get_Type() != Event.EventType.None)
                return Task.CompletedTask;

            var connectionEvent = new ConnectionEvent(@event, this);
            action(connectionEvent);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            disposed = true;
        }
    }
}