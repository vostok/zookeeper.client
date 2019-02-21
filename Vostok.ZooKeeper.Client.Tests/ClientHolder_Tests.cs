using System;
using System.Threading;
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
        private static readonly TimeSpan DefaultTimeout = 10.Seconds();
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
                var client1 = WaitForNewConnectedClient(holder);

                ensemble.Stop();
                WaitForDisconectedState(holder);

                ensemble.Start();
                var client2 = WaitForNewConnectedClient(holder);
                client2.Should().Be(client1);
            }
        }

        [Test]
        public void GetConnectedClient_should_reconect_to_new_enseble_after_timeout()
        {
            using (var ensemble = ZooKeeperEnsemble.DeployNew(1, log))
            {
                var connectionString = ensemble.ConnectionString;
                var setup = new ZooKeeperClientSetup(() => connectionString) { Timeout = DefaultTimeout };

                var holder = new ClientHolder(log, setup);
                WaitForNewConnectedClient(holder);

                ensemble.Dispose();
                WaitForDisconectedState(holder);

                using (var ensemble2 = ZooKeeperEnsemble.DeployNew(1, log))
                {
                    ensemble2.ConnectionString.Should().NotBe(connectionString);
                    connectionString = ensemble2.ConnectionString;

                    Thread.Sleep(DefaultTimeout);

                    WaitForNewConnectedClient(holder);
                }
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
        public void OnConnectionStateChanged_should_observe_last_event()
        {
            using (var ensemble = ZooKeeperEnsemble.DeployNew(1, log))
            {
                var holder = GetClientHolder(ensemble.ConnectionString);
                WaitForNewConnectedClient(holder);

                holder.OnConnectionStateChanged.Subscribe(observer);
                VerifyObserverMessages(ConnectionState.Connected);
            }
        }

        [Test]
        public void OnConnectionStateChanged_should_observe_disconnected()
        {
            using (var ensemble = ZooKeeperEnsemble.DeployNew(1, log))
            {
                var holder = GetClientHolder(ensemble.ConnectionString);
                holder.OnConnectionStateChanged.Subscribe(observer);
                WaitForNewConnectedClient(holder);
                ensemble.Stop();
                VerifyObserverMessages(ConnectionState.Connected, ConnectionState.Disconnected);
            }
        }

        [Test]
        public void Dispose_should_disconect_client()
        {
            using (var ensemble = ZooKeeperEnsemble.DeployNew(1, log))
            {
                var holder = GetClientHolder(ensemble.ConnectionString);
                holder.OnConnectionStateChanged.Subscribe(observer);
                WaitForNewConnectedClient(holder);
                holder.Dispose();
                WaitForDisconectedState(holder);
                VerifyObserverMessages(ConnectionState.Connected, ConnectionState.Disconnected);
            }
        }

        [Test]
        public void Dispose_should_not_wait_for_new_clients()
        {
            using (var ensemble = ZooKeeperEnsemble.DeployNew(1, log, false))
            {
                var holder = GetClientHolder(ensemble.ConnectionString);
                holder.Dispose();
                var client = holder.GetConnectedClient().ShouldCompleteIn(0.5.Seconds());
                client.Should().BeNull();
            }
        }

        private static ZooKeeperNetExClient WaitForNewConnectedClient(ClientHolder holder)
        {
            var client = holder.GetConnectedClient().Result;
            client.getState().Should().Be(ZooKeeperNetExClient.States.CONNECTED);
            holder.ConnectionState.Should().Be(ConnectionState.Connected);
            return client;
        }
        
        private static void WaitForDisconectedState(ClientHolder holder)
        {
            Action assertion = () =>
            {
                holder.ConnectionState.Should().Be(ConnectionState.Disconnected);
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