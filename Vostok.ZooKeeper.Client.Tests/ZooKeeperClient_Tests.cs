﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Helpers;

namespace Vostok.ZooKeeper.Client.Tests
{
    [TestFixture]
    internal class ZooKeeperClient_Tests : TestsBase
    {
        private ZooKeeperClient client;

        [SetUp]
        public new void SetUp()
        {
            client = GetClient();
        }

        [TearDown]
        public void TearDown()
        {
            client.Dispose();
        }

        [Test]
        public async Task Should_return_NotConnected()
        {
            Ensemble.Stop();
            WaitForDisconnectedState(client);
            var result = await client.CreateAsync("/return_connection_loss", CreateMode.Persistent);

            result.Status.Should().Be(ZooKeeperStatus.NotConnected);
        }

        [Test]
        public void SessionTimeout_NotConnected()
        {
            Ensemble.Stop();
            WaitForDisconnectedState(client);
            client.SessionTimeout.Should().Be(DefaultTimeout);
        }

        [Test]
        public async Task SessionTimeout_Connected()
        {
            //note hack: internal zk client saves only [int32 milliseconds]. use this to check actual SessionTimeout got from zk client not settings
            using (var newClient = GetClient(TimeSpan.FromMilliseconds(10000 + 0.4)))
            {
                await client.CreateAsync("/reconnect", CreateMode.Persistent);
                newClient.SessionTimeout.Should().Be(TimeSpan.FromMilliseconds(10000));
            }
        }


        [Test]
        public async Task Should_reconnect()
        {
            Ensemble.Stop();
            WaitForDisconnectedState(client);
            Ensemble.Start();

            var result = await client.CreateAsync("/reconnect", CreateMode.Persistent);
            result.EnsureSuccess();
        }

        [Test]
        public async Task SessionId_SessionPassword_ConnectionState_should_be_filled_after_connect()
        {
            (await client.ExistsAsync("/path")).EnsureSuccess();

            client.ConnectionState.Should().Be(ConnectionState.Connected);
            client.SessionId.Should().NotBe(0);
            client.SessionPassword.Should().NotBeNullOrEmpty();
        }

        [TestCase(CreateMode.Persistent)]
        [TestCase(CreateMode.PersistentSequential)]
        [TestCase(CreateMode.Ephemeral)]
        [TestCase(CreateMode.EphemeralSequential)]
        public async Task Create_should_create_node_in_different_modes(CreateMode createMode)
        {
            var path = $"/create_node_{createMode}";

            var createResult = await client.CreateAsync(path, createMode);
            createResult.EnsureSuccess();

            if (!createMode.IsSequential())
                createResult.NewPath.Should().Be(path);

            await VerifyNodeCreated(client, createResult.NewPath);

            await KillSession(client, Ensemble.ConnectionString);

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
                $"/create_nested_{mode}/a/b/c_second",
                $"/create_nested_{mode}/a/b/c_second/c_child"
            };

            foreach (var path in paths)
            {
                var createResult = await client.CreateAsync(path, CreateMode.Persistent);
                createResult.EnsureSuccess();
                (await client.ExistsAsync(path)).Exists.Should().BeTrue();
            }
        }

        [Test]
        [Combinatorial]
        public async Task Create_should_create_sequential_node(
            [Values("nested_sequential/aaa/bb/c")] string path,
            [Values(CreateMode.PersistentSequential, CreateMode.EphemeralSequential)]
            CreateMode createMode)
        {
            path = $"/{createMode}_{path}";

            for (var i = 0; i < 3; i++)
            {
                var createResult = await client.CreateAsync(path, createMode);
                createResult.EnsureSuccess();

                await VerifyNodeCreated(client, createResult.NewPath);

                createResult.NewPath.Should().Be($"{path}{i:D10}");
            }
        }

