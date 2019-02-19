using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using NUnit.Framework.Internal;
using Vostok.Commons.Testing;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.ZooKeeper.LocalEnsemble;

namespace Vostok.ZooKeeper.Client.Tests
{
    [TestFixture]
    public class ClientHolder_Tests
    {
        private readonly ILog log = new SynchronousConsoleLog();
        
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
        public void GetConnectedClient_should_be_null_when_timeout()
        {
            using (var ensemble = ZooKeeperEnsemble.DeployNew(1, log, false))
            {
                var holder = GetClientHolder(ensemble.ConnectionString, 1.Seconds());
                var client = holder.GetConnectedClient().ShouldCompleteIn(1.5.Seconds());
                client.Should().BeNull();
            }
        }

        private ClientHolder GetClientHolder(string connectionString, TimeSpan? timeout = null)
        {
            var setup = new ZooKeeperClientSetup(connectionString) {Timeout = timeout ?? 15.Seconds()};
            return new ClientHolder(log, setup);
        }
    }
}