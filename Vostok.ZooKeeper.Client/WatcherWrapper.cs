using org.apache.zookeeper;
using Vostok.Commons.Collections;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;

namespace Vostok.ZooKeeper.Client
{
    internal class WatcherWrapper
    {
        private const int DefaultCacheCapacity = 1000;

        private readonly RecyclingBoundedCache<INodeWatcher, Watcher> watcherWrappers;
        private readonly ILog log;

        public WatcherWrapper(ILog log)
        {
            this.log = log;
            watcherWrappers = new RecyclingBoundedCache<INodeWatcher, Watcher>(DefaultCacheCapacity);
        }

        public Watcher Wrap(INodeWatcher watcher)
        {
            return watcher == null ? null : watcherWrappers.Obtain(watcher, w => new ZooKeeperNodeWatcher(w, log));
        }
    }
}