        [TestCase(CreateMode.PersistentSequential)]
        [TestCase(CreateMode.EphemeralSequential)]
        public async Task Create_should_create_sequential_node_without_name(CreateMode createMode)
        {
            var path = $"/create_sequential_node_{createMode}/a/";

            for (var i = 0; i < 3; i++)
            {
                var createResult = await client.CreateAsync(path, createMode);
                createResult.EnsureSuccess();

                await VerifyNodeCreated(client, createResult.NewPath);

                createResult.NewPath.Should().Be($"{path}{i:D10}");
            }
        }

        [Test]
        [SuppressMessage("ReSharper", "PossiblyMistakenUseOfInterpolatedStringInsert")]
        public async Task Create_should_create_sequential_node_with_shared_parent_counter()
        {
            // When creating a node you can also request that ZooKeeper append a monotonically increasing counter to the end of path.
            // This counter is unique to the parent node.

            var createResult = await client.CreateAsync("/shared_sequential/a", CreateMode.PersistentSequential);
            createResult.NewPath.Should().Be($"/shared_sequential/a{0:D10}");

            createResult = await client.CreateAsync("/shared_sequential/b", CreateMode.PersistentSequential);
            createResult.NewPath.Should().Be($"/shared_sequential/b{1:D10}");
        }

        [Test]
        [Combinatorial]
        public async Task Create_should_save_data(
            [Values(0, 1, 10, 1024, 1024 * 10, 1024 * 100, NodeHelper.DataSizeLimit)]
            int size,
            [Values(CreateMode.Persistent, CreateMode.Ephemeral)]
            CreateMode createMode)
        {
            var data = Enumerable.Range(0, size).Select(i => (byte)(i % 256)).ToArray();
            var createResult = await client.CreateAsync($"/create_with_data_{size}_{createMode}", createMode, data);
            createResult.EnsureSuccess();
        }

        [Test]
        public async Task Create_should_return_BadArguments_for_big_data()
        {
            var createResult = await client.CreateAsync("/big_data", CreateMode.Persistent, new byte[NodeHelper.DataSizeLimit + 1]);
            createResult.Status.Should().Be(ZooKeeperStatus.BadArguments);
            createResult.Exception.Should().BeOfType<ArgumentException>();
        }

        [TestCase("without_slash_at_the_beggingig")]
        [TestCase("/with_extra_slash_at_the_ending/")]
        public async Task Create_should_return_BadArguments_for_bad_path(string path)
        {
            var createResult = await client.CreateAsync(path, CreateMode.Persistent);
            createResult.Status.Should().Be(ZooKeeperStatus.BadArguments);
            createResult.Exception.Should().BeOfType<ArgumentException>();
        }

        [Test]
        public async Task Create_should_return_NodeAlreadyExists()
        {
            var createResult = await client.CreateAsync("/create_same_node_twice", CreateMode.Persistent);
            createResult.EnsureSuccess();

            createResult = await client.CreateAsync("/create_same_node_twice", CreateMode.Persistent);
            createResult.Status.Should().Be(ZooKeeperStatus.NodeAlreadyExists);
        }

        [Test]
        public async Task Create_should_return_NodeAlreadyExists_for_nested_node()
        {
            var path = "/create_same_node_twice_nested/a/b/c/d";
            var createResult = await client.CreateAsync(path, CreateMode.Persistent);
            createResult.EnsureSuccess();

            createResult = await client.CreateAsync(path, CreateMode.Persistent);
            createResult.Status.Should().Be(ZooKeeperStatus.NodeAlreadyExists);
        }

        [Test]
        public async Task Create_should_return_NodeNotFound_for_nested_node()
        {
            var path = "/create_not_found/for_parent";
            var createResult = await client.CreateAsync(new CreateRequest(path, CreateMode.Persistent) {CreateParentsIfNeeded = false});

            createResult.Status.Should().Be(ZooKeeperStatus.NodeNotFound);
        }

        [Test]
        public async Task Create_should_not_return_NodeNotFound_for_root_node()
        {
            var path = "/create_not_found_root";
            var createResult = await client.CreateAsync(new CreateRequest(path, CreateMode.Persistent) {CreateParentsIfNeeded = false});
            createResult.EnsureSuccess();
        }

