using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;

namespace Vostok.ZooKeeper.Client.Tests
{
    [TestFixture]
    internal class Acl_Tests : TestsBase
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
        public async Task Create_should_set_specified_acls()
        {
            var path = "/acl_node_create";
            var acls = new List<Acl>
            {
                Acl.Digest(Permissions.All, "user", "password"),
                Acl.ReadUnsafe
            };
            var createRequest = new CreateRequest(path, CreateMode.Persistent)
            {
                Acls = acls
            };
            var createResult = await client.CreateAsync(createRequest);
            createResult.EnsureSuccess();

            var aclResult = await client.GetAclAsync(path);
            aclResult.Acls.Should().BeEquivalentTo(acls);
        }

        [Test]
        public async Task SetAcl_should_set_specified_acls()
        {
            var path = "/acl_node_set";

            var createRequest = new CreateRequest(path, CreateMode.Persistent);
            var createResult = await client.CreateAsync(createRequest);
            createResult.EnsureSuccess();

            var acls = new List<Acl>
            {
                Acl.Digest(Permissions.All, "user", "password"),
                Acl.ReadUnsafe
            };
            var setResult = await client.SetAclAsync(path, acls);
            setResult.EnsureSuccess();

            var aclResult = await client.GetAclAsync(path);
            aclResult.Acls.Should().BeEquivalentTo(acls);
        }

        [Test]
        public async Task SetAcl_should_modify_any_version()
        {
            var path = "/set_acl_with_any_version";

            (await client.CreateAsync(new CreateRequest(path, CreateMode.Persistent))).EnsureSuccess();

            var acls = new List<Acl> {Acl.ReadUnsafe};
            var request = new SetAclRequest(path, acls) { AclVersion = -1};
            var result = await client.SetAclAsync(request);
            result.EnsureSuccess();
            result.Stat.AclVersion.Should().Be(1);
        }

        [Test]
        public async Task SetAcl_should_return_VersionsMismatch()
        {
            var path = "/set_acl_vesions_mismatch";
            (await client.CreateAsync(path, CreateMode.Persistent)).EnsureSuccess();

            var acls = new List<Acl> {Acl.ReadUnsafe};
            var request = new SetAclRequest(path, acls) { AclVersion = 42};
            var result = await client.SetAclAsync(request);

            result.Status.Should().Be(ZooKeeperStatus.VersionsMismatch);
        }

        [Test]
        public async Task Should_add_provided_authentication()
        {
            var path = "/auth";
            var login = "testlogin";
            var password = "testpassword";

            var createRequest = new CreateRequest(path, CreateMode.Persistent)
            {
                Acls = new List<Acl> { Acl.Digest(Permissions.All, login, password) }
            };
            var createResult = await client.CreateAsync(createRequest);
            createResult.EnsureSuccess();

            var authInfo = AuthenticationInfo.Digest(login, password);
            var testClient = GetClient(null, authInfo);

            var getResult = await testClient.GetDataAsync(path);
            getResult.EnsureSuccess();
        }

        [Test]
        public async Task Should_handle_InvalidAcl_error()
        {
            var path = "/forbidden_node";
            var createRequest = new CreateRequest(path, CreateMode.Persistent)
            {
                Acls = new List<Acl>()
            };
            var result = await client.CreateAsync(createRequest);
            result.IsSuccessful.Should().BeFalse();
            result.Status.Should().Be(ZooKeeperStatus.InvalidAcl);
        }

        [Test]
        public async Task Should_handle_NoAuth_error()
        {
            var path = "/noauth";
            var createRequest = new CreateRequest(path, CreateMode.Persistent)
            {
                Acls = new List<Acl> {Acl.Digest(Permissions.All, "user", "password")}
            };
            var createResult = await client.CreateAsync(createRequest);
            createResult.EnsureSuccess();

            var getResult = await client.GetDataAsync(path);
            getResult.IsSuccessful.Should().BeFalse();
            getResult.Status.Should().Be(ZooKeeperStatus.NoAuth);
        }
    }
}