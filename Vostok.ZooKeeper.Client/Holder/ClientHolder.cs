using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Commons.Helpers.Extensions;
using Vostok.Commons.Helpers.Observable;
using Vostok.Commons.Threading;
using Vostok.Commons.Time;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Helpers;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client.Holder
{
    internal class ClientHolder : IDisposable
    {
        private readonly ILog log;
        private readonly ZooKeeperClientSettings settings;
        private readonly AtomicBoolean isDisposed = false;
        private ConnectionState lastSentConnectionState = ConnectionState.Disconnected;

        [CanBeNull]
        private volatile ClientHolderState state;

        public ClientHolder(ZooKeeperClientSettings settings, ILog log)
        {
            this.log = log;
            this.settings = settings;

            state = new ClientHolderState(null, null, ConnectionState.Disconnected, null);

            ZooKeeperLogInjector.Register(this, this.log);
        }

        public CachingObservable<ConnectionState> OnConnectionStateChanged { get; } = new CachingObservable<ConnectionState>(ConnectionState.Disconnected);

        public ConnectionState ConnectionState => state?.ConnectionState ?? ConnectionState.Died;

        public long SessionId => state?.Client?.GetSessionId() ?? 0;

        public byte[] SessionPassword => state?.Client?.GetSessionPassword();

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

                if (currentState.ConnectionState.IsConnected(settings.CanBeReadOnly))
                    return currentState.Client;

                if (!await currentState.NextState.Task.WaitAsync(budget.Remaining).ConfigureAwait(false))
                    return null;
            }

            return null;
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
            if (currentState != null && currentState.NeedToResetClient(settings))
                return ResetClient(currentState);

            return true;
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
                () => new ZooKeeperNetExClient(
                    newConnectionString,
                    settings.ToInnerConnectionTimeout(),
                    newConnectionWatcher,
                    settings.CanBeReadOnly),
                LazyThreadSafetyMode.ExecutionAndPublication);

            var newState = new ClientHolderState(newClient, newConnectionWatcher, ConnectionState.Disconnected, newConnectionString);

            if (ChangeState(currentState, newState))
            {
                newState.Client?.Touch();

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

            if (currentState.ConnectionState != newState.ConnectionState)
                log.Info("Connection state changed. Old: '{OldState}'. New: '{NewState}'.", currentState, newState);

            return true;
        }

        private void ProcessEvent(ConnectionEvent connectionEvent)
        {
            log.Debug("Processing connection state event '{ConnectionEvent}'.", connectionEvent);

            var currentState = state;

            if (currentState == null || !ReferenceEquals(connectionEvent.EventFrom, currentState.ConnectionWatcher))
                return;

            var newState = new ClientHolderState(
                currentState.LazyClient,
                currentState.ConnectionWatcher,
                connectionEvent.NewConnectionState,
                currentState.ConnectionString);

            if (!ChangeState(currentState, newState))
                return;

            if (newState.ConnectionState == ConnectionState.Expired)
                ResetClient(newState);
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