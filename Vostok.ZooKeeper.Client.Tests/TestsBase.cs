using System;
using System.Diagnostics;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Commons.Testing;
using Vostok.Commons.Testing.Observable;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Helpers;
using Vostok.ZooKeeper.Client.Holder;
using Vostok.ZooKeeper.LocalEnsemble;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client.Tests
{
    internal abstract class TestsBase
    {
        protected static readonly ILog Log = new SynchronousConsoleLog();
        protected static TimeSpan DefaultTimeout = 10.Seconds();

        protected ZooKeeperEnsemble Ensemble;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Ensemble = ZooKeeperEnsemble.DeployNew(1, Log);
        }

        [SetUp]
        public void SetUp()
        {
            if (!Ensemble.IsRunning)
                Ensemble.Start();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            Ensemble.Dispose();
        }

        protected static void WaitForNewConnectedClient(ClientHolder holder)
        {
            holder.GetConnectedClient().ShouldCompleteIn(DefaultTimeout).Should().NotBe(null);
            holder.ConnectionState.Should().Be(ConnectionState.Connected);
        }

        protected static void WaitForDisconnectedState(ZooKeeperClient client)
        {
            Action assertion = () => { client.ConnectionState.Should().Be(ConnectionState.Disconnected); };
            assertion.ShouldPassIn(5.Seconds());
        }

        protected static void WaitForDisconnectedState(ClientHolder holder)
        {
            Action assertion = () => { holder.ConnectionState.Should().Be(ConnectionState.Disconnected); };
            assertion.ShouldPassIn(5.Seconds());
        }

        protected static void VerifyObserverMessages(TestObserver<ConnectionState> observer, params ConnectionState[] states)
        {
            Action assertion = () => { observer.Values.Should().BeEquivalentTo(states, options => options.WithStrictOrdering()); };
            assertion.ShouldPassIn(DefaultTimeout, 0.5.Seconds());
        }

        protected static void VerifyObserverMessages(TestObserver<ConnectionState> observer, params Notification<ConnectionState>[] states)
        {
            Action assertion = () => { observer.Messages.Should().BeEquivalentTo(states, options => options.WithStrictOrdering()); };
            assertion.ShouldPassIn(DefaultTimeout, 0.5.Seconds());
        }

        protected static async Task KillSession(ClientHolder holder, string connectionString)
        {
            if (holder.ConnectionState != ConnectionState.Connected)
                return;

            var sessionId = holder.SessionId;
            var sessionPassword = holder.SessionPassword;

            await KillSession(connectionString, sessionId, sessionPassword, holder.OnConnectionStateChanged);
        }

        protected static async Task KillSession(ZooKeeperClient client, string connectionString)
        {
            Log.Info("KILL BEGIN");
            if (client.ConnectionState != ConnectionState.Connected)
                return;

            var sessionId = client.SessionId;
            var sessionPassword = client.SessionPassword;

            await KillSession(connectionString, sessionId, sessionPassword, client.OnConnectionStateChanged);
            Log.Info("KILL END");
        }

        protected ZooKeeperClient GetClient(TimeSpan? timeout = null)
        {
            var settings = new ZooKeeperClientSettings(Ensemble.ConnectionString, Log) {Timeout = timeout ?? DefaultTimeout};
            return new ZooKeeperClient(settings);
        }

        protected ClientHolder GetClientHolder(string connectionString, TimeSpan? timeout = null)
        {
            var settings = new ZooKeeperClientSettings(connectionString, Log) {Timeout = timeout ?? DefaultTimeout};
            return new ClientHolder(settings, Log);
        }

        protected TestObserver<ConnectionState> GetObserver(ClientHolder holder)
        {
            var observer = new TestObserver<ConnectionState>();
            holder.OnConnectionStateChanged.Subscribe(observer);
            return observer;
        }

        private static async Task KillSession(string connectionString, long sessionId, byte[] sessionPassword, IObservable<ConnectionState> onConnectionStateChanged)
        {
            var zooKeeper = new ZooKeeperNetExClient(connectionString, 5000, null, sessionId, sessionPassword);
            var observer = new TestObserver<ConnectionState>();
            onConnectionStateChanged.Subscribe(observer);

            try
            {
                var watch = Stopwatch.StartNew();
                while (watch.Elapsed < DefaultTimeout)
                {
                    if (zooKeeper.getState().Equals(ZooKeeperNetExClient.States.CONNECTED))
                    {
                        break;
                    }

                    Thread.Sleep(100);
                }

                await zooKeeper.closeAsync();

                while (watch.Elapsed < DefaultTimeout)
                {
                    if (observer.Values.Contains(ConnectionState.Expired))
                    {
                        return;
                    }

                    Thread.Sleep(100);
                }

                throw new TimeoutException($"Expected to kill session within {DefaultTimeout}, but failed to do so.");
            }
            finally
            {
                await zooKeeper.closeAsync();
            }
        }
    }
}