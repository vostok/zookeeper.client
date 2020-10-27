using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using org.apache.zookeeper;
using Vostok.Commons.Threading;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Authentication;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;
using Vostok.ZooKeeper.Client.Helpers;
using Vostok.ZooKeeper.Client.Holder;
using Vostok.ZooKeeper.Client.Operations;
using CreateMode = Vostok.ZooKeeper.Client.Abstractions.Model.CreateMode;

namespace Vostok.ZooKeeper.Client
{
    /// <summary>
    /// <para>Represents a ZooKeeper client.</para>
    /// <para>This client automatically reconnects to ZooKeeper cluster on disconnect or session expiry.</para>
    /// </summary>
    [PublicAPI]
    public class ZooKeeperClient : IZooKeeperClient, IZooKeeperAuthClient, IDisposable
    {
        private readonly ILog log;
        private readonly ZooKeeperClientSettings settings;
        private readonly ClientHolder clientHolder;
        private readonly WatcherWrapper watcherWrapper;
        private readonly AtomicBoolean isDisposed = false;

        /// <summary>
        /// Creates a new instance of <see cref="ZooKeeperClient"/> using given <paramref name="settings" /> and <paramref name="log"/>.
        /// </summary>
        public ZooKeeperClient([NotNull] ZooKeeperClientSettings settings, [CanBeNull] ILog log)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.log = log = (log ?? LogProvider.Get())
                .ForContext<ZooKeeperClient>()
                .WithMinimumLevel(settings.LoggingLevel);

            clientHolder = new ClientHolder(settings, log);
            watcherWrapper = new WatcherWrapper(settings.WatchersCacheCapacity, log);
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
        /// <para>Returns negotiated session timeout or <see cref="ZooKeeperClientSettings.Timeout"/> if not connected.</para>
        /// </summary>
        public TimeSpan SessionTimeout => clientHolder.SessionTimeout;

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

            return await CreateWithParents(request).ConfigureAwait(false);
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

        /// <inheritdoc />
        public Task<GetAclResult> GetAclAsync(GetAclRequest request) =>
            ExecuteOperation(new GetAclOperation(request));

        /// <inheritdoc />
        public Task<SetAclResult> SetAclAsync(SetAclRequest request) =>
            ExecuteOperation(new SetAclOperation(request));

        /// <inheritdoc />
        public void AddAuthenticationInfo(AuthenticationInfo authenticationInfo)
        {
            clientHolder.AddAuthenticationInfo(authenticationInfo);
        }

        /// <inheritdoc />
        public void AddAuthenticationInfo(string login, string password)
        {
            clientHolder.AddAuthenticationInfo(AuthenticationInfo.Digest(login, password));
        }

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

        private async Task<CreateResult> CreateWithParents(CreateRequest request)
        {
            while (true)
            {
                var parentPath = ZooKeeperPath.GetParentPath(request.Path);
                if (parentPath == null)
                    return CreateResult.Unsuccessful(
                        ZooKeeperStatus.BadArguments,
                        request.Path,
                        new ArgumentException($"Can't get parent path for `{request.Path}`"));

                var result = await CreateAsync(new CreateRequest(parentPath, CreateMode.Persistent)).ConfigureAwait(false);
                if (!result.IsSuccessful && result.Status != ZooKeeperStatus.NodeAlreadyExists)
                    return CreateResult.Unsuccessful(result.Status, request.Path, result.Exception);

                result = await ExecuteOperation(new CreateOperation(request)).ConfigureAwait(false);
                if (result.Status != ZooKeeperStatus.NodeNotFound)
                    return result;

                // Note(kungurtsev): someone has deleted our parent since we checked, create it again.
            }
        }

        private async Task<DeleteResult> DeleteWithChildren(DeleteRequest request)
        {
            while (true)
            {
                var children = await GetChildrenAsync(new GetChildrenRequest(request.Path)).ConfigureAwait(false);
                if (!children.IsSuccessful)
                {
                    return DeleteResult.Unsuccessful(children.Status, request.Path, children.Exception);
                }

                foreach (var name in children.ChildrenNames)
                {
                    var deleted = await DeleteWithChildren(new DeleteRequest(ZooKeeperPath.Combine(request.Path, name))).ConfigureAwait(false);
                    if (!deleted.IsSuccessful)
                        return DeleteResult.Unsuccessful(deleted.Status, request.Path, deleted.Exception);
                }

                var result = await ExecuteOperation(new DeleteOperation(request)).ConfigureAwait(false);
                if (result.Status != ZooKeeperStatus.NodeHasChildren)
                    return result;

                // Note(kungurtsev): someone has created a new child since we checked, delete it again.
            }
        }

        private async Task<TResult> ExecuteOperation<TRequest, TResult>(BaseOperation<TRequest, TResult> operation)
            where TRequest : ZooKeeperRequest
            where TResult : ZooKeeperResult
        {
            TResult result;
            try
            {
                var client = await clientHolder.GetConnectedClient().ConfigureAwait(false);

                if (client == null)
                    result = operation.CreateUnsuccessfulResult(
                        clientHolder.ConnectionState == ConnectionState.Died ? ZooKeeperStatus.Died : ZooKeeperStatus.NotConnected,
                        null);
                else
                    result = await operation.Execute(client).ConfigureAwait(false);
            }
            catch (KeeperException e)
            {
                result = operation.CreateUnsuccessfulResult(e.ToZooKeeperStatus(), e);
            }
            catch (ArgumentException e)
            {
                result = operation.CreateUnsuccessfulResult(ZooKeeperStatus.BadArguments, e);
            }
            catch (Exception e)
            {
                result = operation.CreateUnsuccessfulResult(ZooKeeperStatus.UnknownError, e);
            }

            LogResult(operation.Request, result);

            return result;
        }

        private void LogResult<TRequest, TResult>(TRequest request, TResult result)
            where TRequest : ZooKeeperRequest
            where TResult : ZooKeeperResult
        {
            if (result.IsSuccessful)
            {
                var messageTemplate = "Request '{Request}' has completed successfully.";
                if (request.IsModifyingRequest())
                    log.Info(messageTemplate, request);
                else
                    log.Debug(messageTemplate, request);
            }
            else
            {
                var messageTemplate = "Request '{Request}' has failed with status '{ResultStatus}'.";
                var exception = result.Exception is KeeperException ? null : result.Exception;

                if (result.Status.IsMundaneError())
                    log.Info(messageTemplate, request, result.Status);
                else
                    log.Warn(exception, messageTemplate, request, result.Status);
            }
        }
    }
}