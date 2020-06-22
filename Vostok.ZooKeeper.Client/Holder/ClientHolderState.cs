using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Commons.Time;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client.Holder
{
    internal class ClientHolderState : IDisposable
    {
        public readonly ConnectionState ConnectionState;
        public readonly IConnectionWatcher ConnectionWatcher;
        public readonly TaskCompletionSource<ClientHolderState> NextState = new TaskCompletionSource<ClientHolderState>(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TimeBudget TimeBeforeReset;
        public readonly bool IsSuspended;
        public readonly bool IsConnected;
        public readonly string ConnectionString;
        private readonly Lazy<ZooKeeperNetExClient> lazyClient;
        private readonly DateTime created = DateTime.UtcNow;

        private ClientHolderState(
            bool isSuspended,
            bool isConnected,
            Lazy<ZooKeeperNetExClient> client,
            IConnectionWatcher connectionWatcher,
            ConnectionState connectionState,
            string connectionString,
            TimeBudget timeBeforeReset)
        {
            IsSuspended = isSuspended;
            IsConnected = isConnected;
            lazyClient = client;
            ConnectionWatcher = connectionWatcher;
            ConnectionState = connectionState;
            ConnectionString = connectionString;
            TimeBeforeReset = timeBeforeReset;
        }

        public static ClientHolderState CreateActive(
            Lazy<ZooKeeperNetExClient> client,
            IConnectionWatcher connectionWatcher,
            ConnectionState connectionState,
            string connectionString,
            ZooKeeperClientSettings settings) =>
            new ClientHolderState(
                false,
                connectionState.IsConnected(settings.CanBeReadOnly),
                client,
                connectionWatcher,
                connectionState,
                connectionString,
                TimeBudget.StartNew(settings.Timeout));

        public static ClientHolderState CreateSuspended(TimeBudget suspendedFor) =>
            new ClientHolderState(
                true,
                false,
                null,
                null,
                ConnectionState.Disconnected,
                null,
                suspendedFor);

        [CanBeNull]
        public ZooKeeperNetExClient Client
        {
            get
            {
                try
                {
                    return lazyClient?.Value;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        [Pure]
        public ClientHolderState WithConnectionState(ConnectionState newConnectionState, ZooKeeperClientSettings settings) =>
            new ClientHolderState(
                IsSuspended,
                newConnectionState.IsConnected(settings.CanBeReadOnly),
                lazyClient,
                ConnectionWatcher,
                newConnectionState,
                ConnectionString,
                TimeBudget.StartNew(settings.Timeout));

        public void Dispose()
        {
            try
            {
                Client?.closeAsync().Wait();
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public override string ToString() =>
            IsSuspended
                ? $"{ConnectionState} (suspended for {TimeBeforeReset.Remaining.ToPrettyString()}) at {created.ToLocalTime():s}"
                : $"{ConnectionState} at {created.ToLocalTime():s}";
    }
}