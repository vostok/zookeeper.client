using FluentAssertions;
using FluentAssertions.Equivalency;
using NUnit.Framework;

namespace Vostok.ZooKeeper.Client.Tests
{
    [TestFixture]
    internal class Helper_Tests
    {
        [Test]
        public void ValidateDataSize_should_allow_null_data()
        {
            Helper.ValidateDataSize(null).Should().BeTrue();
        }

        [Test]
        public void ValidateDataSize_should_allow_small_data()
        {
            Helper.ValidateDataSize(new byte[Helper.DataSizeLimit]).Should().BeTrue();
        }

        [Test]
        public void ValidateDataSize_should_not_allow_big_data()
        {
            Helper.ValidateDataSize(new byte[Helper.DataSizeLimit + 1]).Should().BeFalse();
        }

        [TestCase("/aaaa", new[] { "aaaa" })]
        [TestCase("/aaaa/bbb", new[] { "aaaa", "bbb" })]
        [TestCase("/aaaa/bbb/c/d/e/f/long_123", new[] { "aaaa", "bbb", "c", "d", "e", "f", "long_123" })]
        public void SplitPath_should_split_by_slashes(string path, string[] expected)
        {
            Helper.SplitPath(path).Should().BeEquivalentTo(expected, options => options.WithStrictOrdering());
        }
    }
}