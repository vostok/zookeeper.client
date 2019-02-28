using System;
using System.Threading.Tasks;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ZooKeeper.Client.Operations
{
    internal class GetDataOperation : BaseOperation<GetDataRequest, GetDataResult>
    {
        public GetDataOperation(GetDataRequest request)
            : base(request)
        {
        }

        public override async Task<GetDataResult> Execute(org.apache.zookeeper.ZooKeeper client)
        {
            var result = await client.getDataAsync(Request.Path).ConfigureAwait(false);

            return new GetDataResult(ZooKeeperStatus.Ok, Request.Path, result.Data, result.Stat.FromZooKeeperStat());
        }

        public override GetDataResult CreateUnsuccessfulResult(ZooKeeperStatus status, Exception exception) => new GetDataResult(status, Request.Path, null, null) { Exception = exception };
    }
}