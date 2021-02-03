using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;

namespace Vostok.ZooKeeper.Client.Tests
{
    [TestFixture]
    internal class NodeWatcher_Tests : TestsBase
    {
        private ZooKeeperClient client;

        [SetUp]
        public new void SetUp()
        {
            client = GetClient();

            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    client.Delete(new DeleteRequest("/watch") {DeleteChildrenIfNeeded = true}).EnsureSuccess();
                    client.Create(new CreateRequest("/watch/a/b/c", CreateMode.Persistent)).EnsureSuccess();
                    client.Create(new CreateRequest("/watch/a/b/d", CreateMode.Persistent)).EnsureSuccess();
                    client.Create(new CreateRequest("/watch/a/e", CreateMode.Persistent)).EnsureSuccess();
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to SetUp initial tree.");
                }
            }
        }

        [TearDown]
        public void TearDown()
        {
            client.Dispose();
        }

        [Test]
        public void Exists_should_add_watch_triggered_by_Created()
        {
            var path = "/watch/new";
            var watcher = new TestWatcher();
            client.Exists(new ExistsRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.Create(new CreateRequest(path, CreateMode.Persistent)).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(NodeChangedEventType.Created, path);
        }

        [Test]
        public void Exists_should_add_watch_triggered_by_DataChanged()
        {
            var path = "/watch/a";
            var watcher = new TestWatcher();
            client.Exists(new ExistsRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.SetData(new SetDataRequest(path, new byte[] {1, 2, 3})).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(NodeChangedEventType.DataChanged, path);
        }

        [Test]
        public void Exists_should_add_watch_triggered_by_Deleted()
        {
            var path = "/watch/a/e";
            var watcher = new TestWatcher();
            client.Exists(new ExistsRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.Delete(new DeleteRequest(path)).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(NodeChangedEventType.Deleted, path);
        }

        [Test]
        public void Exists_should_add_watch_not_triggered_by_ChildrenChanged()
        {
            var path = "/watch/a/b";
            var watcher = new TestWatcher();
            client.Exists(new ExistsRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.Create(new CreateRequest(path + "/f", CreateMode.Persistent)).EnsureSuccess();
            watcher.ShouldNotBeTriggered();
        }

        [Test]
        public void Exists_should_not_trigger_by_duplicated_events()
        {
            var path = "/watch/a/e";
            var watcher = new TestWatcher();
            for (var times = 0; times < 5; times++)
                client.Exists(new ExistsRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.Delete(new DeleteRequest(path)).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(NodeChangedEventType.Deleted, path);
        }

        [Test]
        public void Exists_should_be_trigger_by_duplicated_events_without_cache()
        {
            var path = "/watch/a/e";
            var watcher = new TestWatcher();
            for (var times = 0; times < 5; times++)
                client.Exists(new ExistsRequest(path) {Watcher = watcher, IgnoreWatchersCache = true}).EnsureSuccess();
            client.Delete(new DeleteRequest(path)).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(Enumerable.Repeat((NodeChangedEventType.Deleted, path), 5).ToArray());
        }

        [Test]
        public void Exists_should_trigger_by_duplicated_events_without_reattach()
        {
            var path = "/watch/new";
            var watcher = new TestWatcher();

            client.Exists(new ExistsRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.Create(new CreateRequest(path, CreateMode.Persistent)).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(NodeChangedEventType.Created, path);

            client.Delete(new DeleteRequest(path)).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(NodeChangedEventType.Created, path);
        }

        [Test]
        public void Exists_should_trigger_by_duplicated_events_after_reattach()
        {
            var path = "/watch/new";
            var watcher = new TestWatcher();

            client.Exists(new ExistsRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.Create(new CreateRequest(path, CreateMode.Persistent)).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(NodeChangedEventType.Created, path);

            client.Exists(new ExistsRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.Delete(new DeleteRequest(path)).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(
                (NodeChangedEventType.Created, path),
                (NodeChangedEventType.Deleted, path));
        }

        [Test]
        public void GetData_should_not_add_watch_triggered_by_Created()
        {
            var path = "/watch/new";
            var watcher = new TestWatcher();
            client.GetData(new GetDataRequest(path) {Watcher = watcher}).Status.Should().Be(ZooKeeperStatus.NodeNotFound);
            client.Create(new CreateRequest(path, CreateMode.Persistent)).EnsureSuccess();
            watcher.ShouldNotBeTriggered();
        }

        [Test]
        public void GetData_should_add_watch_triggered_by_DataChanged()
        {
            var path = "/watch/a";
            var watcher = new TestWatcher();
            client.GetData(new GetDataRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.SetData(new SetDataRequest(path, new byte[] {1, 2, 3})).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(NodeChangedEventType.DataChanged, path);
        }

        [Test]
        public void GetData_should_add_watch_triggered_by_Deleted()
        {
            var path = "/watch/a/e";
            var watcher = new TestWatcher();
            client.GetData(new GetDataRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.Delete(new DeleteRequest(path)).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(NodeChangedEventType.Deleted, path);
        }

        [Test]
        public void GetData_should_add_watch_not_triggered_by_ChildrenChanged()
        {
            var path = "/watch/a/b";
            var watcher = new TestWatcher();
            client.GetData(new GetDataRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.Create(new CreateRequest(path + "/f", CreateMode.Persistent)).EnsureSuccess();
            watcher.ShouldNotBeTriggered();
        }

        [Test]
        public void GetData_should_not_trigger_by_duplicated_events()
        {
            var path = "/watch/a/e";
            var watcher = new TestWatcher();
            for (var times = 0; times < 5; times++)
                client.GetData(new GetDataRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.Delete(new DeleteRequest(path)).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(NodeChangedEventType.Deleted, path);
        }

        [Test]
        public void GetData_should_trigger_by_duplicated_events_without_reattach()
        {
            var path = "/watch/a/b";
            var watcher = new TestWatcher();

            client.GetData(new GetDataRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.SetData(new SetDataRequest(path, new byte[] {1, 2, 3})).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(NodeChangedEventType.DataChanged, path);

            client.SetData(new SetDataRequest(path, new byte[] {1, 2, 4})).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(NodeChangedEventType.DataChanged, path);
        }

        [Test]
        public void GetData_should_trigger_by_duplicated_events_after_reattach()
        {
            var path = "/watch/a/b";
            var watcher = new TestWatcher();

            client.GetData(new GetDataRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.SetData(new SetDataRequest(path, new byte[] {1, 2, 3})).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(NodeChangedEventType.DataChanged, path);

            client.GetData(new GetDataRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.SetData(new SetDataRequest(path, new byte[] {1, 2, 4})).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(
                (NodeChangedEventType.DataChanged, path),
                (NodeChangedEventType.DataChanged, path));
        }

        [Test]
        public void GetChildren_should_not_add_watch_triggered_by_Created()
        {
            var path = "/watch/new";
            var watcher = new TestWatcher();
            client.GetChildren(new GetChildrenRequest(path) {Watcher = watcher}).Status.Should().Be(ZooKeeperStatus.NodeNotFound);
            client.Create(new CreateRequest(path, CreateMode.Persistent)).EnsureSuccess();
            watcher.ShouldNotBeTriggered();
        }

        [Test]
        public void GetChildren_should_add_watch_not_triggered_by_DataChanged()
        {
            var path = "/watch/a/b";
            var watcher = new TestWatcher();
            client.GetChildren(new GetChildrenRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.SetData(new SetDataRequest(path, new byte[] {1, 2, 3})).EnsureSuccess();
            watcher.ShouldNotBeTriggered();
        }

        [Test]
        public void GetChildren_should_add_watch_triggered_by_Deleted()
        {
            var path = "/watch/a/e";
            var watcher = new TestWatcher();
            client.GetChildren(new GetChildrenRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.Delete(new DeleteRequest(path)).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(NodeChangedEventType.Deleted, path);
        }

        [Test]
        public void GetChildren_should_add_watch_triggered_by_ChildrenChanged_create_child()
        {
            var path = "/watch/a/b";
            var watcher = new TestWatcher();
            client.GetChildren(new GetChildrenRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.Create(new CreateRequest(path + "/f", CreateMode.Persistent)).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(NodeChangedEventType.ChildrenChanged, path);
        }

        [Test]
        public void GetChildren_should_add_watch_triggered_by_ChildrenChanged_delete_child()
        {
            var path = "/watch/a/b";
            var watcher = new TestWatcher();
            client.GetChildren(new GetChildrenRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.Delete(new DeleteRequest(path + "/c")).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(NodeChangedEventType.ChildrenChanged, path);
        }

        [Test]
        public void GetChildren_should_add_watch_not_triggered_by_ChildrenChanged_delete_nested_child()
        {
            var path = "/watch/a";
            var watcher = new TestWatcher();
            client.GetChildren(new GetChildrenRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.Delete(new DeleteRequest(path + "/b/c")).EnsureSuccess();
            watcher.ShouldNotBeTriggered();
        }

        [Test]
        public void GetChildren_should_add_watch_not_triggered_by_ChildrenChanged_set_data_child()
        {
            var path = "/watch/a/b";
            var watcher = new TestWatcher();
            client.GetChildren(new GetChildrenRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.SetData(new SetDataRequest(path + "/c", new byte[] {1, 2, 3})).EnsureSuccess();
            watcher.ShouldNotBeTriggered();
        }

        [Test]
        public void GetChildren_should_not_trigger_by_duplicated_events()
        {
            var path = "/watch/a/b";
            var watcher = new TestWatcher();
            for (var times = 0; times < 5; times++)
                client.GetChildren(new GetChildrenRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.Delete(new DeleteRequest(path + "/c")).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(NodeChangedEventType.ChildrenChanged, path);
        }

        [Test]
        public void GetChildren_should_trigger_by_duplicated_events_without_reattach()
        {
            var path = "/watch/a/b";
            var watcher = new TestWatcher();

            client.GetChildren(new GetChildrenRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.Create(new CreateRequest(path + "/f", CreateMode.Persistent)).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(NodeChangedEventType.ChildrenChanged, path);

            client.Create(new CreateRequest(path + "/g", CreateMode.Persistent)).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(NodeChangedEventType.ChildrenChanged, path);
        }

        [Test]
        public void GetChildren_should_trigger_by_duplicated_events_after_reattach()
        {
            var path = "/watch/a/b";
            var watcher = new TestWatcher();

            client.GetChildren(new GetChildrenRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.Create(new CreateRequest(path + "/f", CreateMode.Persistent)).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(NodeChangedEventType.ChildrenChanged, path);

            client.GetChildren(new GetChildrenRequest(path) {Watcher = watcher}).EnsureSuccess();
            client.Create(new CreateRequest(path + "/g", CreateMode.Persistent)).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(
                (NodeChangedEventType.ChildrenChanged, path),
                (NodeChangedEventType.ChildrenChanged, path));
        }

        [Test]
        public void Should_not_be_triggered_by_dispose()
        {
            var path = "/watch/a";
            var watcher = new TestWatcher();
            var localClient = GetClient();
            localClient.GetData(new GetDataRequest(path) {Watcher = watcher}).EnsureSuccess();
            localClient.Dispose();
            watcher.ShouldNotBeTriggered();
        }

        [TestCase(CreateMode.Persistent)]
        [TestCase(CreateMode.Ephemeral)]
        public void Should_not_be_triggered_on_client_disconnect(CreateMode createMode)
        {
            var path = "/watch/new";
            var watcher = new TestWatcher();
            var localClient = GetClient();
            localClient.Create(new CreateRequest(path, createMode)).EnsureSuccess();
            localClient.Exists(new ExistsRequest(path) {Watcher = watcher});

            Ensemble.Stop();
            WaitForDisconnectedState(localClient);
            Ensemble.Start();

            localClient.Delete(new DeleteRequest(path)).EnsureSuccess();
            watcher.ShouldBeTriggeredBy(
                (NodeChangedEventType.Deleted, path));
        }

        [TestCase(CreateMode.Persistent)]
        [TestCase(CreateMode.Ephemeral)]
        public async Task Should_not_be_triggered_on_client_session_expire(CreateMode createMode)
        {
            var path = "/watch/new";
            var watcher = new TestWatcher();
            var localClient = GetClient();
            localClient.Create(new CreateRequest(path, createMode)).EnsureSuccess();
            localClient.Exists(new ExistsRequest(path) {Watcher = watcher});

            await KillSession(localClient, Ensemble.ConnectionString);

            var result = localClient.Delete(new DeleteRequest(path));
            result.Status.Should().Be(createMode.IsEphemeral() ? ZooKeeperStatus.NodeNotFound : ZooKeeperStatus.Ok);

            watcher.ShouldNotBeTriggered();
        }

        private class TestWatcher : INodeWatcher
        {
            public List<(NodeChangedEventType, string)> Values = new List<(NodeChangedEventType, string)>();
            private TimeSpan timeout = 1.Seconds();
            private object sync = new object();

            public Task ProcessEvent(NodeChangedEventType type, string path)
            {
                lock (sync)
                {
                    Values.Add((type, path));
                }

                return Task.CompletedTask;
            }

            public void ShouldBeTriggeredBy(params (NodeChangedEventType, string)[] events)
            {
                Thread.Sleep(timeout);
                lock (sync)
                {
                    Values.Should().BeEquivalentTo(events, options => options.WithStrictOrdering());
                }
            }

            public void ShouldBeTriggeredBy(NodeChangedEventType type, string path)
            {
                ShouldBeTriggeredBy((type, path));
            }

            public void ShouldNotBeTriggered()
            {
                Thread.Sleep(timeout);
                lock (sync)
                {
                    Values.Should().BeEmpty();
                }
            }
        }
    }
}