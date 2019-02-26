using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Helpers;

namespace Vostok.ZooKeeper.Client.Tests
{
    [TestFixture]
    internal class ZooKeeperClient_Tests : TestsBase
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
        public async Task Should_return_NotConnected()
        {
            ensemble.Stop();
            WaitForDisconectedState(client);
            var result = await client.CreateAsync(new CreateRequest("/return_connection_loss", CreateMode.Persistent));

            result.Status.Should().Be(ZooKeeperStatus.NotConnected);
        }

        [TestCase(CreateMode.Persistent)]
        [TestCase(CreateMode.PersistentSequential)]
        [TestCase(CreateMode.Ephemeral)]
        [TestCase(CreateMode.EphemeralSequential)]
        public async Task Create_should_create_node_in_different_modes(CreateMode createMode)
        {
            var path = $"/create_node_{createMode}";

            var createResult = await client.CreateAsync(new CreateRequest(path, createMode));
            createResult.EnsureSuccess();

            if (!createMode.IsSequential())
                createResult.NewPath.Should().Be(path);

            await VerifyNodeCreated(client, createResult.NewPath);

            await KillSession(client, ensemble.ConnectionString);

            if (createMode.IsEphemeral())
                await VerifyNodeDeleted(client, createResult.NewPath);
            else
                await VerifyNodeCreated(client, createResult.NewPath);
        }

        [TestCase(CreateMode.Ephemeral)]
        [TestCase(CreateMode.Persistent)]
        public async Task Create_should_create_nested_node(CreateMode mode)
        {
            var paths = new List<string>
            {
                $"/create_nested_{mode}/a/b/c_first/d/e_first",
                $"/create_nested_{mode}/a/b/c_first/d/e_second",
                $"/create_nested_{mode}/a/b/c_second"
            };

            foreach (var path in paths)
            {
                var createResult = await client.CreateAsync(new CreateRequest(path, CreateMode.Persistent));
                createResult.EnsureSuccess();
                (await client.ExistsAsync(new ExistsRequest(path))).Exists.Should().BeTrue();
            }
        }

        [Test, Combinatorial]
        public async Task Create_should_create_sequential_node(
            [Values("nested_sequential/aaa/bb/c")] string path,
            [Values(CreateMode.PersistentSequential, CreateMode.EphemeralSequential)] CreateMode createMode)
        {
            path = $"/{createMode}_{path}";
            
            for (var i = 0; i < 3; i++)
            {
                var createResult = await client.CreateAsync(new CreateRequest(path, createMode));
                createResult.EnsureSuccess();

                await VerifyNodeCreated(client, createResult.NewPath);

                createResult.NewPath.Should().Be($"{path}{i:D10}");
            }
        }

        [Test]
        public async Task Create_should_create_sequential_node_with_shared_parent_counter()
        {
            // When creating a znode you can also request that ZooKeeper append a monotonically increasing counter to the end of path.
            // This counter is unique to the parent znode.

            var createResult = await client.CreateAsync(new CreateRequest("/shared_sequential/a", CreateMode.PersistentSequential));
            createResult.NewPath.Should().Be($"/shared_sequential/a{0:D10}");

            createResult = await client.CreateAsync(new CreateRequest("/shared_sequential/b", CreateMode.PersistentSequential));
            createResult.NewPath.Should().Be($"/shared_sequential/b{1:D10}");
        }

        [Test, Combinatorial]
        public async Task Create_should_save_data(
            [Values(0, 1, 10, 1024, 1024*10, 1024 * 100, NodeHelper.DataSizeLimit)] int size,
            [Values(CreateMode.Persistent, CreateMode.Ephemeral)] CreateMode createMode)
        {
            var data = Enumerable.Range(0, size).Select(i => (byte)(i % 256)).ToArray();
            var createResult = await client.CreateAsync(new CreateRequest($"/create_with_data_{size}_{createMode}", createMode) { Data = data });
            createResult.EnsureSuccess();
        }

        [Test]
        public async Task Create_should_return_BadArguments_for_big_data()
        {
            var createResult = await client.CreateAsync(new CreateRequest("/big_data", CreateMode.Persistent) {Data = new byte[NodeHelper.DataSizeLimit + 1]});
            createResult.Status.Should().Be(ZooKeeperStatus.BadArguments);
            createResult.Exception.Should().BeOfType<ArgumentException>();
        }

        [TestCase("without_slash_at_the_beggingig")]
        [TestCase("/with_extra_slash_at_the_ending/")]
        public async Task Create_should_return_BadArguments_for_bad_path(string path)
        {
            var createResult = await client.CreateAsync(new CreateRequest(path, CreateMode.Persistent));
            createResult.Status.Should().Be(ZooKeeperStatus.BadArguments);
            createResult.Exception.Should().BeOfType<ArgumentException>();

            ((Action)(() => createResult.EnsureSuccess())).Should().Throw<ZooKeeperException>();
        }

