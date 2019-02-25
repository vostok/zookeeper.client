using System;
using System.Diagnostics;
using Vostok.Logging.Abstractions;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client
{
    internal static class Helper
    {
        public const int DataSizeLimit = 1024 * 1023;

        public static Exception DataSizeLimitExceededException(byte[] data) => new ArgumentException($"Data size limit exceeded: {data?.Length} bytes, but only {DataSizeLimit} bytes allowed.");

        public static void InjectLogging(ILog log)
        {
            ZooKeeperNetExClient.CustomLogConsumer = new ZooKeeperLogConsumer(log);
            ZooKeeperNetExClient.LogLevel = TraceLevel.Verbose;
            ZooKeeperNetExClient.LogToFile = false;
            ZooKeeperNetExClient.LogToTrace = false;
        }

        public static bool ValidateDataSize(byte[] data)
        {
            return data == null || data.Length <= DataSizeLimit;
        }
        
        public static string[] SplitPath(string path)
        {
            return path.Trim('/').Split('/');
        }
    }
}