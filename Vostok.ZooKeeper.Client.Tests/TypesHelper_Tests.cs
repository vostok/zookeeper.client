using FluentAssertions;
using NUnit.Framework;
using Vostok.Commons.Time;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.ZooKeeper.Client.Helpers;
using CreateMode = org.apache.zookeeper.CreateMode;

namespace Vostok.ZooKeeper.Client.Tests
{
    [TestFixture]
    internal class TypesHelper_Tests
    {
        private readonly ILog log = new SynchronousConsoleLog();

        [Test]
        public void ToInnerConnectionTimeout_should_convert_ZooKeeperClientSettings_to_Milliseconds()
        {
            new ZooKeeperClientSettings("replicas", log) {Timeout = 42.Seconds()}.ToInnerConnectionTimeout().Should().Be(42000);
        }

        [Test]
        public void ToInnerCreateMode_should_convert_CreateMode_to_other_CreateMode()
        {
            Abstractions.Model.CreateMode.EphemeralSequential.ToInnerCreateMode().Should().Be(CreateMode.EPHEMERAL_SEQUENTIAL);
        }
    }
}