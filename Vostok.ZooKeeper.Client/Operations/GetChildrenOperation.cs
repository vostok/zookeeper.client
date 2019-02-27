using System;
using System.Threading.Tasks;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ZooKeeper.Client.Operations
{
    internal class GetChildrenOperation : BaseOperation<GetChildrenRequest, GetChildrenResult>
    {
        public GetChildrenOperation(GetChildrenRequest request)
            : base(request)
        {
        }

        public override async Task<GetChildrenResult> Execute(org.apache.zookeeper.ZooKeeper client)
        {
            var result = await client.getChildrenAsync(Request.Path);

            return new GetChildrenResult(ZooKeeperStatus.Ok, Request.Path, result.Children, result.Stat.FromZooKeeperStat());
        }

        public override GetChildrenResult CreateUnsuccessfulResult(ZooKeeperStatus status, Exception exception) => new GetChildrenResult(status, Request.Path, null, null) { Exception = exception };
    }
}