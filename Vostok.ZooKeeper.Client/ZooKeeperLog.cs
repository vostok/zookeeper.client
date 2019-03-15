using System.Diagnostics;
using JetBrains.Annotations;
using Vostok.Commons.Threading;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Holder;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client
{
    /// <summary>
    /// Provides a way to replace inner <see cref="ZooKeeperClient"/> log to custom.
    /// </summary>
    [PublicAPI]
    public static class ZooKeeperLog
    {
        private static readonly AtomicBoolean SetOnce = new AtomicBoolean(false);

        /// <summary>
        /// Inner <see cref="ZooKeeperClient"/> will use given <paramref name="log"/>.
        /// </summary>
        public static void Set(ILog log)
        {
            SetOnce.TrySetTrue();
            InjectLogging(log);
        }

        /// <summary>
        /// Inner <see cref="ZooKeeperClient"/> will use given <paramref name="log"/>, if it was not already set.
        /// </summary>
        public static void SetIfNull(ILog log)
        {
            if (SetOnce.TrySetTrue())
                InjectLogging(log);
        }

        private static void InjectLogging(ILog log)
        {
            ZooKeeperNetExClient.CustomLogConsumer = new ZooKeeperLogConsumer(log);
            ZooKeeperNetExClient.LogLevel = TraceLevel.Verbose;
            ZooKeeperNetExClient.LogToFile = false;
            ZooKeeperNetExClient.LogToTrace = false;
        }
    }
}