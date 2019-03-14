using System.Diagnostics;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Holder;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client.Helpers
{
    internal static class LoggerHelper
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