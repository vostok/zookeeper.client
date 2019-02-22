using System;
using System.Threading.Tasks;
using org.apache.zookeeper;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client.Operations
{
    internal class CreateOperation : BaseOperation<CreateRequest, CreateResult>
    {
        public CreateOperation(CreateRequest request)
            : base(request)
        {
        }

        public override async Task<CreateResult> Execute(ZooKeeperNetExClient client)
        {
            if (!Helper.ValidateDataSize(Request.Data))
                return CreateUnsuccessfulResult(ZooKeeperStatus.BadArguments, Helper.DataSizeLimitExceededException(Request.Data));

            var newPath = await client.createAsync(Request.Path, Request.Data, ZooDefs.Ids.OPEN_ACL_UNSAFE, Request.CreateMode.ToZooKeeperMode()).ConfigureAwait(false);
            return new CreateResult(ZooKeeperStatus.Ok, Request.Path, newPath);
        }

        public override CreateResult CreateUnsuccessfulResult(ZooKeeperStatus status, Exception exception) => new CreateResult(status, Request.Path, null) {Exception = exception};
    }
}