        [TestCase(CreateMode.Ephemeral)]
        [TestCase(CreateMode.Persistent)]
        public async Task Create_should_return_ChildrenForEphemeralAreNotAllowed(CreateMode childCreateMode)
        {
            var createResult = await client.CreateAsync($"/ephemeral_parent_{childCreateMode}", CreateMode.Ephemeral);
            createResult.EnsureSuccess();

            createResult = await client.CreateAsync($"/ephemeral_parent_{childCreateMode}/child", childCreateMode);
            createResult.Status.Should().Be(ZooKeeperStatus.ChildrenForEphemeralAreNotAllowed);
        }

        [Test]
        [Combinatorial]
        public async Task GetData_should_return_saved_data(
            [Values(0, 1, 10, 1024, 1024 * 10, 1024 * 100, NodeHelper.DataSizeLimit)]
            int size,
            [Values(CreateMode.Persistent, CreateMode.Ephemeral)]
            CreateMode createMode)
        {
            var data = Enumerable.Range(0, size).Select(i => (byte)(i % 256)).ToArray();
            var path = $"/get_saved_data_{size}_{createMode}";

            var createResult = await client.CreateAsync(path, createMode, data);
            createResult.EnsureSuccess();

            var result = await client.GetDataAsync(path);
            result.Data.Should().BeEquivalentTo(data, options => options.WithStrictOrdering());
        }

        [Test]
        public async Task GetData_should_return_null_data()
        {
            var path = "/get_saved_null_data";

            var createResult = await client.CreateAsync(path, CreateMode.Persistent);
            createResult.EnsureSuccess();

            var result = await client.GetDataAsync(path);
            result.Data.Should().BeNull();
        }

        [Test]
        public async Task GetData_should_return_empty_data()
        {
            var path = "/get_saved_empty_data";

            var createResult = await client.CreateAsync(path, CreateMode.Persistent, new byte[0]);
            createResult.EnsureSuccess();

            var result = await client.GetDataAsync(path);
            result.Data.Should().BeEmpty();
        }

        [Test]
        public async Task GetData_should_return_modified_data_and_stat()
        {
            var path = "/get_modified_data";
            var bytes1 = new byte[] {0, 1, 2, 3};
            var bytes2 = new byte[] {3, 2, 1};

            var createResult = await client.CreateAsync(path, CreateMode.Persistent, bytes1);
            createResult.EnsureSuccess();

            var result = await client.GetDataAsync(path);
            result.Data.Should().BeEquivalentTo(bytes1, options => options.WithStrictOrdering());
            result.Stat.DataLength.Should().Be(4);
            result.Stat.Version.Should().Be(0);

            await client.SetDataAsync(path, bytes2);
            result = await client.GetDataAsync(path);
            result.Data.Should().BeEquivalentTo(bytes2, options => options.WithStrictOrdering());
            result.Stat.DataLength.Should().Be(3);
            result.Stat.Version.Should().Be(1);
        }

        [Test]
        public async Task GetData_should_return_NodeNotFound()
        {
            var result = await client.GetDataAsync("/get_unexisting_node");

            result.Status.Should().Be(ZooKeeperStatus.NodeNotFound);
        }

        [TestCase("without_slash_at_the_beggingig")]
        [TestCase("/with_extra_slash_at_the_ending/")]
        public async Task GetData_should_return_BadArguments_for_bad_path(string path)
        {
            var createResult = await client.GetDataAsync(path);
            createResult.Status.Should().Be(ZooKeeperStatus.BadArguments);
            createResult.Exception.Should().BeOfType<ArgumentException>();
        }

        [Test]
        public async Task SetData_should_modify_current_version()
        {
            var path = "/set_data_with_current_version";

            (await client.CreateAsync(path, CreateMode.Persistent)).EnsureSuccess();

            for (var version = 0; version < 3; version++)
            {
                var setResult = await client.SetDataAsync(new SetDataRequest(path, new[] {(byte)(version + 1)}) {Version = version});
                setResult.EnsureSuccess();

                setResult.Stat.Version.Should().Be(version + 1);
            }

            var result = await client.GetDataAsync(path);
            result.Data.Should().BeEquivalentTo(new[] {(byte)3}, options => options.WithStrictOrdering());
            result.Stat.Version.Should().Be(3);
        }

