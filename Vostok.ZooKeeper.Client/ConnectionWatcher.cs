using System;
using System.Threading.Tasks;
using org.apache.zookeeper;
using Vostok.Logging.Abstractions;

namespace Vostok.ZooKeeper.Client
{
    internal class ConnectionWatcher : Watcher
    {
        private readonly ILog log;
        private readonly Action<WatchedEvent> action;

        public ConnectionWatcher(ILog log, Action<WatchedEvent> action)
        {
            this.log = log;
            this.action = action;
        }

        public override Task process(WatchedEvent @event)
        {
            log.Info($"Recieved event {@event}");
            action(@event);
            return Task.CompletedTask;
        }
    }
}