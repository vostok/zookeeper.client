using System;
using System.Diagnostics;
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
    /// Represents a ZooKeeper client.
    /// </summary>
    [PublicAPI]
    public class ZooKeeperClient : IZooKeeperClient
    {
        private readonly ILog log;
        private readonly ZooKeeperClientSetup setup;
        private readonly ClientHolder clientHolder;

        public ZooKeeperClient(ILog log, ZooKeeperClientSetup setup)
        {
            this.setup = setup;

            log = log.ForContext<ZooKeeperClient>();
            this.log = log;

            clientHolder = new ClientHolder(log, setup);
        }

        public async Task<CreateZooKeeperResult> CreateAsync(CreateZooKeeperRequest request)
        {
            log.Debug($"Trying to {request}.");
            var newPath = await (await clientHolder.GetConnectedClient().ConfigureAwait(false)).createAsync(request.Path, request.Data, ZooDefs.Ids.OPEN_ACL_UNSAFE, request.CreateMode.ToZooKeeperMode()).ConfigureAwait(false);
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
            var data = await (await clientHolder.GetConnectedClient().ConfigureAwait(false)).getDataAsync(request.Path).ConfigureAwait(false);
            return new GetDataZooKeeperResult(ZooKeeperStatus.Ok, request.Path, data.Data, data.Stat.FromZooKeeperStat());
        }

        public IObservable<ConnectionState> OnConnectionStateChanged => clientHolder.OnConnectionStateChanged;

        public ConnectionState ConnectionState => clientHolder.ConnectionState;

        public long SessionId => clientHolder.SessionId;

        public void Dispose()
        {
            log.Debug($"Disposing client.");
            clientHolder.Dispose();
        }
    }
}