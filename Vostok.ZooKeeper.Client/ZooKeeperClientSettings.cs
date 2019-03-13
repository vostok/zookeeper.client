using System;
using JetBrains.Annotations;
using Vostok.Commons.Time;

namespace Vostok.ZooKeeper.Client
{
    /// <summary>
    /// Represents a ZooKeeper client settings.
    /// </summary>
    [PublicAPI]
    public class ZooKeeperClientSettings
    {
        /// <summary>
        /// Creates a new instance of <see cref="ZooKeeperClientSettings"/> using given <paramref name="connectionString"/>
        /// </summary>
        public ZooKeeperClientSettings(string connectionString)
        {
            GetConnectionString = () => connectionString;
        }

        /// <summary>
        /// Creates a new instance of <see cref="ZooKeeperClientSettings"/> using given <paramref name="connectionString"/>
        /// </summary>
        public ZooKeeperClientSettings(Func<string> connectionString)
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
        /// Is allowed to go to read-only mode in case of partitioning.
        /// </summary>
        public bool CanBeReadOnly { get; set; }
    }
}