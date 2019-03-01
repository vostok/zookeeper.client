using System.Collections.Concurrent;
using org.apache.zookeeper;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;

namespace Vostok.ZooKeeper.Client
{
    internal class WatcherWrapper
    {
        private readonly ConcurrentDictionary<INodeWatcher, Watcher> watcherWrappers = new ConcurrentDictionary<INodeWatcher, Watcher>();
        private readonly ILog log;

        public WatcherWrapper(ILog log)
        {
            this.log = log;
        }

        public Watcher Wrap(INodeWatcher watcher)
        {
            return watcher == null ? null : watcherWrappers.GetOrAdd(watcher, w => new ZooKeeperNodeWatcher(w, log));
        }
    }
}