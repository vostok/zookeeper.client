using System;
using System.Threading.Tasks;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client.Holder
{
    internal class ClientHolderState : IDisposable
    {
        public readonly Lazy<ZooKeeperNetExClient> LazyClient;
        public readonly ConnectionState ConnectionState;
        public readonly DateTime StateChanged = DateTime.UtcNow;
        public readonly ConnectionWatcher ConnectionWatcher;
        public readonly TaskCompletionSource<ClientHolderState> NextState = new TaskCompletionSource<ClientHolderState>();
        public readonly string ConnectionString;

        public ClientHolderState(Lazy<ZooKeeperNetExClient> client, ConnectionWatcher connectionWatcher, ConnectionState connectionState, string connectionString)
        {
            LazyClient = client;
            ConnectionState = connectionState;
            ConnectionString = connectionString;
            ConnectionWatcher = connectionWatcher;
        }

        public ZooKeeperNetExClient Client => LazyClient?.Value;

        public void Dispose()
        {
            Client.Dispose();
        }

        public override string ToString() =>
            $"{ConnectionState} at {StateChanged}";
    }
}