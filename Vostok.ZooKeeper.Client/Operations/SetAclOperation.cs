using System;
using System.Threading.Tasks;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;
using Vostok.ZooKeeper.Client.Helpers;

namespace Vostok.ZooKeeper.Client.Operations
{
    internal class SetAclOperation : BaseOperation<SetAclRequest, SetAclResult>
    {
        public SetAclOperation(SetAclRequest request)
            : base(request)
        {
        }

        public override async Task<SetAclResult> Execute(org.apache.zookeeper.ZooKeeper client)
        {
            var result = await client.setACLAsync(
                    Request.Path,
                    Request.Acls.ToInnerAcls(),
                    Request.AclVersion)
                .ConfigureAwait(false);

            return SetAclResult.Successful(Request.Path, result.ToNodeStat());
        }

        public override SetAclResult CreateUnsuccessfulResult(ZooKeeperStatus status, Exception exception) =>
            SetAclResult.Unsuccessful(status, Request.Path, exception);
    }
}