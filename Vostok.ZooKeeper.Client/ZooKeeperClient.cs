using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using org.apache.zookeeper;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ZooKeeper.Client
{
    /// <summary>
    /// <para>Represents a ZooKeeper client.</para>
    /// </summary>
    [PublicAPI]
    public class ZooKeeperClient : IZooKeeperClient
    {
        private readonly ILog log;
        private readonly org.apache.zookeeper.ZooKeeper client;

        public ZooKeeperClient(ILog log, string connectionString, TimeSpan timeOut)
        {
            this.log = log.ForContext<ZooKeeperClient>();

            org.apache.zookeeper.ZooKeeper.CustomLogConsumer = new ZooKeeperLogConsumer(log);
            org.apache.zookeeper.ZooKeeper.LogToFile = false;
            org.apache.zookeeper.ZooKeeper.LogToTrace = false;

            client = new org.apache.zookeeper.ZooKeeper(connectionString, (int)timeOut.TotalMilliseconds, new ZooKeeperWatcher(log));
        }

        public async Task<CreateZooKeeperResult> CreateAsync(CreateZooKeeperRequest request)
        {
            log.Debug($"Trying to {request}.");
            var newPath = await client.createAsync(request.Path, request.Data, ZooDefs.Ids.OPEN_ACL_UNSAFE, request.CreateMode.ToZooKeeperMode()).ConfigureAwait(false);
            return new CreateZooKeeperResult(ZooKeeperStatus.Ok, newPath, newPath);
        }

        public Task<DeleteZooKeeperResult> DeleteAsync(DeleteZooKeeperRequest request)
        {
            log.Debug($"Trying to {request}.");
            throw new NotImplementedException();
        }

        public Task<SetDataZooKeeperResult> SetDataAsync(SetDataZooKeeperRequest request)
        {
            log.Debug($"Trying to {request}.");
            throw new NotImplementedException();
        }

        public Task<ExistsZooKeeperResult> ExistsAsync(ExistsZooKeeperRequest request)
        {
            log.Debug($"Checking {request}.");
            throw new NotImplementedException();
        }

        public Task<GetChildrenZooKeeperResult> GetChildrenAsync(GetChildrenZooKeeperRequest request)
        {
            log.Debug($"Trying to {request}.");
            throw new NotImplementedException();
        }

        public Task<GetChildrenWithStatZooKeeperResult> GetChildrenWithStatAsync(GetChildrenZooKeeperRequest request)
        {
            log.Debug($"Trying to {request} with stat.");
            throw new NotImplementedException();
        }

        public async Task<GetDataZooKeeperResult> GetDataAsync(GetDataZooKeeperRequest request)
        {
            log.Debug($"Trying to {request}.");
            var data = await client.getDataAsync(request.Path).ConfigureAwait(false);
            return new GetDataZooKeeperResult(ZooKeeperStatus.Ok, request.Path, data.Data, data.Stat.FromZooKeeperStat());
        }

        public IObservable<ConnectionState> OnConnectionStateChanged { get; }

        public bool IsConnected { get; }

        public long SessionId { get; }

        public void Dispose()
        {
            log.Debug($"Disposing client.");
        }
    }
}