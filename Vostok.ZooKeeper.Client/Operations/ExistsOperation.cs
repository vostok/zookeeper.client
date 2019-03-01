using System;
using System.Threading.Tasks;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ZooKeeper.Client.Operations
{
    internal class ExistsOperation : BaseOperation<ExistsRequest, ExistsResult>
    {
        private readonly WatcherWrapper wrapper;

        public ExistsOperation(ExistsRequest request, WatcherWrapper wrapper)
            : base(request)
        {
            this.wrapper = wrapper;
        }

        public override async Task<ExistsResult> Execute(org.apache.zookeeper.ZooKeeper client)
        {
            var result = await client.existsAsync(Request.Path, wrapper.Wrap(Request.Watcher)).ConfigureAwait(false);

            return new ExistsResult(ZooKeeperStatus.Ok, Request.Path, result.FromZooKeeperStat());
        }

        public override ExistsResult CreateUnsuccessfulResult(ZooKeeperStatus status, Exception exception) => new ExistsResult(status, Request.Path, null) {Exception = exception};
    }
}