using org.apache.zookeeper;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Helpers;

namespace Vostok.ZooKeeper.Client.Holder
{
    internal class ConnectionEvent
    {
        public readonly ConnectionWatcher EventFrom;
        private readonly WatchedEvent Event;

        public ConnectionEvent(WatchedEvent @event, ConnectionWatcher eventFrom)
        {
            Event = @event;
            EventFrom = eventFrom;
        }

        public ConnectionState NewConnectionState => Event.getState().ToConnectionState();

        public override string ToString() => Event.ToString();
    }
}