        [Test]
        public async Task Create_should_return_NodeAlreadyExists()
        {
            var createResult = await client.CreateAsync(new CreateRequest("/create_same_node_twice", CreateMode.Persistent));
            createResult.EnsureSuccess();
            createResult = await client.CreateAsync(new CreateRequest("/create_same_node_twice", CreateMode.Persistent));

            ((Action)(() => createResult.EnsureSuccess())).Should().Throw<ZooKeeperException>();
            createResult.Status.Should().Be(ZooKeeperStatus.NodeAlreadyExists);
        }

        [TestCase(CreateMode.Ephemeral)]
        [TestCase(CreateMode.Persistent)]
        public async Task Create_should_return_ChildrenForEphemeralsAreNotAllowed(CreateMode childCreateMode)
        {
            var createResult = await client.CreateAsync(new CreateRequest($"/ephemeral_parent_{childCreateMode}", CreateMode.Ephemeral));
            createResult.EnsureSuccess();

            createResult = await client.CreateAsync(new CreateRequest($"/ephemeral_parent_{childCreateMode}/child", childCreateMode));
            ((Action)(() => createResult.EnsureSuccess())).Should().Throw<ZooKeeperException>();
            createResult.Status.Should().Be(ZooKeeperStatus.ChildrenForEphemeralsAreNotAllowed);
        }

        [Test, Combinatorial]
        public async Task GetData_should_return_saved_data(
            [Values(0, 1, 10, 1024, 1024 * 10, 1024 * 100, NodeHelper.DataSizeLimit)] int size,
            [Values(CreateMode.Persistent, CreateMode.Ephemeral)] CreateMode createMode)
        {
            var data = Enumerable.Range(0, size).Select(i => (byte)(i % 256)).ToArray();
            var path = $"/get_saved_data_{size}_{createMode}";

            var createResult = await client.CreateAsync(new CreateRequest(path, createMode) { Data = data });
            createResult.EnsureSuccess();

            var result = await client.GetDataAsync(new GetDataRequest(path));
            result.Data.Should().BeEquivalentTo(data, options => options.WithStrictOrdering());
        }

        [Test]
        public async Task GetData_should_return_modified_data()
        {
            var path = "/get_modified_data";

            var bytes1 = new byte[]{0, 1, 2, 3};
            var bytes2 = new byte[]{3, 2, 1};
            var createResult = await client.CreateAsync(new CreateRequest(path, CreateMode.Persistent) { Data = bytes1});
            createResult.EnsureSuccess();

            var result = await client.GetDataAsync(new GetDataRequest(path));
            result.Data.Should().BeEquivalentTo(bytes1, options => options.WithStrictOrdering());
            result.Stat.DataLength.Should().Be(4);
            result.Stat.Version.Should().Be(0);

            await client.SetDataAsync(new SetDataRequest(path, bytes2));
            result = await client.GetDataAsync(new GetDataRequest(path));
            result.Data.Should().BeEquivalentTo(bytes2, options => options.WithStrictOrdering());
            result.Stat.DataLength.Should().Be(3);
            result.Stat.Version.Should().Be(1);
        }

        [Test]
        public async Task GetData_should_return_NodeNotFound()
        {
            var result = await client.GetDataAsync(new GetDataRequest("/get_unexisting_node"));

            ((Action)(() => result.EnsureSuccess())).Should().Throw<ZooKeeperException>();
            result.Status.Should().Be(ZooKeeperStatus.NodeNotFound);
        }

        [TestCase("without_slash_at_the_beggingig")]
        [TestCase("/with_extra_slash_at_the_ending/")]
        public async Task GetData_should_return_BadArguments_for_bad_path(string path)
        {
            var createResult = await client.GetDataAsync(new GetDataRequest(path));
            createResult.Status.Should().Be(ZooKeeperStatus.BadArguments);
            createResult.Exception.Should().BeOfType<ArgumentException>();

            ((Action)(() => createResult.EnsureSuccess())).Should().Throw<ZooKeeperException>();
        }

        private static async Task VerifyNodeCreated(ZooKeeperClient client, string path)
        {
            var node = await client.GetDataAsync(new GetDataRequest(path));
            node.EnsureSuccess();

            node.Path.Should().Be(path);
            node.Stat.Version.Should().Be(0);
        }

        private static async Task VerifyNodeDeleted(ZooKeeperClient client, string path)
        {
            var node = await client.ExistsAsync( new ExistsRequest(path));
            node.EnsureSuccess();

            node.Exists.Should().BeFalse();
        }

        //[Test]
        //public void Test_KillSession()
        //{
        //    using (var client = CreateNewClient())
        //    {
        //        var sessionId = client.SessionId;
        //        var sessionPassword = client.SessionPassword;

