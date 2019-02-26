﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Commons.Helpers.Observable;
using Vostok.Commons.Testing;
using Vostok.Commons.Testing.Observable;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.LocalEnsemble;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client.Tests
{
    internal abstract class TestsBase
    {
        protected static TimeSpan DefaultTimeout = 10.Seconds();
        protected static readonly ILog Log = new SynchronousConsoleLog();

        protected ZooKeeperEnsemble ensemble;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            ensemble = ZooKeeperEnsemble.DeployNew(1, Log);
        }

        [SetUp]
        public void SetUp()
        {
            if (!ensemble.IsRunning)
                ensemble.Start();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            ensemble.Dispose();
        }

        protected static ZooKeeperNetExClient WaitForNewConnectedClient(ClientHolder holder)
        {
            var client = holder.GetConnectedClient().Result;
            client.getState().Should().Be(ZooKeeperNetExClient.States.CONNECTED);
            holder.ConnectionState.Should().Be(ConnectionState.Connected);
            return client;
        }

        protected static void WaitForDisconectedState(ClientHolder holder)
        {
            Action assertion = () => { holder.ConnectionState.Should().Be(ConnectionState.Disconnected); };
            assertion.ShouldPassIn(5.Seconds());
        }

        protected static void VerifyObserverMessages(TestObserver<ConnectionState> observer, params ConnectionState[] states)
        {
            Action assertion = () => { observer.Values.Should().BeEquivalentTo(states, options => options.WithStrictOrdering()); };
            assertion.ShouldPassIn(DefaultTimeout, 0.5.Seconds());
        }

        protected static async Task KillSession(ClientHolder holder, string connectionString)
        {
            if (holder.ConnectionState != ConnectionState.Connected)
                return;

            var client = await holder.GetConnectedClient();
            var sessionId = client.getSessionId();
            var sessionPassword = client.getSessionPasswd();

            await KillSession(connectionString, sessionId, sessionPassword, holder.OnConnectionStateChanged);
        }

        protected static async Task KillSession(ZooKeeperClient client, string connectionString)
        {
            if (client.ConnectionState != ConnectionState.Connected)
                return;

            var sessionId = client.SessionId;
            var sessionPassword = client.SessionPassword;

            await KillSession(connectionString, sessionId, sessionPassword, client.OnConnectionStateChanged);
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
                    if (observer.Values.Contains(ConnectionState.Disconnected))
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

        protected ZooKeeperClient GetClient(TimeSpan? timeout = null)
        {
            var setup = new ZooKeeperClientSetup(ensemble.ConnectionString) {Timeout = timeout ?? DefaultTimeout};
            return new ZooKeeperClient(Log, setup);
        }

        protected ClientHolder GetClientHolder(string connectionString, TimeSpan? timeout = null)
        {
            var setup = new ZooKeeperClientSetup(connectionString) {Timeout = timeout ?? DefaultTimeout};
            return new ClientHolder(Log, setup);
        }

        protected TestObserver<ConnectionState> GetObserver(ClientHolder holder)
        {
            var observer = new TestObserver<ConnectionState>();
            holder.OnConnectionStateChanged.Subscribe(observer);
            return observer;
        }
    }
}