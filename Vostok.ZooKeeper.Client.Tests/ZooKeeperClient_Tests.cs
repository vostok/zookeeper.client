using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.LocalEnsemble;

namespace Vostok.ZooKeeper.Client.Tests
{
    [TestFixture]
    internal class ZooKeeperClient_Tests
    {
        private readonly ILog log = new SynchronousConsoleLog();
        private ZooKeeperEnsemble ensemble;
        
        [SetUp]
        public void SetUp()
        {
            ensemble = ZooKeeperEnsemble.DeployNew(1, log);
        }

        [TearDown]
        public void TearDown()
        {
            ensemble.Dispose();
        }

        [TestCase("/root")]
        //[TestCase("/a/b/c1")]
        //[TestCase("/a/b/c2")]
        public async Task Create_persistent_node_should_work_with_different_pathes(string path)
        {
            using (var client = Client())
            {
                await client.CreateAsync(new CreateZooKeeperRequest(path, null, CreateMode.Persistent));
                var node = await client.GetDataAsync(new GetDataZooKeeperRequest(path));

                node.Path.Should().Be(path);
                node.Stat.Version.Should().Be(0);
            }
        }



        //[Test]
        //public void IsStarted_should_be_false_by_default()
        //{
        //    var client = new ZooKeeperClient(ensemble.ConnectionString, 5.Seconds(), new SilentLog());

        //    client.IsStarted.Should().BeFalse();
        //    client.IsConnected.Should().BeFalse();
        //}

        //[Test]
        //public void Start_should_start_client()
        //{
        //    var client = new ZooKeeperClient(ensemble.ConnectionString, 5.Seconds(), new SilentLog());

        //    client.Start();
        //    client.IsStarted.Should().BeTrue();
        //    Action checkIsConnected = () => client.IsConnected.Should().BeTrue();
        //    checkIsConnected.ShouldPassIn(5.Seconds());
        //}

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

        private ZooKeeperClient Client()
        {
            return new ZooKeeperClient(log, new ZooKeeperClientSetup(() => ensemble.ConnectionString) {Timeout = 15.Seconds()});
        }
    }
}