        //        client.KillSession(TimeSpan.FromSeconds(10));
        //        client.WaitUntilConnected();

        //        Action checkNewSessionIdAndPassword = () =>
        //        {
        //            // ReSharper disable AccessToDisposedClosure
        //            client.SessionId.Should().NotBe(sessionId);
        //            client.SessionPassword.Should().NotBeEquivalentTo(sessionPassword);
        //        };
        //        checkNewSessionIdAndPassword.ShouldPassIn(10.Seconds());
        //    }
        //}

        //[TestCase("/persistentNode")]
        //public void Created_persistent_node_should_be_alive_after_client_stop(string path)
        //{
        //    var client = CreateNewClient();
        //    {
        //        CreateNode(path, org.apache.zookeeper.CreateMode.PERSISTENT, client);
        //    }
        //    client.closeAsync().Wait();

        //    var anotherClient = CreateNewClient();
        //    {
        //        EnsureNodeExist(path, anotherClient);

        //        anotherClient.createAsync(path, null, null, org.apache.zookeeper.CreateMode.PERSISTENT).GetAwaiter().GetResult();

        //        DeleteNode(path, anotherClient);
        //    }
        //    anotherClient.closeAsync().Wait();
        //}

        //[TestCase("/ephemeral/node1", "/ephemeral/node2")]
        //public void Created_ephemeral_node_should_disappear_when_owning_client_Disposes(params string[] nodes)
        //{
        //    var client = CreateNewClient();

        //    foreach (var node in nodes)
        //    {
        //        CreateNode(node, org.apache.zookeeper.CreateMode.EPHEMERAL, client);
        //    }

        //    client.closeAsync().Wait();

        //    var anotherClient = CreateNewClient();
        //    {
        //        foreach (var node in nodes)
        //        {
        //            EnsureNodeDoesNotExist(node, anotherClient);
        //        }
        //    }
        //    anotherClient.closeAsync().Wait();
        //}

        //[TestCase("/forDelete")]
        //public void Delete_should_delete_ephemeral_node(string path)
        //{
        //    var client = CreateNewClient();
        //    {
        //        CreateNode(path, org.apache.zookeeper.CreateMode.EPHEMERAL, client);

        //        DeleteNode(path, client);

        //        EnsureNodeDoesNotExist(path, client);
        //    }
        //    client.closeAsync().Wait();

        //    var anotherClient = CreateNewClient();
        //    {
        //        EnsureNodeDoesNotExist(path, anotherClient);
        //    }
        //    anotherClient.closeAsync().Wait();
        //}

        //[TestCase("/forDelete")]
        //public void Delete_should_delete_persistent_node(string path)
        //{
        //    var client = CreateNewClient();
        //    {
        //        CreateNode(path, org.apache.zookeeper.CreateMode.PERSISTENT, client);

        //        DeleteNode(path, client);

        //        EnsureNodeDoesNotExist(path, client);
        //    }
        //    client.closeAsync().Wait();

        //    var anotherClient = CreateNewClient();
        //    {
        //        EnsureNodeDoesNotExist(path, anotherClient);
        //    }
        //    anotherClient.closeAsync().Wait();
        //}

        //[TestCase("/forDelete/nonexistent/node")]
        //public void Delete_nonexistent_node_should_return_NoNode(string path)
        //{
        //    var client = CreateNewClient();
        //    {
        //        DeleteNonexistentNode(path, client);

        //        EnsureNodeDoesNotExist(path, client);
        //    }
        //    client.closeAsync().Wait();

        //    var anotherClient = CreateNewClient();
        //    {
        //        EnsureNodeDoesNotExist(path, anotherClient);
        //    }
        //    anotherClient.closeAsync().Wait();
        //}

        //[TestCase("/setData/node/qwerty", "qwerty")]
        //public void SetData_and_GetData_should_works_correct(string path, string data)
        //{
        //    var client = CreateNewClient();
        //    {
        //        CreateNode(path, org.apache.zookeeper.CreateMode.PERSISTENT, client);

        //        SetData(path, data, client);

        //        EnsureDataExists(path, client, data, 1);

        //        DeleteNode(path, client);
        //    }
        //    client.closeAsync().Wait();
        //}

        //[TestCase("/getChildrenEphemeral/child1", "/getChildrenEphemeral/child2", "/getChildrenEphemeral/child3")]
        //public void GetChildren_should_return_all_children_from_ephemeral_node(params string[] nodes)
        //{
        //    var rootNode = "/" + nodes.First().Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries).First();
        //    var children = nodes.Select(x => x.Replace(rootNode + "/", string.Empty)).ToArray();

