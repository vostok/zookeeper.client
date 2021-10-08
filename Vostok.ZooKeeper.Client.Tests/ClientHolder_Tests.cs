using System;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Commons.Testing;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Authentication;
using Vostok.ZooKeeper.Client.Holder;
using Vostok.ZooKeeper.Client.Helpers;
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

            holder.GetConnectedClientObject().ShouldCompleteIn(1.5.Seconds());

            holder.ConnectionState.Should().Be(ConnectionState.Disconnected);
            holder.SessionId.Should().Be(0);
        }

        [Test]
        public void GetConnectedClient_should_be_null_with_empty_connection_string()
        {
            var holder = GetClientHolder((string)null);

            holder.GetConnectedClientObject().ShouldCompleteIn(1.5.Seconds()).Should().BeNull();

            holder.ConnectionState.Should().Be(ConnectionState.Disconnected);
            holder.SessionId.Should().Be(0);
        }
        
        [Test]
        [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
        public void GetConnectedClient_should_disconnect_on_empty_connection_string()
        {
            var connectionString = Ensemble.ConnectionString;
            
            var holder = GetClientHolder(() => connectionString);

            WaitForNewConnectedClient(holder);

            Log.Info("Set empty");
            connectionString = "";

            holder.GetConnectedClientObject().ShouldCompleteIn(DefaultTimeout).Should().Be(null);
            
            WaitForDisconnectedState(holder);
        }
        
        [Test]
        [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
        public void GetConnectedClient_reconnect_after_empty_connection_string_by_itself()
        {
            string connectionString = null;
            
            var holder = GetClientHolder(() => connectionString);

            holder.GetConnectedClientObject().ShouldCompleteIn(1.5.Seconds()).Should().BeNull();

            Log.Info("Set not empty");
            connectionString = Ensemble.ConnectionString;

            WaitForState(holder, ConnectionState.Connected, 15.Seconds());
            WaitForNewConnectedClient(holder);
        }

        [Test]
        public void GetConnectedClient_should_be_reconnectable()
        {
            var holder = GetClientHolder(Ensemble.ConnectionString);
            WaitForNewConnectedClient(holder);
            var sid1 = holder.SessionId;

            Ensemble.Stop();
            WaitForDisconnectedState(holder);

            Ensemble.Start();
            WaitForNewConnectedClient(holder);
            var sid2 = holder.SessionId;

            sid2.Should().Be(sid1);
        }

        [Test]
        public void GetConnectedClient_should_reconnect_to_new_ensemble_after_timeout()
        {
            using (var ensemble1 = ZooKeeperEnsemble.DeployNew(10, 1, Log))
            {
                var connectionString = ensemble1.ConnectionString;
                // ReSharper disable once AccessToModifiedClosure
                var settings = new ZooKeeperClientSettings(() => connectionString) {Timeout = DefaultTimeout};

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

        [TestCase(false)]
        [TestCase(true)]
        [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
        public void GetConnectedClient_should_reconnect_to_new_ensemble_after_connection_string_change(bool useUri)
        {
            using (var ensemble1 = ZooKeeperEnsemble.DeployNew(10, 1, Log))
            {
                var currentEnsemble = ensemble1;
                var settings = useUri
                    ? new ZooKeeperClientSettings(() => currentEnsemble.Topology) {Timeout = DefaultTimeout}
                    : new ZooKeeperClientSettings(() => currentEnsemble.ConnectionString) {Timeout = DefaultTimeout};

                var holder = new ClientHolder(settings, Log);
                var observer = GetObserver(holder);

                WaitForNewConnectedClient(holder);
                var sid1 = holder.SessionId;

                using (var ensemble2 = ZooKeeperEnsemble.DeployNew(11, 1, Log))
                {
                    currentEnsemble = ensemble2;

                    WaitForNewConnectedClient(holder);
                    ensemble1.Stop();
                    WaitForNewConnectedClient(holder);

                    var sid2 = holder.SessionId;
                    sid2.Should().NotBe(sid1);
                    
                    VerifyObserverMessages(observer, ConnectionState.Disconnected, ConnectionState.Connected, ConnectionState.Disconnected, ConnectionState.Connected);
                }
            }
        }

        [Test]
        public async Task GetConnectedClient_should_return_new_after_expired()
        {
            var holder = GetClientHolder(Ensemble.ConnectionString);
            WaitForNewConnectedClient(holder);
            var sid1 = holder.SessionId;
            await KillSession(holder, Ensemble.ConnectionString);
            WaitForNewConnectedClient(holder);
            var sid2 = holder.SessionId;

            sid2.Should().NotBe(sid1);
        }

        [Test]
        public void GetConnectedClient_should_return_new_after_disconnect_and_timeout()
        {
            var holder = GetClientHolder(Ensemble.ConnectionString);
            var observer = GetObserver(holder);

            WaitForNewConnectedClient(holder);

            Ensemble.Stop();
            WaitForDisconnectedState(holder);

            Thread.Sleep(DefaultTimeout + 1.Seconds());
            holder.SessionId.Should().Be(0);

            Ensemble.Start();
            WaitForNewConnectedClient(holder);
            var sid = holder.SessionId;
            holder.SessionId.Should().NotBe(0);

            Thread.Sleep(DefaultTimeout + 1.Seconds());
            holder.SessionId.Should().Be(sid);

            VerifyObserverMessages(
                observer,
                ConnectionState.Disconnected,
                ConnectionState.Connected,
                ConnectionState.Disconnected,
                ConnectionState.Connected
            );
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
        public void OnConnectionStateChanged_should_observe_auth_failed()
        {
            var holder = GetClientHolder(Ensemble.ConnectionString);
            var observer = GetObserver(holder);

            WaitForNewConnectedClient(holder);

            holder.AddAuthenticationInfo(new AuthenticationInfo("bad_scheme", new byte[0]));

            Action assertion = () => { observer.Messages.Should().Contain(m => m.Value == ConnectionState.AuthFailed); };
            assertion.ShouldPassIn(DefaultTimeout, 0.5.Seconds());
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
                Notification.CreateOnNext(ConnectionState.Died),
                Notification.CreateOnCompleted<ConnectionState>());
        }

        [Test]
        public void Dispose_should_disconnect_client()
        {
            var holder = GetClientHolder(Ensemble.ConnectionString);
            WaitForNewConnectedClient(holder);
            holder.Dispose();
            WaitForDiedState(holder);
        }

        [Test]
        public void Dispose_should_not_wait_for_new_clients()
        {
            var holder = GetClientHolder(Ensemble.ConnectionString);
            WaitForNewConnectedClient(holder);
            holder.Dispose();
            var client = holder.GetConnectedClientObject().ShouldCompleteImmediately();
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
            var client = holder.GetConnectedClientObject().ShouldCompleteImmediately();
            client.Should().BeNull();
        }

        [Test]
        public void Dispose_should_be_tolerant_to_null_client()
        {
            var holder = GetClientHolder((string)null, 1.Seconds());
            holder.GetConnectedClientObject().ShouldCompleteIn(1.5.Seconds());
            holder.Dispose();
        }

        [Test]
        public void Should_work_with_uri_provider()
        {
            var settings = new ZooKeeperClientSettings(() => Ensemble.Topology) {Timeout = DefaultTimeout};

            var holder = new ClientHolder(settings, Log);
            WaitForNewConnectedClient(holder);

            holder.ConnectionState.Should().Be(ConnectionState.Connected);
            holder.SessionId.Should().NotBe(0);
        }
    }
}