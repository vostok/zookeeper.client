using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;

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
        public async Task CreatedZxid_CreatedTime_should_increase()
        {
            var result1 = await client.CreateAsync(new CreateRequest("/create/a", CreateMode.Persistent));
            var result2 = await client.CreateAsync(new CreateRequest("/create/b", CreateMode.Persistent));
            var stat1 = await GetNodeStat(result1.NewPath);
            var stat2 = await GetNodeStat(result2.NewPath);

            stat2.CreatedZxid.Should().Be(stat1.CreatedZxid + 1);
            stat2.CreatedTime.Should().BeGreaterThan(stat1.CreatedZxid);
        }

        [Test]
        public async Task ModifiedZxid_Version_should_increase()
        {
            var result = await client.CreateAsync(new CreateRequest("/modify/a", CreateMode.Persistent));
            var stat1 = await GetNodeStat(result.NewPath);

            stat1.ModifiedZxid.Should().Be(stat1.CreatedZxid);
            stat1.Version.Should().Be(0);

            (await client.SetDataAsync(new SetDataRequest(result.NewPath, new byte[] {1, 2, 3}))).EnsureSuccess();

            var stat2 = await GetNodeStat(result.NewPath);

            stat2.ModifiedZxid.Should().Be(stat1.ModifiedZxid + 1);
            stat2.Version.Should().Be(1);
        }

        [Test]
        public async Task ModifiedChildrenZxid_ChildrenVersion_should_increase()
        {
            var result = await client.CreateAsync(new CreateRequest("/modify_children/a", CreateMode.Persistent));
            var stat1 = await GetNodeStat(result.NewPath);

            stat1.ModifiedChildrenZxid.Should().Be(stat1.CreatedZxid);
            stat1.ChildrenVersion.Should().Be(0);

            (await client.CreateAsync(new CreateRequest(result.NewPath + "/b", CreateMode.Persistent))).EnsureSuccess();

            var stat2 = await GetNodeStat(result.NewPath);

            stat2.ModifiedChildrenZxid.Should().Be(stat1.ModifiedChildrenZxid + 1);
            stat2.ModifiedZxid.Should().Be(stat1.ModifiedZxid);
            stat2.ChildrenVersion.Should().Be(1);
        }

        [Test]
        public async Task EphemeralOwner_should_return_current_client_session_id()
        {
            var result = await client.CreateAsync(new CreateRequest("/owner/a", CreateMode.Ephemeral));
            var stat = await GetNodeStat(result.NewPath);

            stat.EphemeralOwner.Should().Be(client.SessionId);
        }

        [Test]
        public async Task DataLength_should_return_data_lengt()
        {
            var result = await client.CreateAsync(new CreateRequest("/data/a", CreateMode.Persistent));

            await client.SetDataAsync(new SetDataRequest(result.NewPath, null));
            (await GetNodeStat(result.NewPath)).DataLength.Should().Be(0);

            await client.SetDataAsync(new SetDataRequest(result.NewPath, new byte[0]));
            (await GetNodeStat(result.NewPath)).DataLength.Should().Be(0);

            await client.SetDataAsync(new SetDataRequest(result.NewPath, new byte[42]));
            (await GetNodeStat(result.NewPath)).DataLength.Should().Be(42);
        }

        [Test]
        public async Task NumberOfChildren_should_return_number_of_children()
        {
            var root = "/number_of_children/a";

            (await client.CreateAsync(new CreateRequest(root, CreateMode.Persistent))).EnsureSuccess();
            (await GetNodeStat(root)).NumberOfChildren.Should().Be(0);

            (await client.CreateAsync(new CreateRequest(root + "/x", CreateMode.Persistent))).EnsureSuccess();
            (await GetNodeStat(root)).NumberOfChildren.Should().Be(1);

            (await client.CreateAsync(new CreateRequest(root + "/y", CreateMode.Persistent))).EnsureSuccess();
            (await GetNodeStat(root)).NumberOfChildren.Should().Be(2);

            (await client.CreateAsync(new CreateRequest(root + "/x/y", CreateMode.Persistent))).EnsureSuccess();
            (await GetNodeStat(root)).NumberOfChildren.Should().Be(2);
        }

        private async Task<NodeStat> GetNodeStat(string path)
        {
            return (await client.ExistsAsync(new ExistsRequest(path))).Stat;
        }
    }
}