        //    var client = CreateNewClient();
        //    {
        //        CreateNode("/getChildrenEphemeral", org.apache.zookeeper.CreateMode.PERSISTENT, client);
        //        foreach (var node in nodes)
        //        {
        //            CreateNode(node, org.apache.zookeeper.CreateMode.EPHEMERAL, client);
        //        }

        //        EnsureChildrenExists(client, rootNode, children);
        //    }
        //    client.closeAsync().Wait();
        //}

        //[TestCase("/getChildrenPersistent/child1", "/getChildrenPersistent/child2", "/getChildrenPersistent/child3")]
        //public void GetChildren_should_return_all_children_from_persistent_node(params string[] nodes)
        //{
        //    var rootNode = "/" + nodes.First().Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries).First();
        //    var children = nodes.Select(x => x.Replace(rootNode + "/", string.Empty)).ToArray();

        //    var client = CreateNewClient();
        //    {
        //        CreateNode("/getChildrenPersistent", org.apache.zookeeper.CreateMode.PERSISTENT, client);
        //        foreach (var node in nodes)
        //        {
        //            CreateNode(node, org.apache.zookeeper.CreateMode.PERSISTENT, client);
        //        }

        //        EnsureChildrenExists(client, rootNode, children);
        //    }
        //    client.closeAsync().Wait();
        //    ;

        //    var anotherClient = CreateNewClient();
        //    {
        //        EnsureChildrenExists(anotherClient, rootNode, children);

        //        foreach (var node in nodes)
        //        {
        //            DeleteNode(node, anotherClient);
        //        }
        //    }
        //    anotherClient.closeAsync().Wait();
        //}

        //[TestCase("/getChildrenWithStatEphemeral/child1", "/getChildrenWithStatEphemeral/child2", "/getChildrenWithStatEphemeral/child3")]
        //public void GetChildrenWithStat_should_return_all_children_with_correct_Stat(params string[] nodes)
        //{
        //    var rootNode = "/" + nodes.First().Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries).First();
        //    var children = nodes.Select(x => x.Replace(rootNode + "/", string.Empty)).ToArray();

        //    var client = CreateNewClient();
        //    {
        //        foreach (var node in nodes)
        //        {
        //            CreateNode(node, org.apache.zookeeper.CreateMode.EPHEMERAL, client);
        //        }

        //        EnsureChildrenExistWithCorrectStat(client, rootNode, children, 0, 3);
        //    }
        //    client.closeAsync().Wait();
        //}

        //[TestCase("/getChildrenWithStatPersistent/child1", "/getChildrenWithStatPersistent/child2", "/getChildrenWithStatPersistent/child3")]
        //public void GetChildrenWithStat_should_return_all_children_with_correct_Stat_for_persistent_node(params string[] nodes)
        //{
        //    var rootNode = "/" + nodes.First().Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries).First();
        //    var children = nodes.Select(x => x.Replace(rootNode + "/", string.Empty)).ToArray();

        //    var client = CreateNewClient();
        //    {
        //        foreach (var node in nodes)
        //        {
        //            CreateNode(node, org.apache.zookeeper.CreateMode.PERSISTENT, client);
        //        }

        //        EnsureChildrenExistWithCorrectStat(client, rootNode, children, 0, 3);
        //    }
        //    client.closeAsync().Wait();

        //    var anotherClient = CreateNewClient();
        //    {
        //        EnsureChildrenExistWithCorrectStat(anotherClient, rootNode, children, 0, 3);

        //        foreach (var node in nodes)
        //        {
        //            DeleteNode(node, anotherClient);
        //        }
        //    }
        //    anotherClient.closeAsync().Wait();
        //}

        ////[Test]
        //public void Client_should_correct_see_node_Stat()
        //{
        //    const string rootNode = "/statChecking";
        //    const string childNode = "/statChecking/child1";

        //    var client = CreateNewClient();
        //    {
        //        CreateNode(rootNode, org.apache.zookeeper.CreateMode.PERSISTENT, client);

        //        var currentVersion = 0;
        //        var currentCVersion = 0;
        //        CheckVersions(client, rootNode, currentVersion, currentCVersion);

        //        for (var i = 0; i < 3; i++)
        //        {
        //            CreateNode(childNode, org.apache.zookeeper.CreateMode.PERSISTENT, client);
        //            currentCVersion++;

        //            CheckVersions(client, rootNode, currentVersion, currentCVersion);

        //            SetData(rootNode, "some data", client);
        //            currentVersion++;

        //            CheckVersions(client, rootNode, currentVersion, currentCVersion);

        //            DeleteNode(childNode, client);
        //            currentCVersion++;

        //            CheckVersions(client, rootNode, currentVersion, currentCVersion);
        //        }

        //        DeleteNode(rootNode, client);
        //    }
        //    client.closeAsync().Wait();
        //}
    }
}