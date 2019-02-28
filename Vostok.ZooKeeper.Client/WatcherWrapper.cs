using System.Collections.Concurrent;
using System.Threading.Tasks;
using org.apache.zookeeper;
using Vostok.ZooKeeper.Client.Abstractions.Model;

namespace Vostok.ZooKeeper.Client
{
    internal class WatcherWrapper
    {
	    private readonly ConcurrentDictionary<INodeWatcher, Watcher> watcherWrappers = new ConcurrentDictionary<INodeWatcher, Watcher>();

        public Watcher Wrap(INodeWatcher watcher)
        {
            return watcher == null ? null : watcherWrappers.GetOrAdd(watcher, w => w.ToZooKeeperWatcher());
        }
    }
}