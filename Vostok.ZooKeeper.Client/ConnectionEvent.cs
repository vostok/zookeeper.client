using org.apache.zookeeper;
using Vostok.ZooKeeper.Client.Abstractions.Model;

namespace Vostok.ZooKeeper.Client
{
    internal class ConnectionEvent
    {
        public ConnectionState NewConnectionState => Event.getState().FromZooKeeperState();
        public readonly ConnectionWatcher EventFrom;
        private readonly WatchedEvent Event;

        public ConnectionEvent(WatchedEvent @event, ConnectionWatcher eventFrom)
        {
            Event = @event;
            EventFrom = eventFrom;
        }

        public override string ToString() => Event.ToString();
    }
}