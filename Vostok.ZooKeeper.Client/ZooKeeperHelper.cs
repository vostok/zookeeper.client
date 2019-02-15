using System.Diagnostics;
using Vostok.Logging.Abstractions;

namespace Vostok.ZooKeeper.Client
{
    internal static class ZooKeeperHelper
    {
        public static void InjectLogging(ILog log)
        {
            org.apache.zookeeper.ZooKeeper.CustomLogConsumer = new ZooKeeperLogConsumer(log);
            org.apache.zookeeper.ZooKeeper.LogLevel = TraceLevel.Verbose;
            org.apache.zookeeper.ZooKeeper.LogToFile = false;
            org.apache.zookeeper.ZooKeeper.LogToTrace = false;
        }
    }
}