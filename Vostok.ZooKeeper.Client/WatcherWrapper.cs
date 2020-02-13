using org.apache.zookeeper;
using Vostok.Commons.Collections;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;

namespace Vostok.ZooKeeper.Client
{
    internal class WatcherWrapper
    {
        private readonly RecyclingBoundedCache<INodeWatcher, Watcher> watcherWrappers;
        private readonly ILog log;

        public WatcherWrapper(int cacheCapacity, ILog log)
        {
            this.log = log;
            watcherWrappers = new RecyclingBoundedCache<INodeWatcher, Watcher>(cacheCapacity);
        }

        public Watcher Wrap(INodeWatcher watcher, bool ignoreCache)
        {
            if (watcher == null)
                return null;

            if (ignoreCache)
                return new ZooKeeperNodeWatcher(watcher, log);

            return watcherWrappers.Obtain(watcher, w => new ZooKeeperNodeWatcher(w, log));
        }
    }
}