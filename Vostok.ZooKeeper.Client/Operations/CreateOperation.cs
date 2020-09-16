using System;
using System.Threading.Tasks;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;
using Vostok.ZooKeeper.Client.Helpers;
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
            if (!NodeHelper.ValidateDataSize(Request.Data))
                return CreateUnsuccessfulResult(ZooKeeperStatus.BadArguments, NodeHelper.DataSizeLimitExceededException(Request.Data));

            var newPath = await client.createAsync(
                    Request.Path,
                    Request.Data,
                    Request.Acls.ToInnerAcls(),
                    Request.CreateMode.ToInnerCreateMode())
                .ConfigureAwait(false);
            return CreateResult.Successful(Request.Path, newPath);
        }

        public override CreateResult CreateUnsuccessfulResult(ZooKeeperStatus status, Exception exception) =>
            CreateResult.Unsuccessful(status, Request.Path, exception);
    }
}