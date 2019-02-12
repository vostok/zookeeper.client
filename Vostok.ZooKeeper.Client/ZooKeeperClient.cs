using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using org.apache.zookeeper;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ZooKeeper.Client
{
    [PublicAPI]
    public class ZooKeeperClient : IZooKeeperClient
    {
        private org.apache.zookeeper.ZooKeeper client;

        public ZooKeeperClient(string connectionString, TimeSpan timeOut)
        {
            client = new org.apache.zookeeper.ZooKeeper(connectionString, (int)timeOut.TotalMilliseconds, null);
        }

        public async Task<CreateZooKeeperResult> CreateAsync(CreateZooKeeperRequest request)
        {
            var newPath = await client.createAsync(request.Path, request.Data, ZooDefs.Ids.OPEN_ACL_UNSAFE, request.CreateMode.ToZooKeeperMode()).ConfigureAwait(false);
            return new CreateZooKeeperResult(ZooKeeperStatus.Ok, newPath, newPath);
        }

        public Task<DeleteZooKeeperResult> DeleteAsync(DeleteZooKeeperRequest request) => throw new NotImplementedException();

        public Task<SetDataZooKeeperResult> SetDataAsync(SetDataZooKeeperRequest request) => throw new NotImplementedException();

        public Task<ExistsZooKeeperResult> ExistsAsync(ExistsZooKeeperRequest request) => throw new NotImplementedException();

        public Task<GetChildrenZooKeeperResult> GetChildrenAsync(GetChildrenZooKeeperRequest request) => throw new NotImplementedException();

        public Task<GetChildrenWithStatZooKeeperResult> GetChildrenWithStatAsync(GetChildrenZooKeeperRequest request) => throw new NotImplementedException();

        public async Task<GetDataZooKeeperResult> GetDataAsync(GetDataZooKeeperRequest request)
        {
            var data = await client.getDataAsync(request.Path).ConfigureAwait(false);
            return new GetDataZooKeeperResult(ZooKeeperStatus.Ok, request.Path, data.Data, data.Stat.FromZooKeeperStat());
        }

        public IObservable<ConnectionState> OnConnectionStateChanged { get; }

        public bool IsConnected { get; }

        public long SessionId { get; }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}