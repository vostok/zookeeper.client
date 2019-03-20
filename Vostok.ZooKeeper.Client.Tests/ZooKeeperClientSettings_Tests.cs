using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace Vostok.ZooKeeper.Client.Tests
{
    [TestFixture]
    internal class ZooKeeperClientSettings_Tests
    {
        [Test]
        public void ConnectionStringProvider_from_string()
        {
            var str = "localhost:56736,localhost:56739,localhost:56742";
            var settings = new ZooKeeperClientSettings(str);
            settings.ConnectionStringProvider().Should().Be(str);
        }

        [Test]
        public void ConnectionStringProvider_from_string_provider()
        {
            var str = "localhost:56736,localhost:56739,localhost:56742";
            var settings = new ZooKeeperClientSettings(() => str);
            settings.ConnectionStringProvider().Should().Be(str);
        }

        [Test]
        public void ConnectionStringProvider_from_replicas()
        {
            var str = "http://localhost:56736/,http://localhost:56739/,http://localhost:56742/";
            var replicas = str.Split(',').Select(x => new Uri(x)).ToArray();
            var settings = new ZooKeeperClientSettings(replicas);
            settings.ConnectionStringProvider().Should().Be("localhost:56736,localhost:56739,localhost:56742");
        }

        [Test]
        public void ConnectionStringProvider_from_replicas_provider()
        {
            var str = "http://localhost:56736/,http://localhost:56739/,http://localhost:56742/";
            var replicas = str.Split(',').Select(x => new Uri(x)).ToArray();
            var settings = new ZooKeeperClientSettings(() => replicas);
            settings.ConnectionStringProvider().Should().Be("localhost:56736,localhost:56739,localhost:56742");
        }
    }
}