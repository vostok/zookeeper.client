using FluentAssertions;
using NUnit.Framework;
using Vostok.Commons.Time;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Holder;
using Vostok.ZooKeeper.Client.Helpers;

namespace Vostok.ZooKeeper.Client.Tests
{
    [TestFixture]
    internal class ClientHolderState_Tests
    {
        private ZooKeeperClientSettings settings;

        [SetUp]
        public void Setup()
        {
            settings = new ZooKeeperClientSettings("127.0.0.1:4222")
            {
                CanBeReadOnly = false,
                Timeout = 13.Seconds()
            };
        }

        [Test]
        public void Should_work_with_suspended_state()
        {
            var state = new ClientHolderState(TimeBudget.StartNew(9.Seconds()), settings);

            state.ConnectionState.Should().Be(ConnectionState.Disconnected);
            state.ConnectionWatcher.Should().BeNull();
            state.TimeBeforeReset.Total.Should().Be(9.Seconds());
            state.IsSuspended.Should().BeTrue();
            state.IsConnected.Should().BeFalse();
            state.ConnectionString.Should().BeNull();

            state.Dispose();
        }

        [Test]
        public void Should_work_with_real_state()
        {
            var state = TestHelpers.CreateConnectedClientHolderState(settings);

            state.ConnectionState.Should().Be(ConnectionState.Connected);
            state.ConnectionWatcher.Should().NotBeNull();
            state.TimeBeforeReset.Total.Should().Be(settings.Timeout);
            state.IsSuspended.Should().BeFalse();
            state.IsConnected.Should().BeTrue();
            state.ConnectionString.Should().Be(settings.ConnectionStringProvider());

            state.Dispose();
        }
    }
}