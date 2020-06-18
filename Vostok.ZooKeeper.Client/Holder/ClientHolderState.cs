using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
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
        public readonly string ConnectionString;
        public readonly TimeSpan TimeBeforeReset;
        private readonly ZooKeeperClientSettings settings;
        private readonly DateTime stateChanged = DateTime.UtcNow;

        public ClientHolderState(
            Lazy<ZooKeeperNetExClient> client,
            ConnectionWatcher connectionWatcher,
            ConnectionState connectionState,
            string connectionString,
            ZooKeeperClientSettings settings)
        {
            LazyClient = client;
            ConnectionState = connectionState;
            ConnectionString = connectionString;
            this.settings = settings;
            ConnectionWatcher = connectionWatcher;
            TimeBeforeReset = settings.Timeout;
        }

        [CanBeNull]
        public ZooKeeperNetExClient Client
        {
            get
            {
                try
                {
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

        public bool NeedToResetClient() =>
            Client == null
            || !IsConnected && DateTime.UtcNow - stateChanged > TimeBeforeReset
            || ConnectionString != settings.ConnectionStringProvider();

        public void Dispose() =>
            Client.Dispose();

        public override string ToString() =>
            $"{ConnectionState} at {stateChanged.ToLocalTime():s}";
    }
}