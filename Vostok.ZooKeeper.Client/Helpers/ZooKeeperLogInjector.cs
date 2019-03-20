using System.Diagnostics;
using System.Threading;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Holder;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client.Helpers
{
    internal static class ZooKeeperLogInjector
    {
        private static volatile ClientHolder owner;

        public static bool Register(ClientHolder client, ILog log)
        {
            if (Interlocked.CompareExchange(ref owner, client, null) != null)
                return false;

            InjectLogging(log);
            return true;
        }

        public static void Unregister(ClientHolder client)
            => Interlocked.CompareExchange(ref owner, null, client);

        private static void InjectLogging(ILog log)
        {
            ZooKeeperNetExClient.CustomLogConsumer = new ZooKeeperLogConsumer(log);
            ZooKeeperNetExClient.LogLevel = TraceLevel.Verbose;
            ZooKeeperNetExClient.LogToFile = false;
            ZooKeeperNetExClient.LogToTrace = false;
        }
    }
}