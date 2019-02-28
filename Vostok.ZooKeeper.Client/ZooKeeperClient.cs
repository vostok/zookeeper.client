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
        private readonly WatcherWrapper watcherWrapper;

        /// <summary>
        /// Creates a new instance of <see cref="ZooKeeperClient"/> using given <paramref name="log" /> and <paramref name="setup" />.
        /// </summary>
        public ZooKeeperClient(ILog log, ZooKeeperClientSetup setup)
        {
            this.setup = setup;

            log = log.ForContext<ZooKeeperClient>();
            this.log = log;

            clientHolder = new ClientHolder(log, setup);
            watcherWrapper = new WatcherWrapper(log);
        }

        /// <inheritdoc />
        public IObservable<ConnectionState> OnConnectionStateChanged => clientHolder.OnConnectionStateChanged;

        /// <inheritdoc />
        public ConnectionState ConnectionState => clientHolder.ConnectionState;

        /// <inheritdoc />
        public long SessionId => clientHolder.SessionId;

        /// <inheritdoc />
        public byte[] SessionPassword => clientHolder.SessionPassword;

        /// <inheritdoc />
        public async Task<CreateResult> CreateAsync(CreateRequest request)
        {
            var result = await ExecuteOperation(new CreateOperation(request)).ConfigureAwait(false);
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

                result = await ExecuteOperation(new CreateOperation(new CreateRequest(path, CreateMode.Persistent))).ConfigureAwait(false);
                if (!result.IsSuccessful && result.Status != ZooKeeperStatus.NodeAlreadyExists)
                    return new CreateOperation(request).CreateUnsuccessfulResult(result.Status, result.Exception);
            }

            result = await ExecuteOperation(new CreateOperation(request)).ConfigureAwait(false);

            return result;
        }

        /// <inheritdoc />
        public async Task<DeleteResult> DeleteAsync(DeleteRequest request)
        {
            var result = await ExecuteOperation(new DeleteOperation(request)).ConfigureAwait(false);
            if (result.Status != ZooKeeperStatus.NodeHasChildren || !request.DeleteChildrenIfNeeded)
                return result;

            return await DeleteWithChildren(request).ConfigureAwait(false);
        }

        private async Task<DeleteResult> DeleteWithChildren(DeleteRequest request)
        {
            while (true)
            {
                var children = await GetChildrenAsync(new GetChildrenRequest(request.Path)).ConfigureAwait(false);
                if (!children.IsSuccessful)
                {
                    // Even if status is ZooKeeperStatus.NodeNotFound, return it too, because someone else deleted node before us.
                    return new DeleteResult(children.Status, request.Path);
                }

                foreach (var name in children.ChildrenNames)
                {
                    await DeleteWithChildren(new DeleteRequest($"{request.Path}/{name}")).ConfigureAwait(false);
                }

                var result = await ExecuteOperation(new DeleteOperation(request)).ConfigureAwait(false);
                if (result.Status != ZooKeeperStatus.NodeHasChildren)
                    return result;

                // Someone has created a new child since we checked ... delete again.
            }
        }

        /// <inheritdoc />
        public async Task<SetDataResult> SetDataAsync(SetDataRequest request)
        {
            return await ExecuteOperation(new SetDataOperation(request)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<ExistsResult> ExistsAsync(ExistsRequest request)
        {
            return await ExecuteOperation(new ExistsOperation(request, watcherWrapper)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<GetChildrenResult> GetChildrenAsync(GetChildrenRequest request)
        {
            return await ExecuteOperation(new GetChildrenOperation(request, watcherWrapper)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<GetDataResult> GetDataAsync(GetDataRequest request)
        {
            return await ExecuteOperation(new GetDataOperation(request, watcherWrapper)).ConfigureAwait(false);
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

        private async Task<TResult> ExecuteOperation<TRequest, TResult>(BaseOperation<TRequest, TResult> operation)
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