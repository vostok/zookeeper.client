using System;
using System.Threading;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Holder;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client.Helpers
{
    // Note(kungurtsev): needed for test ILRepacked assembly without rename internalized.
    internal static class TestHelpers
    {
        public static ClientHolderState CreateActiveClientHolderState(ZooKeeperClientSettings settings)
        {
            var connectionString = settings.ConnectionStringProvider();
            var connectionWatcher = new ConnectionWatcher(_ => {});

            var client = new Lazy<ZooKeeperNetExClient>(
                () =>
                {
                    using (ExecutionContext.SuppressFlow())
                    {
                        return new ZooKeeperNetExClient(
                            connectionString,
                            settings.ToInnerConnectionTimeout(),
                            connectionWatcher,
                            settings.CanBeReadOnly);
                    }
                },
                LazyThreadSafetyMode.ExecutionAndPublication);

            return ClientHolderState.CreateActive(client, connectionWatcher, ConnectionState.Connected, connectionString, settings);
        }
    }
}