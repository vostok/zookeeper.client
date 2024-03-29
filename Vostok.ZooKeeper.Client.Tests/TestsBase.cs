﻿using System;
using System.Reactive;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Commons.Testing;
using Vostok.Commons.Testing.Observable;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.Logging.File;
using Vostok.Logging.File.Configuration;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Holder;
using Vostok.ZooKeeper.Client.Helpers;
using Vostok.ZooKeeper.LocalEnsemble;
using Vostok.ZooKeeper.Testing;

namespace Vostok.ZooKeeper.Client.Tests
{
    internal abstract class TestsBase
    {
        protected const string IgnoreReason = "Doesn't work on Unix systems because https://github.com/shayhatsor/zookeeper/issues/45";
        protected static readonly ILog Log = new CompositeLog(new SynchronousConsoleLog(), new FileLog(new FileLogSettings {FileOpenMode = FileOpenMode.Rewrite}));
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
            holder.GetConnectedClientObject().ShouldCompleteIn(DefaultTimeout).Should().NotBe(null);
            holder.ConnectionState.Should().Be(ConnectionState.Connected);
        }

        protected static void WaitForDisconnectedState(ZooKeeperClient client)
        {
            Action assertion = () => { client.ConnectionState.Should().Be(ConnectionState.Disconnected); };
            assertion.ShouldPassIn(5.Seconds());
        }

        protected static void WaitForDisconnectedState(ClientHolder holder) =>
            WaitForState(holder, ConnectionState.Disconnected);

        protected static void WaitForDiedState(ClientHolder holder) =>
            WaitForState(holder, ConnectionState.Died);
        
        protected static void WaitForState(ClientHolder holder, ConnectionState state, TimeSpan? timeout = null)
        {
            timeout = timeout ?? 5.Seconds();
            Action assertion = () => { holder.ConnectionState.Should().Be(state); };
            assertion.ShouldPassIn(timeout.Value);
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

        protected static Task KillSession(ClientHolder holder, string connectionString) =>
            ZooKeeperClientTestsHelper.KillSession(holder.SessionId, holder.SessionPassword, holder.OnConnectionStateChanged, connectionString, DefaultTimeout);

        protected static Task KillSession(ZooKeeperClient client, string connectionString) =>
            ZooKeeperClientTestsHelper.KillSession(client.SessionId, client.SessionPassword, client.OnConnectionStateChanged, connectionString, DefaultTimeout);

        protected ZooKeeperClient GetClient(TimeSpan? timeout = null)
        {
            var settings = new ZooKeeperClientSettings(Ensemble.ConnectionString)
            {
                Timeout = timeout ?? DefaultTimeout,
                LoggingLevel = LogLevel.Debug
            };
            return new ZooKeeperClient(settings, Log);
        }

        protected ClientHolder GetClientHolder(string connectionString, TimeSpan? timeout = null) =>
            GetClientHolder(() => connectionString, timeout);
        
        protected ClientHolder GetClientHolder(Func<string> connectionString, TimeSpan? timeout = null)
        {
            var settings = new ZooKeeperClientSettings(connectionString) {Timeout = timeout ?? DefaultTimeout, LoggingLevel = LogLevel.Debug};
            return new ClientHolder(settings, Log);
        }

        protected TestObserver<ConnectionState> GetObserver(ClientHolder holder)
        {
            var observer = new TestObserver<ConnectionState>();
            holder.OnConnectionStateChanged.Subscribe(observer);
            return observer;
        }
    }
}