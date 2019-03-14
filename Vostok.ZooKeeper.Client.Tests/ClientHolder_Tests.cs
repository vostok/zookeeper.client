using System;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Commons.Testing;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Holder;
using Vostok.ZooKeeper.LocalEnsemble;

namespace Vostok.ZooKeeper.Client.Tests
{
    [TestFixture]
    internal class ClientHolder_Tests : TestsBase
    {
        [Test]
        public void GetConnectedClient_should_return_connected_client()
        {
            var holder = GetClientHolder(Ensemble.ConnectionString);
            WaitForNewConnectedClient(holder);

            holder.ConnectionState.Should().Be(ConnectionState.Connected);
            holder.SessionId.Should().NotBe(0);
        }

        [Test]
        public void GetConnectedClient_should_be_null_after_timeout()
        {
            Ensemble.Stop();

            var holder = GetClientHolder(Ensemble.ConnectionString, 1.Seconds());
            var client = holder.GetConnectedClient().ShouldCompleteIn(1.5.Seconds());
            client.Should().BeNull();

            holder.ConnectionState.Should().Be(ConnectionState.Disconnected);
            holder.SessionId.Should().Be(0);
        }

        [Test]
        public void GetConnectedClient_should_be_reconnectable()
        {
            var holder = GetClientHolder(Ensemble.ConnectionString);
            var client1 = WaitForNewConnectedClient(holder);

            Ensemble.Stop();
            WaitForDisconnectedState(holder);

            Ensemble.Start();
            var client2 = WaitForNewConnectedClient(holder);

            client2.Should().BeSameAs(client1);
        }

        [Test]
        public void GetConnectedClient_should_reconnect_to_new_ensemble_after_timeout()
        {
            using (var ensemble1 = ZooKeeperEnsemble.DeployNew(10, 1, Log))
            {
                var connectionString = ensemble1.ConnectionString;
                // ReSharper disable once AccessToModifiedClosure
                var settings = new ZooKeeperClientSettings(() => connectionString, Log) {Timeout = DefaultTimeout};

                var holder = new ClientHolder(settings, Log);
                WaitForNewConnectedClient(holder);

                ensemble1.Dispose();
                WaitForDisconnectedState(holder);

                using (var ensemble2 = ZooKeeperEnsemble.DeployNew(11, 1, Log))
                {
                    ensemble2.ConnectionString.Should().NotBe(connectionString);
                    connectionString = ensemble2.ConnectionString;

                    Thread.Sleep(DefaultTimeout);

                    WaitForNewConnectedClient(holder);
                }
            }
        }

        [Test]
        public async Task GetConnectedClient_should_return_new_after_expired()
        {
            var holder = GetClientHolder(Ensemble.ConnectionString);
            var client1 = WaitForNewConnectedClient(holder);
            await KillSession(holder, Ensemble.ConnectionString);
            var client2 = WaitForNewConnectedClient(holder);

            client2.Should().NotBe(client1);
        }

        [Test]
        public void OnConnectionStateChanged_should_observe_disconnected_as_initial_state()
        {
            Ensemble.Stop();

            var holder = GetClientHolder(Ensemble.ConnectionString, 1.Seconds());
            var observer = GetObserver(holder);

            VerifyObserverMessages(observer, ConnectionState.Disconnected);
        }

        [Test]
        public void OnConnectionStateChanged_should_observe_connected()
        {
            var holder = GetClientHolder(Ensemble.ConnectionString);
            var observer = GetObserver(holder);
            WaitForNewConnectedClient(holder);
            VerifyObserverMessages(observer, ConnectionState.Disconnected, ConnectionState.Connected);
        }

        [Test]
        public void OnConnectionStateChanged_should_observe_last_event()
        {
            var holder = GetClientHolder(Ensemble.ConnectionString);
            WaitForNewConnectedClient(holder);

            var observer = GetObserver(holder);
            VerifyObserverMessages(observer, ConnectionState.Connected);
        }

        [Test]
        public void OnConnectionStateChanged_should_observe_disconnected()
        {
            var holder = GetClientHolder(Ensemble.ConnectionString);
            var observer = GetObserver(holder);
            WaitForNewConnectedClient(holder);
            Ensemble.Stop();
            VerifyObserverMessages(observer, ConnectionState.Disconnected, ConnectionState.Connected, ConnectionState.Disconnected);
        }

        [Test]
        public async Task OnConnectionStateChanged_should_observe_expired()
        {
            var holder = GetClientHolder(Ensemble.ConnectionString);
            var observer = GetObserver(holder);

            WaitForNewConnectedClient(holder);

            await KillSession(holder, Ensemble.ConnectionString);

            VerifyObserverMessages(observer, ConnectionState.Disconnected, ConnectionState.Connected, ConnectionState.Disconnected, ConnectionState.Expired, ConnectionState.Disconnected, ConnectionState.Connected);
        }

        [Test]
        public void OnConnectionStateChanged_should_observe_reconnected()
        {
            var holder = GetClientHolder(Ensemble.ConnectionString);
            var observer = GetObserver(holder);

            WaitForNewConnectedClient(holder);

            Ensemble.Stop();
            Ensemble.Start();

            VerifyObserverMessages(observer, ConnectionState.Disconnected, ConnectionState.Connected, ConnectionState.Disconnected, ConnectionState.Connected);
        }

        [Test]
        public void OnConnectionStateChanged_should_complete_on_dispose()
        {
            var holder = GetClientHolder(Ensemble.ConnectionString);
            var observer = GetObserver(holder);
            WaitForNewConnectedClient(holder);
            holder.Dispose();
            VerifyObserverMessages(
                observer,
                Notification.CreateOnNext(ConnectionState.Disconnected),
                Notification.CreateOnNext(ConnectionState.Connected),
                Notification.CreateOnNext(ConnectionState.Disconnected),
                Notification.CreateOnCompleted<ConnectionState>());
        }

        [Test]
        public void Dispose_should_disconnect_client()
        {
            var holder = GetClientHolder(Ensemble.ConnectionString);
            WaitForNewConnectedClient(holder);
            holder.Dispose();
            WaitForDisconnectedState(holder);
        }

        [Test]
        public void Dispose_should_not_wait_for_new_clients()
        {
            var holder = GetClientHolder(Ensemble.ConnectionString);
            WaitForNewConnectedClient(holder);
            holder.Dispose();
            var client = holder.GetConnectedClient().ShouldCompleteImmediately();
            client.Should().BeNull();
        }

        [Test]
        public void Dispose_should_be_tolerant_to_multiple_calls()
        {
            var holder = GetClientHolder(Ensemble.ConnectionString);
            WaitForNewConnectedClient(holder);
            holder.Dispose();
            holder.Dispose();
            holder.Dispose();
            var client = holder.GetConnectedClient().ShouldCompleteImmediately();
            client.Should().BeNull();
        }

        [Test]
        public void Should_work_with_uri()
        {
            var uri = new Uri("http://localhost:" + Ensemble.Instances[0].ClientPort);
            var settings = new ZooKeeperClientSettings(new[] {uri}, Log) { Timeout = DefaultTimeout };

            var holder = new ClientHolder(settings, Log);
            WaitForNewConnectedClient(holder);

            holder.ConnectionState.Should().Be(ConnectionState.Connected);
            holder.SessionId.Should().NotBe(0);
        }
    }
}