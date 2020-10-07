using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Commons.Testing;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Authentication;
using Vostok.ZooKeeper.Client.Helpers;
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

            holder.GetConnectedClient().ShouldCompleteIn(1.5.Seconds());

            holder.ConnectionState.Should().Be(ConnectionState.Disconnected);
            holder.SessionId.Should().Be(0);
        }

        [Test]
        public void GetConnectedClient_should_be_null_with_empty_connection_string()
        {
            var holder = GetClientHolder(null, 1.Seconds());

            holder.GetConnectedClient().ShouldCompleteIn(1.5.Seconds());

            holder.ConnectionState.Should().Be(ConnectionState.Disconnected);
            holder.SessionId.Should().Be(0);
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
                }

                VerifyObserverMessages(observer, ConnectionState.Disconnected, ConnectionState.Connected, ConnectionState.Disconnected, ConnectionState.Connected);
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
        public void GetConnectedClient_should_reconnect_to_new_ensemble_with_provided_auth_info()
        {
            using (var ensemble1 = ZooKeeperEnsemble.DeployNew(10, 1, Log))
            {
                var connectionString = ensemble1.ConnectionString;
                // ReSharper disable once AccessToModifiedClosure
                var settings = new ZooKeeperClientSettings(() => connectionString) {Timeout = DefaultTimeout};

                var holder = new ClientHolder(settings, Log);
                WaitForNewConnectedClient(holder);

                var login = "login";
                var password = "password";
                var path = "/path";

                var authInfo = AuthenticationInfo.Digest(login, password);
                holder.AddAuthenticationInfo(authInfo);
                var client = holder.GetConnectedClient().Result;

                var acls = new List<Acl> {Acl.Digest(Permissions.All, login, password)}.ToInnerAcls();
                client.createAsync(path, new byte[0], acls, org.apache.zookeeper.CreateMode.PERSISTENT).GetAwaiter().GetResult();

                ensemble1.Dispose();
                WaitForDisconnectedState(holder);

                using (var ensemble2 = ZooKeeperEnsemble.DeployNew(11, 1, Log))
                {
                    ensemble2.ConnectionString.Should().NotBe(connectionString);
                    connectionString = ensemble2.ConnectionString;

                    Thread.Sleep(DefaultTimeout);

                    WaitForNewConnectedClient(holder);
                    client = holder.GetConnectedClient().GetAwaiter().GetResult();
                    client.createAsync(path, new byte[0], acls, org.apache.zookeeper.CreateMode.PERSISTENT).GetAwaiter().GetResult();

                    client.getACLAsync(path).GetAwaiter().GetResult().Acls.Count.Should().Be(1);
                }
            }
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
        public async Task OnConnectionStateChanged_should_observe_auth_failed()
        {
            var holder = GetClientHolder(Ensemble.ConnectionString);
            var observer = GetObserver(holder);

            WaitForNewConnectedClient(holder);

            var client = await holder.GetConnectedClient();

            client.addAuthInfo("bad_scheme", new byte[0]);

            VerifyObserverMessages(observer, ConnectionState.Disconnected, ConnectionState.Connected, ConnectionState.AuthFailed, ConnectionState.Disconnected, ConnectionState.Connected);
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
        public void Dispose_should_be_tolerant_to_null_client()
        {
            var holder = GetClientHolder(null, 1.Seconds());
            holder.GetConnectedClient().ShouldCompleteIn(1.5.Seconds());
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