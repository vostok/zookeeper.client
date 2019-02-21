using System;
using System.Threading.Tasks;
using org.apache.zookeeper;
using Vostok.Logging.Abstractions;

namespace Vostok.ZooKeeper.Client
{
    internal class ConnectionWatcher : Watcher, IDisposable
    {
        public bool Disposed;
        private readonly ILog log;
        private readonly Action<WatchedEvent, ConnectionWatcher> action;

        public ConnectionWatcher(ILog log, Action<WatchedEvent, ConnectionWatcher> action)
        {
            this.log = log;
            this.action = action;
        }

        public override Task process(WatchedEvent @event)
        {
            if (Disposed)
                return Task.CompletedTask;

            return Task.Run(
                () =>
                {
                    log.Debug($"Recieved event {@event}");
                    action(@event, this);
                });
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}