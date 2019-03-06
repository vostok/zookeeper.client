using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;

namespace Vostok.ZooKeeper.Client.Tests
{
    [TestFixture]
    internal class NodeStat_Tests : TestsBase
    {
        private ZooKeeperClient client;

        [OneTimeSetUp]
        public new void OneTimeSetUp()
        {
            client = GetClient();
        }

        [OneTimeTearDown]
        public new void OneTimeTearDown()
        {
            client.Dispose();
        }

        [Test]
        public async Task CreatedZxId_CreatedTime_should_increase()
        {
            var result1 = await client.CreateAsync("/create/a", CreateMode.Persistent);
            var result2 = await client.CreateAsync("/create/b", CreateMode.Persistent);
            var stat1 = await GetNodeStat(result1.NewPath);
            var stat2 = await GetNodeStat(result2.NewPath);

            stat2.CreatedZxId.Should().Be(stat1.CreatedZxId + 1);
            stat2.CreatedTime.Should().BeOnOrAfter(stat1.CreatedTime);
        }

        [Test]
        public async Task ModifiedZxId_ModifiedTime_Version_should_increase()
        {
            var result = await client.CreateAsync("/modify/a", CreateMode.Persistent);
            var stat1 = await GetNodeStat(result.NewPath);

            stat1.ModifiedZxId.Should().Be(stat1.CreatedZxId);
            stat1.Version.Should().Be(0);

            (await client.SetDataAsync(result.NewPath, new byte[] {1, 2, 3})).EnsureSuccess();

            var stat2 = await GetNodeStat(result.NewPath);

            stat2.ModifiedZxId.Should().Be(stat1.ModifiedZxId + 1);
            stat2.ModifiedTime.Should().BeOnOrAfter(stat1.ModifiedTime);
            stat2.Version.Should().Be(1);
        }

        [Test]
        public async Task ModifiedChildrenZxId_ChildrenVersion_should_increase()
        {
            var result = await client.CreateAsync("/modify_children/a", CreateMode.Persistent);
            var stat1 = await GetNodeStat(result.NewPath);

            stat1.ModifiedChildrenZxId.Should().Be(stat1.CreatedZxId);
            stat1.ChildrenVersion.Should().Be(0);

            (await client.CreateAsync(result.NewPath + "/b", CreateMode.Persistent)).EnsureSuccess();

            var stat2 = await GetNodeStat(result.NewPath);

            stat2.ModifiedChildrenZxId.Should().Be(stat1.ModifiedChildrenZxId + 1);
            stat2.ModifiedZxId.Should().Be(stat1.ModifiedZxId);
            stat2.ChildrenVersion.Should().Be(1);
        }

        [Test]
        public async Task EphemeralOwner_should_return_current_client_session_id()
        {
            var result = await client.CreateAsync("/owner/a", CreateMode.Ephemeral);
            var stat = await GetNodeStat(result.NewPath);

            stat.EphemeralOwner.Should().Be(client.SessionId);
        }

        [Test]
        public async Task DataLength_should_return_data_length()
        {
            var result = await client.CreateAsync("/data/a", CreateMode.Persistent);

            await client.SetDataAsync(result.NewPath, null);
            (await GetNodeStat(result.NewPath)).DataLength.Should().Be(0);

            await client.SetDataAsync(result.NewPath, new byte[0]);
            (await GetNodeStat(result.NewPath)).DataLength.Should().Be(0);

            await client.SetDataAsync(result.NewPath, new byte[42]);
            (await GetNodeStat(result.NewPath)).DataLength.Should().Be(42);
        }

        [Test]
        public async Task NumberOfChildren_should_return_number_of_children()
        {
            var root = "/number_of_children/a";

            (await client.CreateAsync(root, CreateMode.Persistent)).EnsureSuccess();
            (await GetNodeStat(root)).NumberOfChildren.Should().Be(0);

            (await client.CreateAsync(root + "/x", CreateMode.Persistent)).EnsureSuccess();
            (await GetNodeStat(root)).NumberOfChildren.Should().Be(1);

            (await client.CreateAsync(root + "/y", CreateMode.Persistent)).EnsureSuccess();
            (await GetNodeStat(root)).NumberOfChildren.Should().Be(2);

            (await client.CreateAsync(root + "/x/y", CreateMode.Persistent)).EnsureSuccess();
            (await GetNodeStat(root)).NumberOfChildren.Should().Be(2);
        }

        private async Task<NodeStat> GetNodeStat(string path)
        {
            return (await client.ExistsAsync(path)).Stat;
        }
    }
}