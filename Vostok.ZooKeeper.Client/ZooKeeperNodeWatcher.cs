using System.Threading.Tasks;
using org.apache.zookeeper;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Helpers;

namespace Vostok.ZooKeeper.Client
{
    internal class ZooKeeperNodeWatcher : Watcher
    {
        private readonly INodeWatcher watcher;
        private readonly ILog log;

        public ZooKeeperNodeWatcher(INodeWatcher watcher, ILog log)
        {
            this.watcher = watcher;
            this.log = log;
        }

        public override Task process(WatchedEvent @event)
        {
            // Note(kungurtsev): we ignore connection state changed events, because client holder can reset client.
            if (@event.get_Type() == Event.EventType.None)
                return Task.CompletedTask;

            var eventType = @event.get_Type().ToNodeChangedEventType();
            var nodePath = @event.getPath();

            log.Info("Recieved node event of type '{NodeEventType}' on path '{NodePath}'.", eventType, nodePath);

            return watcher.ProcessEvent(eventType, nodePath);
        }
    }
}