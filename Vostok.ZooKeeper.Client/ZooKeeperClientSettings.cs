using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Vostok.Commons.Collections;
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
        /// Creates a new instance of <see cref="ZooKeeperClientSettings"/> using given <paramref name="connectionString"/>.
        /// </summary>
        public ZooKeeperClientSettings([NotNull] string connectionString)
            : this(() => connectionString)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="ZooKeeperClientSettings"/> using given <paramref name="replicas"/>.
        /// </summary>
        public ZooKeeperClientSettings([NotNull] [ItemNotNull] IList<Uri> replicas)
            : this(() => replicas)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="ZooKeeperClientSettings"/> using given <paramref name="replicasProvider"/>.
        /// </summary>
        public ZooKeeperClientSettings([NotNull] Func<IList<Uri>> replicasProvider)
        {
            if (replicasProvider == null)
                throw new ArgumentNullException(nameof(replicasProvider));

            var transform = new CachingTransform<IList<Uri>, string>(BuildConnectionString);

            ConnectionStringProvider = () => transform.Get(replicasProvider());
        }

        /// <summary>
        /// Creates a new instance of <see cref="ZooKeeperClientSettings"/> using given <paramref name="connectionStringProvider"/>.
        /// </summary>
        public ZooKeeperClientSettings([NotNull] Func<string> connectionStringProvider)
        {
            ConnectionStringProvider = connectionStringProvider ?? throw new ArgumentNullException(nameof(connectionStringProvider));
        }

        /// <summary>
        /// A delegate that returns a connection string used to discover ZooKeeper cluster nodes.
        /// </summary>
        [NotNull]
        public Func<string> ConnectionStringProvider { get; }

        /// <summary>
        /// Session and connect timeout.
        /// </summary>
        public TimeSpan Timeout { get; set; } = 10.Seconds();

        /// <summary>
        /// If set to <c>true</c>, client will be able to operate in read-only mode during partitions that isolate the node it's connected to from established quorum.
        /// </summary>
        public bool CanBeReadOnly { get; set; }

        /// <summary>
        /// Gets or sets the minimum level for logs produced by the client.
        /// </summary>
        public LogLevel LoggingLevel { get; set; } = LogLevel.Info;

        /// <summary>
        /// <para>Limit of unique watchers that can be used with <see cref="ZooKeeperClient"/> instance.</para>
        /// <para>After reaching this limit, same watcher can be triggered multiple times, if it was added multiple times on same node.</para>
        /// </summary>
        public int WatchersCacheCapacity { get; set; } = 100_000;

        /// <summary>
        /// <para>Maximum <see cref="Timeout"/> multiplier for delays between attempts of connecting to ZooKeeper cluster.</para>
        /// </summary>
        public int MaximumConnectPeriodMultiplier { get; set; } = 10;

        private static string BuildConnectionString(IList<Uri> uris)
            => uris == null ? null : string.Join(",", uris.Select(u => $"{u.Host}:{u.Port}"));
    }
}