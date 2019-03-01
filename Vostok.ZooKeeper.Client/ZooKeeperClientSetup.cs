using System;
using JetBrains.Annotations;
using Vostok.Commons.Time;

namespace Vostok.ZooKeeper.Client
{
    /// <summary>
    /// Represents a ZooKeeper client setup.
    /// </summary>
    [PublicAPI]
    public class ZooKeeperClientSetup
    {
        /// <summary>
        /// Creates a new instance of <see cref="ZooKeeperClientSetup"/> using given <paramref name="connectionString"/>
        /// </summary>
        public ZooKeeperClientSetup(string connectionString)
        {
            GetConnectionString = () => connectionString;
        }

        /// <summary>
        /// Creates a new instance of <see cref="ZooKeeperClientSetup"/> using given <paramref name="connectionString"/>
        /// </summary>
        public ZooKeeperClientSetup(Func<string> connectionString)
        {
            GetConnectionString = connectionString;
        }

        /// <summary>
        /// Delegate for producing connection string.
        /// </summary>
        public Func<string> GetConnectionString { get; set; }

        /// <summary>
        /// Session and connect timeout.
        /// </summary>
        public TimeSpan Timeout { get; set; } = 5.Seconds();

        /// <summary>
        /// Namespace for node pathes (used as path prefix for applications isolation).
        /// </summary>
        public string Namespace { get; set; }
    }
}