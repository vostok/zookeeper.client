using System.Threading.Tasks;
using org.apache.zookeeper;
using Vostok.Logging.Abstractions;

namespace Vostok.ZooKeeper.Client
{
    internal class ConnectionWatcher : Watcher
    {
        private readonly ILog log;

        public ConnectionWatcher(ILog log)
        {
            this.log = log;
        }

        public override Task process(WatchedEvent @event)
        {
            log.Info($"Recieved event {@event}");
            return Task.CompletedTask;
        }
    }
}