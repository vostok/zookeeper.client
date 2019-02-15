using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using NUnit.Framework.Internal;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.ZooKeeper.LocalEnsemble;

namespace Vostok.ZooKeeper.Client.Tests
{
    [TestFixture]
    public class ClientHolder_Tests
    {
        private readonly ILog log = new ConsoleLog();

        [Test]
        public async Task GetConnectedClient_should_return_connected_client()
        {
            using (var ensemble = ZooKeeperEnsemble.DeployNew(1, log))
            {
                var holder = GetClientHolder(ensemble.ConnectionString);
                var client = await holder.GetConnectedClient();
                client.getState().Should().Be(org.apache.zookeeper.ZooKeeper.States.CONNECTED);
            }
        }

        [Test]
        public async Task GetConnectedClient_should_be_null_when_timeout()
        {
            using (var ensemble = ZooKeeperEnsemble.DeployNew(1, log, startInstances:false))
            {
                var holder = GetClientHolder(ensemble.ConnectionString, 1.Seconds());
                var client = await holder.GetConnectedClient();
                client.Should().BeNull();
            }
        }

        private ClientHolder GetClientHolder(string connectionString, TimeSpan? timeout = null)
        {
            var setup = new ZooKeeperClientSetup(connectionString);
            if (timeout.HasValue)
                setup.Timeout = timeout.Value;
            return new ClientHolder(log, setup);
        }
    }
}