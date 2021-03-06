﻿using System;
using System.Threading.Tasks;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;
using Vostok.ZooKeeper.Client.Helpers;

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
            var result = await client.existsAsync(Request.Path, wrapper.Wrap(Request.Watcher, Request.IgnoreWatchersCache)).ConfigureAwait(false);

            return ExistsResult.Successful(Request.Path, result.ToNodeStat());
        }

        public override ExistsResult CreateUnsuccessfulResult(ZooKeeperStatus status, Exception exception) =>
            ExistsResult.Unsuccessful(status, Request.Path, exception);
    }
}