using System;
using System.Threading.Tasks;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ZooKeeper.Client.Operations
{
    internal class SetDataOperation : BaseOperation<SetDataRequest, SetDataResult>
    {
        public SetDataOperation(SetDataRequest request)
            : base(request)
        {
        }

        public override async Task<SetDataResult> Execute(org.apache.zookeeper.ZooKeeper client)
        {
            var result = await client.setDataAsync(Request.Path, Request.Data, Request.Version).ConfigureAwait(false);

            return new SetDataResult(ZooKeeperStatus.Ok, Request.Path, result.FromZooKeeperStat());
        }

        public override SetDataResult CreateUnsuccessfulResult(ZooKeeperStatus status, Exception exception) => new SetDataResult(status, Request.Path, null) { Exception = exception };
    }
}