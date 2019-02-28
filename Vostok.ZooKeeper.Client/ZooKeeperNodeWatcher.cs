using System.Threading.Tasks;
using org.apache.zookeeper;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;

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
            log.Debug($"Recieved node event {@event}");
            return watcher.ProcessEvent(@event.get_Type().FromZooKeeperEventType(), @event.getState().FromZooKeeperState(), @event.getPath());
        }
    }
}