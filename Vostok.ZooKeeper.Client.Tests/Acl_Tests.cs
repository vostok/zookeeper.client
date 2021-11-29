using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Authentication;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.LocalEnsemble;

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
                Acl.Digest(AclPermissions.All, "user", "password"),
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
                Acl.Digest(AclPermissions.All, "user", "password"),
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
                Acls = new List<Acl> { Acl.Digest(AclPermissions.All, login, password) }
            };
            var createResult = await client.CreateAsync(createRequest);
            createResult.EnsureSuccess();

            var authInfo = AuthenticationInfo.Digest(login, password);
            var testClient = GetClient(null);
            testClient.AddAuthenticationInfo(authInfo);

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
                Acls = new List<Acl> {Acl.Digest(AclPermissions.All, "user", "password")}
            };
            var createResult = await client.CreateAsync(createRequest);
            createResult.EnsureSuccess();

            var getResult = await client.GetDataAsync(path);
            getResult.IsSuccessful.Should().BeFalse();
            getResult.Status.Should().Be(ZooKeeperStatus.NoAuth);
        }

        [Test]
        [Platform("Win", Reason = IgnoreReason)]
        public async Task Should_reconnect_to_new_ensemble_with_provided_auth_info()
        {
            using(var ensemble1 = ZooKeeperEnsemble.DeployNew(10, 1, Log))
            {
                var connectionString = ensemble1.ConnectionString;
                var path = "/auth";
                var login = "testlogin";
                var password = "testpassword";

                var client = new ZooKeeperClient(new ZooKeeperClientSettings(() =>connectionString), Log);

                var createRequest = new CreateRequest(path, CreateMode.Persistent)
                {
                    Acls = new List<Acl> { Acl.Digest(AclPermissions.All, login, password) }
                };

                var createResult = await client.CreateAsync(createRequest);
                createResult.EnsureSuccess();

                var authInfo = AuthenticationInfo.Digest(login, password);
                var testClient = new ZooKeeperClient(new ZooKeeperClientSettings(() => connectionString), Log);
                testClient.AddAuthenticationInfo(authInfo);

                (await testClient.CreateAsync(new CreateRequest("/bla/bla", CreateMode.Persistent))).EnsureSuccess();
                ensemble1.Dispose();
                WaitForDisconnectedState(client);
                using(var ensemble2 = ZooKeeperEnsemble.DeployNew(11, 1, Log))
                {
                    ensemble2.ConnectionString.Should().NotBe(connectionString);
                    connectionString = ensemble2.ConnectionString;
                   
                    await client.CreateAsync(createRequest);

                    var res = (await testClient.GetDataAsync(path));
                    res.EnsureSuccess();
                }
            }
        }

        [Test]
        public async Task Should_add_auth_with_digest_scheme_by_default()
        {
            var path = "/test-auth";
            var login = "test-login";
            var password = "test-password";

            var createRequest = new CreateRequest(path, CreateMode.Persistent)
            {
                Acls = new List<Acl> { Acl.Digest(AclPermissions.All, login, password) }
            };
            var createResult = await client.CreateAsync(createRequest);
            createResult.EnsureSuccess();

            var testClient = GetClient(null);
            testClient.AddAuthenticationInfo(login, password);

            var getResult = await testClient.GetDataAsync(path);
            getResult.EnsureSuccess();
        }
    }
}