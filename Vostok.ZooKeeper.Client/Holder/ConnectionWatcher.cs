using System;
using System.Threading.Tasks;
using org.apache.zookeeper;

namespace Vostok.ZooKeeper.Client.Holder
{
    internal class ConnectionWatcher : Watcher, IConnectionWatcher
    {
        private readonly Action<ConnectionEvent> action;

        public ConnectionWatcher(Action<ConnectionEvent> action)
        {
            this.action = action;
        }

        public override Task process(WatchedEvent @event)
        {
            if (@event.get_Type() != Event.EventType.None)
                return Task.CompletedTask;

            var connectionEvent = new ConnectionEvent(@event, this);

            action(connectionEvent);

            return Task.CompletedTask;
        }
    }
}