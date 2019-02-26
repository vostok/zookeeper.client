using FluentAssertions;
using NUnit.Framework;
using Vostok.ZooKeeper.Client.Helpers;

namespace Vostok.ZooKeeper.Client.Tests.Helpers
{
    [TestFixture]
    public class NodeHelper_Tests
    {
        [Test]
        public void ValidateDataSize_should_allow_null_data()
        {
            NodeHelper.ValidateDataSize(null).Should().BeTrue();
        }

        [Test]
        public void ValidateDataSize_should_allow_small_data()
        {
            NodeHelper.ValidateDataSize(new byte[NodeHelper.DataSizeLimit]).Should().BeTrue();
        }

        [Test]
        public void ValidateDataSize_should_not_allow_big_data()
        {
            NodeHelper.ValidateDataSize(new byte[NodeHelper.DataSizeLimit + 1]).Should().BeFalse();
        }
    }
}