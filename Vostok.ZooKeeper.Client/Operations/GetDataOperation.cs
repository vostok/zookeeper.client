using System;
using System.Threading.Tasks;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ZooKeeper.Client.Operations
{
    internal class GetDataOperation : BaseOperation<GetDataRequest, GetDataResult>
    {
        private readonly WatcherWrapper wrapper;

        public GetDataOperation(GetDataRequest request, WatcherWrapper wrapper)
            : base(request)
        {
            this.wrapper = wrapper;
        }

        public override async Task<GetDataResult> Execute(org.apache.zookeeper.ZooKeeper client)
        {
            var result = await client.getDataAsync(Request.Path, wrapper.Wrap(Request.Watcher)).ConfigureAwait(false);

            return GetDataResult.Successful(Request.Path, result.Data, result.Stat.FromZooKeeperStat());
        }

        public override GetDataResult CreateUnsuccessfulResult(ZooKeeperStatus status, Exception exception) =>
            GetDataResult.Unsuccessful(status, Request.Path, exception);
    }
}