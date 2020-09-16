using System;
using System.Threading.Tasks;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;
using Vostok.ZooKeeper.Client.Helpers;

namespace Vostok.ZooKeeper.Client.Operations
{
    internal class GetAclOperation : BaseOperation<GetAclRequest, GetAclResult>
    {
        public GetAclOperation(GetAclRequest request)
            : base(request)
        {
        }

        public override async Task<GetAclResult> Execute(org.apache.zookeeper.ZooKeeper client)
        {
            var result = await client.getACLAsync(Request.Path).ConfigureAwait(false);

            return GetAclResult.Successful(Request.Path, result.Acls.ToAcls(), result.Stat.ToNodeStat());
        }

        public override GetAclResult CreateUnsuccessfulResult(ZooKeeperStatus status, Exception exception) =>
            GetAclResult.Unsuccessful(status, Request.Path, exception);
    }
}