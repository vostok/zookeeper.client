using System;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using org.apache.zookeeper;
using Vostok.Commons.Threading;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;
using Vostok.ZooKeeper.Client.Holder;
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
        private readonly ZooKeeperClientSettings settings;
        private readonly ClientHolder clientHolder;
        private readonly WatcherWrapper watcherWrapper;
        private readonly AtomicBoolean isDisposed = false;

        /// <summary>
        /// Creates a new instance of <see cref="ZooKeeperClient"/> using given <paramref name="settings" />.
        /// </summary>
        public ZooKeeperClient([NotNull] ZooKeeperClientSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            log = settings.Log.ForContext<ZooKeeperClient>();

            clientHolder = new ClientHolder(settings, log);
            watcherWrapper = new WatcherWrapper(log);
        }

        /// <inheritdoc />
        /// <summary>
        /// <para>Pushes last value to new subscribers.</para>
        /// <para>Calls <see cref="IObserver{ConnectionState}.OnCompleted"/> on client <see cref="Dispose"/>.</para>
        /// </summary>
        public IObservable<ConnectionState> OnConnectionStateChanged => clientHolder.OnConnectionStateChanged;

        /// <inheritdoc />
        public ConnectionState ConnectionState => clientHolder.ConnectionState;

        /// <inheritdoc />
        public long SessionId => clientHolder.SessionId;

        /// <summary>
        /// Returns client session password or null if not connected.
        /// </summary>
        public byte[] SessionPassword => clientHolder.SessionPassword;

        /// <inheritdoc />
        public async Task<CreateResult> CreateAsync(CreateRequest request)
        {
            var result = await ExecuteOperation(new CreateOperation(request)).ConfigureAwait(false);
            if (result.Status != ZooKeeperStatus.NodeNotFound || !request.CreateParentsIfNeeded)
                return result;

            var parentPath = ZooKeeperPath.GetParentPath(request.Path);
            if (parentPath == null)
                return CreateResult.Unsuccessful(ZooKeeperStatus.BadArguments, request.Path, 
                    new ArgumentException($"Can't get parent path for `{request.Path}`"));

            result = await CreateAsync(new CreateRequest(parentPath, CreateMode.Persistent)).ConfigureAwait(false);
            if (!result.IsSuccessful && result.Status != ZooKeeperStatus.NodeAlreadyExists)
                return CreateResult.Unsuccessful(result.Status, request.Path, result.Exception);

            // Note(kungurtsev): not infinity retry, if someone deletes our parent again.
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

        /// <inheritdoc />
        public Task<SetDataResult> SetDataAsync(SetDataRequest request) =>
            ExecuteOperation(new SetDataOperation(request));

        /// <inheritdoc />
        public Task<ExistsResult> ExistsAsync(ExistsRequest request) =>
            ExecuteOperation(new ExistsOperation(request, watcherWrapper));

        /// <inheritdoc />
        public Task<GetChildrenResult> GetChildrenAsync(GetChildrenRequest request) =>
            ExecuteOperation(new GetChildrenOperation(request, watcherWrapper));

        /// <inheritdoc />
        public Task<GetDataResult> GetDataAsync(GetDataRequest request) =>
            ExecuteOperation(new GetDataOperation(request, watcherWrapper));

        /// <summary>
        /// <para>Dispose this client object.</para>
        /// <para>Once the client is closed, its session becomes invalid.</para>
        /// <para>All the ephemeral nodes in the ZooKeeper server associated with the session will be removed.</para>
        /// <para>The watches left on nodes will be triggered.</para>
        /// </summary>
        public void Dispose()
        {
            if (isDisposed.TrySetTrue())
                clientHolder.Dispose();
        }

        private async Task<DeleteResult> DeleteWithChildren(DeleteRequest request)
        {
            while (true)
            {
                var children = await GetChildrenAsync(new GetChildrenRequest(request.Path)).ConfigureAwait(false);
                if (!children.IsSuccessful)
                {
                    // Even if status is ZooKeeperStatus.NodeNotFound, return it too, because someone else deleted node before us.
                    return DeleteResult.Unsuccessful(children.Status, request.Path, null);
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
                result = operation.CreateUnsuccessfulResult(e.FromZooKeeperException(), e);
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