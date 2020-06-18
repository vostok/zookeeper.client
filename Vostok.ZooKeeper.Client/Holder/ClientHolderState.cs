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
        public readonly Lazy<ZooKeeperNetExClient> LazyClient;
        public readonly ConnectionState ConnectionState;
        public readonly ConnectionWatcher ConnectionWatcher;
        public readonly TaskCompletionSource<ClientHolderState> NextState = new TaskCompletionSource<ClientHolderState>(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TimeBudget Suspended;
        public readonly string ConnectionString;
        public readonly TimeSpan TimeUntilNextReset;
        private readonly ZooKeeperClientSettings settings;

        private readonly DateTime stateChanged = DateTime.UtcNow;

        public ClientHolderState(
            Lazy<ZooKeeperNetExClient> client,
            ConnectionWatcher connectionWatcher,
            ConnectionState connectionState,
            TimeBudget suspended,
            string connectionString,
            ZooKeeperClientSettings settings)
        {
            LazyClient = client;
            ConnectionState = connectionState;
            ConnectionString = connectionString;
            this.settings = settings;
            Suspended = suspended;
            ConnectionWatcher = connectionWatcher;

            TimeUntilNextReset = suspended.Remaining + settings.Timeout;
        }

        [CanBeNull]
        public ZooKeeperNetExClient Client
        {
            get
            {
                try
                {
                    if (!Suspended.HasExpired)
                        return null;

                    return LazyClient?.Value;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public bool IsConnected =>
            ConnectionState.IsConnected(settings.CanBeReadOnly);

        public bool NeedToResetClient()
        {
            if (!Suspended.HasExpired)
                return false;

            return Client == null
                   || !ConnectionState.IsConnected(settings.CanBeReadOnly) && DateTime.UtcNow - stateChanged > TimeUntilNextReset
                   || ConnectionString != settings.ConnectionStringProvider();
        }

        public void InitializeClient()
        {
            LazyClient?.Value?.Touch();
        }

        public void Dispose()
        {
            try
            {
                LazyClient?.Value?.closeAsync().Wait();
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public override string ToString() =>
            Suspended.HasExpired
                ? $"{ConnectionState} at {stateChanged.ToLocalTime():s}"
                : $"{ConnectionState} (suspended for {Suspended.Remaining.ToPrettyString()}) at {stateChanged.ToLocalTime():s}";
    }
}