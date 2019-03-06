using System;
using System.Threading.Tasks;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ZooKeeper.Client.Operations
{
    internal class GetChildrenOperation : BaseOperation<GetChildrenRequest, GetChildrenResult>
    {
        private readonly WatcherWrapper wrapper;

        public GetChildrenOperation(GetChildrenRequest request, WatcherWrapper wrapper)
            : base(request)
        {
            this.wrapper = wrapper;
        }

        public override async Task<GetChildrenResult> Execute(org.apache.zookeeper.ZooKeeper client)
        {
            var result = await client.getChildrenAsync(Request.Path, wrapper.Wrap(Request.Watcher)).ConfigureAwait(false);

            return GetChildrenResult.Successful(Request.Path, result.Children, result.Stat.FromZooKeeperStat());
        }

        public override GetChildrenResult CreateUnsuccessfulResult(ZooKeeperStatus status, Exception exception) => 
            GetChildrenResult.Unsuccessful(status, Request.Path, exception);
    }
}