        [Test]
        public async Task SetData_should_modify_any_version()
        {
            var path = "/set_data_with_any_version";

            (await client.CreateAsync(new CreateRequest(path, CreateMode.Persistent))).EnsureSuccess();

            var result = await client.SetDataAsync(new SetDataRequest(path, null) {Version = -1});
            result.EnsureSuccess();
            result.Stat.Version.Should().Be(1);
        }

        [Test]
        public async Task SetData_should_return_VersionsMismatch()
        {
            var path = "/set_data_vesions_mismatch";
            (await client.CreateAsync(path, CreateMode.Persistent)).EnsureSuccess();

            var result = await client.SetDataAsync(new SetDataRequest(path, null) {Version = 42});

            result.Status.Should().Be(ZooKeeperStatus.VersionsMismatch);
        }

        [Test]
        public async Task SetData_should_return_NodeNotFound()
        {
            var result = await client.SetDataAsync("/set_data_unexisting_node", null);

            result.Status.Should().Be(ZooKeeperStatus.NodeNotFound);
        }

        [Test]
        public async Task Exists_should_return_false()
        {
            var result = await client.ExistsAsync("/exists_false");

            result.EnsureSuccess();
            result.Exists.Should().BeFalse();
            result.Stat.Should().Be(null);
        }

        [Test]
        public async Task Exists_should_return_true()
        {
            var path = "/exists_true";
            (await client.CreateAsync(path, CreateMode.Persistent)).EnsureSuccess();

            var result = await client.ExistsAsync(path);

            result.EnsureSuccess();
            result.Exists.Should().BeTrue();
            // ReSharper disable once PossibleNullReferenceException
            result.Stat.Version.Should().Be(0);
        }

        [TestCase("without_slash_at_the_beggingig")]
        [TestCase("/with_extra_slash_at_the_ending/")]
        public async Task Exists_should_return_BadArguments_for_bad_path(string path)
        {
            var createResult = await client.ExistsAsync(path);
            createResult.Status.Should().Be(ZooKeeperStatus.BadArguments);
            createResult.Exception.Should().BeOfType<ArgumentException>();
        }

        [Test]
        public async Task Delete_should_delete_leaf()
        {
            var path = "/delete_leaf";

            (await client.CreateAsync(path, CreateMode.Persistent)).EnsureSuccess();

            await VerifyNodeCreated(client, path);

            (await client.DeleteAsync(path)).EnsureSuccess();

            await VerifyNodeDeleted(client, path);
        }

        [Test]
        public async Task Delete_should_delete_with_children()
        {
            var paths = new List<string> {"/root/a/b/c", "/root/a/b/d", "/root/a/e", "/root/b"};

            foreach (var path in paths)
            {
                (await client.CreateAsync(path, CreateMode.Persistent)).EnsureSuccess();
            }

            (await client.DeleteAsync("/root/a")).EnsureSuccess();

            await VerifyNodeDeleted(client, "/root/a");
            foreach (var path in paths.Where(p => p.StartsWith("/root/a")))
            {
                await VerifyNodeDeleted(client, path);
            }

            await VerifyNodeCreated(client, "/root/b");
        }

        [Test]
        public async Task Delete_should_return_NodeNotFound()
        {
            var result = await client.DeleteAsync("/delete_unexisting_node");

            result.Status.Should().Be(ZooKeeperStatus.NodeNotFound);
            result.EnsureSuccess();
        }

        [Test]
        public async Task Delete_should_return_NodeHasChildren()
        {
            var path = "/delete_nested/a/b";

            (await client.CreateAsync(path, CreateMode.Persistent)).EnsureSuccess();

            var result = await client.DeleteAsync(new DeleteRequest("/delete_nested/a") {DeleteChildrenIfNeeded = false});

            result.Status.Should().Be(ZooKeeperStatus.NodeHasChildren);

            await VerifyNodeCreated(client, path);
        }

