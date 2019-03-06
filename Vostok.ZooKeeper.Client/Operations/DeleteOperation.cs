using System;
using System.Threading.Tasks;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ZooKeeper.Client.Operations
{
    internal class DeleteOperation : BaseOperation<DeleteRequest, DeleteResult>
    {
        public DeleteOperation(DeleteRequest request)
            : base(request)
        {
        }

        public override async Task<DeleteResult> Execute(org.apache.zookeeper.ZooKeeper client)
        {
            await client.deleteAsync(Request.Path, Request.Version).ConfigureAwait(false);
            return DeleteResult.Successful(Request.Path);
        }

        public override DeleteResult CreateUnsuccessfulResult(ZooKeeperStatus status, Exception exception) => 
            DeleteResult.Unsuccessful(status, Request.Path, exception);
    }
}