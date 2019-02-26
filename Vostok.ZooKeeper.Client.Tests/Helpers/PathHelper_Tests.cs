using FluentAssertions;
using NUnit.Framework;
using Vostok.ZooKeeper.Client.Helpers;

namespace Vostok.ZooKeeper.Client.Tests.Helpers
{
    [TestFixture]
    public class PathHelper_Tests
    {
        [TestCase("/aaaa", new[] { "aaaa" })]
        [TestCase("/aaaa/bbb", new[] { "aaaa", "bbb" })]
        [TestCase("/aaaa/bbb/c/d/e/f/long_123", new[] { "aaaa", "bbb", "c", "d", "e", "f", "long_123" })]
        public void SplitPath_should_split_by_slashes(string path, string[] expected)
        {
            PathHelper.SplitPath(path).Should().BeEquivalentTo(expected, options => options.WithStrictOrdering());
        }
    }
}