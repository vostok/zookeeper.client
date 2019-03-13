using System;
using JetBrains.Annotations;
using Vostok.Commons.Time;
using Vostok.Logging.Abstractions;

namespace Vostok.ZooKeeper.Client
{
    /// <summary>
    /// Represents a ZooKeeper client settings.
    /// </summary>
    [PublicAPI]
    public class ZooKeeperClientSettings
    {
        /// <summary>
        /// Creates a new instance of <see cref="ZooKeeperClientSettings"/> using given <paramref name="connectionString"/> and <paramref name="log"/>.
        /// </summary>
        public ZooKeeperClientSettings(string connectionString, ILog log)
        {
            Log = log;
            GetConnectionString = () => connectionString;
        }

        /// <summary>
        /// Creates a new instance of <see cref="ZooKeeperClientSettings"/> using given <paramref name="connectionString"/> and <paramref name="log"/>.
        /// </summary>
        public ZooKeeperClientSettings(Func<string> connectionString, ILog log)
        {
            GetConnectionString = connectionString;
        }

        /// <summary>
        /// Client logger.
        /// </summary>
        public ILog Log { get; }

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