using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Commons.Helpers.Extensions;
using Vostok.Commons.Helpers.Observable;
using Vostok.Commons.Time;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Helpers;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client
{
    internal class ClientHolderState : IDisposable
    {
        public ZooKeeperNetExClient Client => LazyClient?.Value;
        public readonly Lazy<ZooKeeperNetExClient> LazyClient;
        public readonly ConnectionState ConnectionState;
        public readonly DateTime StateChanged = DateTime.UtcNow;
        public readonly ConnectionWatcher ConnectionWatcher;
        public readonly TaskCompletionSource<ClientHolderState> NextState = new TaskCompletionSource<ClientHolderState>();

        public ClientHolderState(Lazy<ZooKeeperNetExClient> client, ConnectionWatcher connectionWatcher, ConnectionState connectionState)
        {
            LazyClient = client;
            ConnectionState = connectionState;
            ConnectionWatcher = connectionWatcher;
        }

        public void Dispose()
        {
            Client.Dispose();
        }

        public override string ToString() =>
            $"{ConnectionState} at {StateChanged}";
    }

    internal class ClientHolder : IDisposable
    {
        private readonly ILog log;
        private readonly ZooKeeperClientSetup setup;
        private ConnectionState lastSentConnectionState = ConnectionState.Disconnected;

        private volatile ClientHolderState state;

        public ClientHolder(ILog log, ZooKeeperClientSetup setup)
        {
            this.log = log.ForContext<ClientHolder>();
            this.setup = setup;

            state = new ClientHolderState(null, null, ConnectionState.Disconnected);

            LoggerHelper.InjectLogging(this.log);
        }

        public CachingObservable<ConnectionState> OnConnectionStateChanged { get; } = new CachingObservable<ConnectionState>(ConnectionState.Disconnected);

        public ConnectionState ConnectionState => state?.ConnectionState ?? ConnectionState.Disconnected;

        public long SessionId => state?.Client?.GetSessionId() ?? 0;

        public byte[] SessionPassword => state?.Client?.GetSessionPassword();

        public async Task<ZooKeeperNetExClient> GetConnectedClient()
        {
            var budget = TimeBudget.StartNew(setup.Timeout);

            while (!budget.HasExpired && !Disposed)
            {
                ResetClientIfNeeded(state);

                var currentState = state;
                if (IsConnected(currentState))
                    return currentState.Client;

                if (!await currentState.NextState.Task.WaitAsync(budget.Remaining).ConfigureAwait(false))
                    break;
            }

            return null;
        }

        public void Dispose()
        {
            var oldState = Interlocked.Exchange(ref state, null);

            if (oldState == null)
                return;

            SendOnConnectionStateChanged(true);

            oldState.NextState.TrySetResult(null);

            oldState.Dispose();

            log.Debug("Disposed.");
        }

        private bool Disposed => state == null;

        private bool IsConnected([CanBeNull] ClientHolderState currentState)
        {
            return currentState != null &&
                   (currentState.ConnectionState == ConnectionState.Connected || setup.CanBeReadOnly && currentState.ConnectionState == ConnectionState.ConnectedReadonly);
        }

        private void ResetClientIfNeeded([CanBeNull] ClientHolderState currentState)
        {
            if (currentState == null)
                return;

            if (currentState.Client == null ||
                !IsConnected(currentState) && DateTime.UtcNow - currentState.StateChanged > setup.Timeout)
                ResetClient(currentState);
        }

        private bool ChangeState([NotNull] ClientHolderState currentState, [NotNull] ClientHolderState newState)
        {
            if (Interlocked.CompareExchange(ref state, newState, currentState) != currentState)
                return false;

            SendOnConnectionStateChanged();

            currentState.NextState.TrySetResult(newState);

            log.Debug($"State changed. Old state: {currentState}. New state: {newState}.");
            return true;
        }

        private void ResetClient([NotNull] ClientHolderState currentState)
        {
            log.Debug($"Reseting client. Current state: {currentState}.");

            var newConnectionWatcher = new ConnectionWatcher(log, ProcessEvent);
            var newClient = new Lazy<ZooKeeperNetExClient>(() => 
                new ZooKeeperNetExClient(
                    setup.GetConnectionString(),
                    setup.ToZooKeeperConnectionTimeout(),
                    newConnectionWatcher,
                    setup.CanBeReadOnly), 
                LazyThreadSafetyMode.ExecutionAndPublication);

            var newState = new ClientHolderState(newClient, newConnectionWatcher, ConnectionState.Disconnected);

            if (!ChangeState(currentState, newState))
                return;

            newClient.Value.Touch();
            currentState.Dispose();
        }

        private void ProcessEvent(ConnectionEvent connectionEvent)
        {
            log.Debug($"Processing event {connectionEvent}.");

            var currentState = state;

            if (currentState == null || !ReferenceEquals(connectionEvent.EventFrom, currentState.ConnectionWatcher))
                return;

            var newState = new ClientHolderState(currentState.LazyClient, currentState.ConnectionWatcher, connectionEvent.NewConnectionState);

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