        [Test]
        public async Task Delete_should_return_VersionsMismatch()
        {
            var path = "/delete_version_mismatch";

            (await client.CreateAsync(path, CreateMode.Persistent)).EnsureSuccess();

            var result = await client.DeleteAsync(new DeleteRequest(path) {Version = 42});

            result.Status.Should().Be(ZooKeeperStatus.VersionsMismatch);

            await VerifyNodeCreated(client, path);
        }

        [Test]
        public async Task GetChildren_should_return_NodeNotFound()
        {
            var result = await client.GetChildrenAsync("/get_children_unexisting_node");

            result.Status.Should().Be(ZooKeeperStatus.NodeNotFound);
        }

        [Test]
        public async Task GetChildren_should_return_names_with_parent_stat()
        {
            var paths = new List<string> {"/get_children/a/b/c", "/get_children/a/b/d", "/get_children/a/e", "/get_children/b"};

            foreach (var path in paths)
            {
                (await client.CreateAsync(path, CreateMode.Persistent)).EnsureSuccess();
            }

            var result = await client.GetChildrenAsync("/get_children/a");
            result.ChildrenNames.Should().BeEquivalentTo("b", "e");
            result.Stat.ChildrenVersion.Should().Be(2);
        }

        [Test]
        public async Task Dispose_should_delete_ephemeral_nodes()
        {
            var path = "/dispose_ephemeral";

            var disposedClient = GetClient();
            (await disposedClient.CreateAsync(path, CreateMode.Ephemeral)).EnsureSuccess();
            disposedClient.Dispose();

            await VerifyNodeDeleted(GetClient(), path);
        }

        [Test]
        public void Dispose_should_stop_client()
        {
            var disposedClient = GetClient();
            disposedClient.Dispose();

            disposedClient.ConnectionState.Should().Be(ConnectionState.Died);
            disposedClient.SessionId.Should().Be(0);
            disposedClient.SessionPassword.Should().BeNull();

            var result = disposedClient.Exists("node");
            result.IsSuccessful.Should().BeFalse();
            result.Status.Should().Be(ZooKeeperStatus.Died);
        }

        [Test]
        public async Task Dispose_immediately_should_not_deadlock()
        {
            using (var c = GetClient())
            {
                await c.ExistsAsync("/path");
            }

            using (var c = GetClient())
            {
                await c.ExistsAsync("path");
            }

            using (var c = GetClient())
            {
            }
        }

        [Test]
        public async Task Dispose_should_not_execute_operations()
        {
            var disposedClient = GetClient();
            disposedClient.Dispose();

            var result = await disposedClient.ExistsAsync("/path");

            result.Status.Should().Be(ZooKeeperStatus.Died);
        }

        [Test]
        public async Task Should_works_with_multiple_clients()
        {
            var clients = Enumerable.Range(0, 3).Select(_ => GetClient()).ToList();

            var path = "/multiple_clients";
            await client.CreateAsync(path, CreateMode.Persistent);

            var times = 0;

            foreach (var c in clients)
            {
                await c.SetDataAsync(path, new[] {(byte)++times});
                await KillSession(c, Ensemble.ConnectionString);
                await c.SetDataAsync(path, new[] {(byte)++times});
            }

            Ensemble.Stop();
            Ensemble.Start();

            foreach (var c in clients)
            {
                var data = await c.GetDataAsync(path);
                data.Data.Should().BeEquivalentTo(new[] {(byte)times});
            }
        }

        private static async Task VerifyNodeCreated(ZooKeeperClient client, string path)
        {
            var node = await client.GetDataAsync(path);
            node.EnsureSuccess();

            node.Path.Should().Be(path);
            node.Stat.Version.Should().Be(0);
        }

        private static async Task VerifyNodeDeleted(ZooKeeperClient client, string path)
        {
            var node = await client.ExistsAsync(path);
            node.EnsureSuccess();

            node.Exists.Should().BeFalse();
        }
    }
}