using System;
using JetBrains.Annotations;
using Vostok.Commons.Time;

namespace Vostok.ZooKeeper.Client
{
    [PublicAPI]
    public class ZooKeeperClientSetup
    {
        public ZooKeeperClientSetup(string connectionString)
        {
            GetConnectionString = () => connectionString;
        }

        public ZooKeeperClientSetup(Func<string> connectionString)
        {
            GetConnectionString = connectionString;
        }

        public Func<string> GetConnectionString { get; set; }

        public TimeSpan Timeout { get; set; } = 5.Seconds();

        public string Namespace { get; set; }
    }
}