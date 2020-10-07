using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Commons.Helpers.Extensions;
using Vostok.Commons.Helpers.Observable;
using Vostok.Commons.Threading;
using Vostok.Commons.Time;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Authentication;
using Vostok.ZooKeeper.Client.Helpers;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client.Holder
{
    internal class ClientHolder : IDisposable
    {
        private readonly ILog log;
        private readonly ZooKeeperClientSettings settings;
        private readonly AtomicBoolean isDisposed = false;
        private readonly SuspendedManager suspendedManager;
        private readonly object addAuthSyncObj;
        private readonly List<AuthenticationInfo> authenticationInfos;

        private ConnectionState lastSentConnectionState = ConnectionState.Disconnected;

        [CanBeNull]
        private volatile ClientHolderState state;

        public ClientHolder(ZooKeeperClientSettings settings, ILog log)
        {
            this.log = log;
            this.settings = settings;

            state = ClientHolderState.CreateActive(null, null, ConnectionState.Disconnected, null, settings);
            suspendedManager = new SuspendedManager(settings.Timeout, settings.Timeout.Multiply(settings.MaximumConnectPeriodMultiplier), -3);
            authenticationInfos = new List<AuthenticationInfo>();
            addAuthSyncObj = new object();

            ZooKeeperLogInjector.Register(this, this.log);
        }

        public CachingObservable<ConnectionState> OnConnectionStateChanged { get; } = new CachingObservable<ConnectionState>(ConnectionState.Disconnected);

        public ConnectionState ConnectionState => state?.ConnectionState ?? ConnectionState.Died;

        public long SessionId => state?.Client?.GetSessionId() ?? 0;

        public byte[] SessionPassword => state?.Client?.GetSessionPassword();

        public TimeSpan SessionTimeout => state?.Client?.GetSessionTimeout() ?? settings.Timeout;

        public async Task<ZooKeeperNetExClient> GetConnectedClient()
        {
            var budget = TimeBudget.StartNew(settings.Timeout);

            while (!budget.HasExpired)
            {
                if (!ResetClientIfNeeded(state))
                    return null;

                var currentState = state;
                if (currentState == null)
                    return null;

                if (currentState.IsConnected)
                    return currentState.Client;

                if (!await currentState.NextState.Task.WaitAsync(budget.Remaining).ConfigureAwait(false))
                    return null;
            }

            return null;
        }

        public void AddAuthenticationInfo(AuthenticationInfo authenticationInfo)
        {
            lock (addAuthSyncObj)
            {
                authenticationInfos.Add(authenticationInfo);
                state?.Client?.addAuthInfo(authenticationInfo.Scheme, authenticationInfo.Data);
            }
        }

        public void Dispose()
        {
            if (isDisposed.TrySetTrue())
            {
                var oldState = Interlocked.Exchange(ref state, null);

                SendOnConnectionStateChanged(true);

                oldState.NextState.TrySetResult(null);

                oldState.Dispose();

                ZooKeeperLogInjector.Unregister(this);
            }
        }

        private bool ResetClientIfNeeded([CanBeNull] ClientHolderState currentState)
        {
            if (currentState != null && NeedToResetClient(currentState))
                return ResetClient(currentState);

            return true;
        }

        private bool NeedToResetClient([NotNull] ClientHolderState currentState)
        {
            if (currentState.IsSuspended)
                return currentState.TimeBeforeReset.HasExpired;

            if (currentState.ConnectionString != settings.ConnectionStringProvider())
                return true;

            if (!currentState.IsConnected)
                return currentState.TimeBeforeReset.HasExpired;

            return false;
        }

        private async Task WaitAndResetClient([NotNull] ClientHolderState currentState)
        {
            try
            {
                await Task.Delay(currentState.TimeBeforeReset.Remaining).ConfigureAwait(false);

                if (ReferenceEquals(state, currentState))
                    ResetClient(currentState);
            }
            catch (Exception e)
            {
                log.Error(e, "Failed to reset client.");
            }
        }

        private bool ResetClient([NotNull] ClientHolderState currentState)
        {
            log.Info("Resetting client. Current state: '{CurrentState}'.", currentState);

            var newConnectionString = settings.ConnectionStringProvider();
            if (string.IsNullOrEmpty(newConnectionString))
            {
                log.Error("Failed to resolve any ZooKeeper replicas.");
                return false;
            }

            var newConnectionWatcher = new ConnectionWatcher(ProcessEvent);
            var newClient = new Lazy<ZooKeeperNetExClient>(
                () =>
                {
                    using (ExecutionContext.SuppressFlow())
                    {
                        var zk = new ZooKeeperNetExClient(
                            newConnectionString,
                            settings.ToInnerConnectionTimeout(),
                            newConnectionWatcher,
                            settings.CanBeReadOnly);

                        lock (addAuthSyncObj)
                        {
                            foreach (var authenticationInfo in authenticationInfos)
                                zk.addAuthInfo(authenticationInfo.Scheme, authenticationInfo.Data);
                        }

                        return zk;
                    }
                },
                LazyThreadSafetyMode.ExecutionAndPublication);

            var suspendedFor = suspendedManager.GetNextDelay();
            var newState = suspendedFor != null && !currentState.IsSuspended
                ? ClientHolderState.CreateSuspended(suspendedFor)
                : ClientHolderState.CreateActive(newClient, newConnectionWatcher, ConnectionState.Disconnected, newConnectionString, settings);

            if (ChangeState(currentState, newState))
            {
                newState.Client?.Touch();

                // Note(kungurtsev): increase delay for each active (not suspended) client creation.
                if (!currentState.IsSuspended)
                    suspendedManager.IncreaseDelay();

                currentState.Dispose();
            }

            return true;
        }

        private bool ChangeState([NotNull] ClientHolderState currentState, [NotNull] ClientHolderState newState)
        {
            if (Interlocked.CompareExchange(ref state, newState, currentState) != currentState)
                return false;

            SendOnConnectionStateChanged();

            currentState.NextState.TrySetResult(newState);

            log.Info("Connection state changed. Old: '{OldState}'. New: '{NewState}'.", currentState, newState);

            if (newState.ConnectionState == ConnectionState.Expired)
                ResetClient(newState);
            else if (!newState.IsConnected)
                Task.Run(() => WaitAndResetClient(newState));
            else
                suspendedManager.ResetDelay();

            return true;
        }

        private void ProcessEvent(ConnectionEvent connectionEvent)
        {
            log.Debug("Processing connection state event '{ConnectionEvent}'.", connectionEvent);

            var currentState = state;

            if (currentState == null || !ReferenceEquals(connectionEvent.EventFrom, currentState.ConnectionWatcher))
                return;

            var newState = currentState.WithConnectionState(connectionEvent.NewConnectionState, settings);

            ChangeState(currentState, newState);
        }

        private void SendOnConnectionStateChanged(bool complete = false)
        {
            // Note(kungurtsev): double reset can shuffle currentState, so we use state.

            lock (OnConnectionStateChanged)
            {
                var toSend = ConnectionState;
                if (lastSentConnectionState != toSend)
                {
                    OnConnectionStateChanged.Next(toSend);
                    lastSentConnectionState = toSend;
                }

                if (complete)
                    OnConnectionStateChanged.Complete();
            }
        }
    }
}