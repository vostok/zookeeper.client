using System;
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

namespace Vostok.ZooKeeper.Client
{
    /// <summary>
    /// <para>Represents a ZooKeeper client.</para>
    /// <para>This client is automatically reconnects to ZooKeeper cluster.</para>
    /// </summary>
    [PublicAPI]
    public class ZooKeeperClient : IZooKeeperClient, IDisposable
    {
        private readonly ILog log;
        private readonly ZooKeeperClientSetup setup;
        private readonly ClientHolder clientHolder;

        /// <summary>
        /// Creates a new instance of <see cref="ZooKeeperClient"/> using given <paramref name="log" /> and <paramref name="setup" />.
        /// </summary>
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

        /// <inheritdoc />
        public Task<DeleteResult> DeleteAsync(DeleteRequest request)
        {
            return PerformOperation(new DeleteOperation(request));
        }

        /// <inheritdoc />
        public async Task<SetDataResult> SetDataAsync(SetDataRequest request)
        {
            return await PerformOperation(new SetDataOperation(request)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<ExistsResult> ExistsAsync(ExistsRequest request)
        {
            return await PerformOperation(new ExistsOperation(request));
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
            return await PerformOperation(new GetDataOperation(request)).ConfigureAwait(false);
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