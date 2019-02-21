using System.Diagnostics;
using Vostok.Logging.Abstractions;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client
{
    internal static class ZooKeeperHelper
    {
        public static void InjectLogging(ILog log)
        {
            ZooKeeperNetExClient.CustomLogConsumer = new ZooKeeperLogConsumer(log);
            ZooKeeperNetExClient.LogLevel = TraceLevel.Verbose;
            ZooKeeperNetExClient.LogToFile = false;
            ZooKeeperNetExClient.LogToTrace = false;
        }
    }
}