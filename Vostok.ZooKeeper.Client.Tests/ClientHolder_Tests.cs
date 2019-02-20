using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Commons.Testing;
using Vostok.Commons.Testing.Observable;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.LocalEnsemble;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client.Tests
{
    [TestFixture]
    public class ClientHolder_Tests
    {
        private readonly ILog log = new SynchronousConsoleLog();
        private static readonly TimeSpan DefaultTimeout = 15.Seconds();
        private TestObserver<ConnectionState> observer;

        [SetUp]
        public void SetUp()
        {
            observer = new TestObserver<ConnectionState>();
        }

        [Test]
        public void GetConnectedClient_should_return_connected_client()
        {
            using (var ensemble = ZooKeeperEnsemble.DeployNew(1, log))
            {
                var holder = GetClientHolder(ensemble.ConnectionString);
                WaitForNewConnectedClient(holder);
            }
        }

        [Test]
        public void GetConnectedClient_should_be_null_after_timeout()
        {
            using (var ensemble = ZooKeeperEnsemble.DeployNew(1, log, false))
            {
                var holder = GetClientHolder(ensemble.ConnectionString, 1.Seconds());
                var client = holder.GetConnectedClient().ShouldCompleteIn(1.5.Seconds());
                client.Should().BeNull();
            }
        }

        [Test]
        public void GetConnectedClient_should_be_reconnectable()
        {
            using (var ensemble = ZooKeeperEnsemble.DeployNew(1, log))
            {
                var holder = GetClientHolder(ensemble.ConnectionString);
                WaitForNewConnectedClient(holder);

                ensemble.Stop();
                WaitForNewDisconectedClient(holder);

                ensemble.Start();
                WaitForNewConnectedClient(holder);
            }
        }

        [Test]
        public void OnConnectionStateChanged_should_observe_connected()
        {
            using (var ensemble = ZooKeeperEnsemble.DeployNew(1, log))
            {
                var holder = GetClientHolder(ensemble.ConnectionString);
                holder.OnConnectionStateChanged.Subscribe(observer);
                holder.InitializeConnection();
                VerifyObserverMessages(ConnectionState.Connected);
            }
        }

        [Test]
        public async Task OnConnectionStateChanged_should_observe_disconnected()
        {
            using (var ensemble = ZooKeeperEnsemble.DeployNew(1, log))
            {
                var holder = GetClientHolder(ensemble.ConnectionString);
                holder.OnConnectionStateChanged.Subscribe(observer);
                var client = await holder.GetConnectedClient();
                client.Should().NotBe(null);
                ensemble.Stop();
                VerifyObserverMessages(ConnectionState.Connected, ConnectionState.Disconnected);
            }
        }

        private static ZooKeeperNetExClient WaitForNewConnectedClient(ClientHolder holder)
        {
            var client = holder.GetConnectedClient().Result;
            client.getState().Should().Be(ZooKeeperNetExClient.States.CONNECTED);
            return client;
        }

        private static void WaitForNewDisconectedClient(ClientHolder holder)
        {
            Action assertion = () =>
            {
                try
                {
                    var client = holder.GetConnectedClient().Result;
                    client.Should().BeNull();
                }
                catch (ObjectDisposedException e)
                {
                    throw new AssertionException("Disposed", e);
                }
            };
            assertion.ShouldPassIn(5.Seconds());
        }

        private void VerifyObserverMessages(params ConnectionState[] states)
        {
            Action assertion = () =>
            {
                observer.Values.Should().BeEquivalentTo(states, options => options.WithStrictOrdering());
            };
            assertion.ShouldPassIn(DefaultTimeout, 0.5.Seconds());
        }

        private ClientHolder GetClientHolder(string connectionString, TimeSpan? timeout = null)
        {
            var setup = new ZooKeeperClientSetup(connectionString) {Timeout = timeout ?? DefaultTimeout};
            return new ClientHolder(log, setup);
        }
    }
}