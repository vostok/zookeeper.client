using System;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client
{
    internal class ClientHolder : IDisposable
    {
        private readonly ILog log;
        private readonly ZooKeeperClientSetup setup;
        private ZooKeeperNetExClient client;

        public ClientHolder(ILog log, ZooKeeperClientSetup setup)
        {
            this.log = log;
            this.setup = setup;
        }

        public ZooKeeperNetExClient GetConnectedClient()
        {
            ResetClientIfNeeded();

            // TODO(kungurtsev): connect
            return client;
        }

        private void ResetClientIfNeeded()
        {
            if (client == null)
                ResetClient();
        }

        private void ResetClient()
        {
            // TODO(kungurtsev): dispose old client with watchers
            client = new ZooKeeperNetExClient(setup.ToZooKeeperConnectionString(), setup.ToZooKeeperConnectionTimeout(), new ConnectionWatcher(log));
        }

        public IObservable<ConnectionState> OnConnectionStateChanged { get; }
        
        public void Dispose()
        {
        }
    }
}