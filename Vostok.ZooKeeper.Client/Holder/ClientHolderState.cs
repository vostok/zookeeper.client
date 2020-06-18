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
        public readonly ConnectionWatcher ConnectionWatcher;
        public readonly TaskCompletionSource<ClientHolderState> NextState = new TaskCompletionSource<ClientHolderState>(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TimeSpan TimeBeforeReset;
        private readonly Lazy<ZooKeeperNetExClient> lazyClient;
        private readonly string connectionString;
        private readonly ZooKeeperClientSettings settings;
        private readonly DateTime created = DateTime.UtcNow;
        private readonly TimeBudget suspended;

        public ClientHolderState(
            Lazy<ZooKeeperNetExClient> client,
            ConnectionWatcher connectionWatcher,
            ConnectionState connectionState,
            string connectionString,
            ZooKeeperClientSettings settings)
        {
            lazyClient = client;
            ConnectionState = connectionState;
            this.connectionString = connectionString;
            this.settings = settings;
            ConnectionWatcher = connectionWatcher;
            TimeBeforeReset = settings.Timeout;
        }

        public ClientHolderState(
            TimeBudget suspended,
            ZooKeeperClientSettings settings)
        {
            this.suspended = suspended;
            this.settings = settings;

            ConnectionState = ConnectionState.Disconnected;
            TimeBeforeReset = suspended.Remaining;
        }

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

        public bool IsConnected =>
            ConnectionState.IsConnected(settings.CanBeReadOnly);

        public bool IsSuspended =>
            suspended != null;

        public bool NeedToResetClient() =>
            !IsConnected && DateTime.UtcNow - created > TimeBeforeReset
            || connectionString != settings.ConnectionStringProvider();

        [Pure]
        public ClientHolderState WithConnectionState(ConnectionState newConnectionState) =>
            new ClientHolderState(
                lazyClient,
                ConnectionWatcher,
                newConnectionState,
                connectionString,
                settings);

        public void Dispose() =>
            Client.Dispose();

        public override string ToString() =>
            suspended == null
                ? $"{ConnectionState} at {created.ToLocalTime():s}"
                : $"{ConnectionState} (suspended for {suspended.Remaining.ToPrettyString()}) at {created.ToLocalTime():s}";
    }
}