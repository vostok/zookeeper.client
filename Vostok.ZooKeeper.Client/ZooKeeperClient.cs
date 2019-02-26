using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using org.apache.zookeeper;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;
using Vostok.ZooKeeper.Client.Helpers;
using Vostok.ZooKeeper.Client.Operations;
using CreateMode = Vostok.ZooKeeper.Client.Abstractions.Model.CreateMode;
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

        public byte[] SessionPassword => clientHolder.SessionPassword;

        /// <inheritdoc />
        public async Task<CreateResult> CreateAsync(CreateRequest request)
        {
            // TODO(kungurtsev): namespace?

            var result = await PerformOperation(new CreateOperation(request)).ConfigureAwait(false);
            if (result.Status != ZooKeeperStatus.NodeNotFound)
                return result;

            var nodes = PathHelper.SplitPath(request.Path);
            for (var take = 1; take < nodes.Length; take++)
            {
                var path = "/" + string.Join("/", nodes.Take(take));
                var exists = await ExistsAsync(new ExistsRequest(path)).ConfigureAwait(false);

                if (!exists.IsSuccessful)
                    return new CreateOperation(request).CreateUnsuccessfulResult(exists.Status, exists.Exception);

                if (exists.Exists)
                    continue;

                result = await PerformOperation(new CreateOperation(new CreateRequest(path, CreateMode.Persistent))).ConfigureAwait(false);
                if (!result.IsSuccessful && result.Status != ZooKeeperStatus.NodeAlreadyExists)
                    return new CreateOperation(request).CreateUnsuccessfulResult(result.Status, result.Exception);
            }

            result = await PerformOperation(new CreateOperation(request)).ConfigureAwait(false);

            return result;
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

        /// <inheritdoc />
        public async Task<GetDataResult> GetDataAsync(GetDataRequest request)
        {
            var result = await PerformOperation(new GetDataOperation(request)).ConfigureAwait(false);
            return result;
        }

        /// <summary>
        /// <para>Dispose this client object.</para>
        /// <para>Once the client is closed, its session becomes invalid.</para>
        /// <para>All the ephemeral nodes in the ZooKeeper server associated with the session will be removed.</para>
        /// <para>The watches left on nodes will be triggered.</para>
        /// </summary>
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
            
            TResult result;
            try
            {
                var client = await clientHolder.GetConnectedClient().ConfigureAwait(false);

                if (client == null)
                    result = operation.CreateUnsuccessfulResult(ZooKeeperStatus.NotConnected, null);
                else
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