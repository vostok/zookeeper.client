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
using Vostok.ZooKeeper.Client.Operations;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client
{
    /// <summary>
    /// Represents a ZooKeeper client.
    /// </summary>
    [PublicAPI]
    public class ZooKeeperClient : IZooKeeperClient, IDisposable
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

        public IObservable<ConnectionState> OnConnectionStateChanged => clientHolder.OnConnectionStateChanged;

        public ConnectionState ConnectionState => clientHolder.ConnectionState;

        public long SessionId => clientHolder.SessionId;

        /// <inheritdoc />
        public async Task<CreateResult> CreateAsync(CreateRequest request)
        {
            return await PerformOperation(new CreateOperation(request));
        }

        public Task<DeleteResult> DeleteAsync(DeleteRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<SetDataResult> SetDataAsync(SetDataRequest request)
        {
            throw new NotImplementedException();
        }

        public async Task<ExistsResult> ExistsAsync(ExistsRequest request)
        {
            var data = await(await clientHolder.GetConnectedClient().ConfigureAwait(false)).existsAsync(request.Path).ConfigureAwait(false);
            return new ExistsResult(ZooKeeperStatus.Ok, request.Path, data.FromZooKeeperStat());
        }

        public Task<GetChildrenResult> GetChildrenAsync(GetChildrenRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetChildrenWithStatResult> GetChildrenWithStatAsync(GetChildrenRequest request)
        {
            throw new NotImplementedException();
        }

        public async Task<GetDataResult> GetDataAsync(GetDataRequest request)
        {
            var data = await (await clientHolder.GetConnectedClient().ConfigureAwait(false)).getDataAsync(request.Path).ConfigureAwait(false);
            return new GetDataResult(ZooKeeperStatus.Ok, request.Path, data.Data, data.Stat.FromZooKeeperStat());
        }

        public void Dispose()
        {
            log.Debug("Disposing client.");
            clientHolder.Dispose();
        }

        private async Task<TResult> PerformOperation<TRequest, TResult>(BaseOperation<TRequest, TResult> operation)
            where TRequest : ZooKeeperRequest
            where TResult : ZooKeeperResult
        {
            log.Debug($"Trying to {operation.Request}.");
            
            var client = await clientHolder.GetConnectedClient().ConfigureAwait(false);
            // TODO(kungurtsev): null client?

            TResult result;
            try
            {
                result = await operation.Execute(client).ConfigureAwait(false);
            }
            catch (KeeperException e)
            {
                result = operation.CreateUnsuccessfulResult(e.FromZooKeeperExcetion(), e);
            }
            catch (ArgumentException e)
            {
                result = operation.CreateUnsuccessfulResult(ZooKeeperStatus.BadArguments, e);
            }
            catch (Exception e)
            {
                result = operation.CreateUnsuccessfulResult(ZooKeeperStatus.UnknownError, e);
            }

            log.Debug($"Result {result}.");
            return result;
        }
    }
}