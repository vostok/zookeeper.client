using System;
using System.Threading;
using System.Threading.Tasks;
using Vostok.Commons.Helpers.Observable;
using Vostok.Commons.Threading;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Helpers;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client
{
    internal class ClientHolderState : IDisposable
    {
        public readonly ZooKeeperNetExClient Client;
        public readonly ConnectionState ConnectionState;
        public readonly DateTime StateChanged = DateTime.UtcNow;
        public readonly ConnectionWatcher ConnectionWatcher;

        public ClientHolderState(ZooKeeperNetExClient client, ConnectionWatcher connectionWatcher, ConnectionState connectionState)
        {
            Client = client;
            ConnectionState = connectionState;
            ConnectionWatcher = connectionWatcher;
        }

        public void Dispose()
        {
            Client.Dispose();
        }

        public override string ToString() =>
            $"{ConnectionState} changed at {StateChanged}";
    }

    internal class ClientHolder : IDisposable
    {
        private readonly ILog log;
        private readonly ZooKeeperClientSetup setup;
        private readonly AsyncManualResetEvent connectSignal = new AsyncManualResetEvent(false);

        private volatile ClientHolderState state;

        public ClientHolder(ILog log, ZooKeeperClientSetup setup)
        {
            this.log = log;
            this.setup = setup;

            state = new ClientHolderState(null, null, ConnectionState.Disconnected);

            LoggerHelper.InjectLogging(log);
        }

        public CachingObservable<ConnectionState> OnConnectionStateChanged { get; } = new CachingObservable<ConnectionState>(ConnectionState.Disconnected);

        public ConnectionState ConnectionState => state?.ConnectionState ?? ConnectionState.Disconnected;

        public long SessionId => state?.Client?.GetSessionId() ?? 0;

        public byte[] SessionPassword => state?.Client?.GetSessionPassword();

        public async Task<ZooKeeperNetExClient> GetConnectedClient()
        {
            if (Disposed)
                return null;

            var currentState = state;
            ResetClientIfNeeded(currentState);

            currentState = state;
            if (IsConnected(currentState))
                return currentState.Client;

            if (!await WaitWithTimeout(connectSignal).ConfigureAwait(false))
                return null;

            currentState = state;
            return IsConnected(currentState) ? currentState.Client : null;
        }

        public void Dispose()
        {
            log.Debug($"Disposing client. Current state: {ConnectionState}.");

            var oldState = Interlocked.Exchange(ref state, null);

            if (oldState == null)
            {
                log.Debug("Disposed by someone else.");
                return;
            }

            // TODO(kungurtsev): not send twice
            if (oldState.ConnectionState != ConnectionState.Disconnected)
                OnConnectionStateChanged.Next(ConnectionState.Disconnected);
            OnConnectionStateChanged.Complete();

            oldState.Dispose();
            log.Debug("Disposed.");
        }

        private bool Disposed => state == null;

        private bool IsConnected(ClientHolderState currentState)
        {
            return currentState != null && 
                   (currentState.ConnectionState == ConnectionState.Connected || setup.CanBeReadOnly && currentState.ConnectionState == ConnectionState.ConnectedReadonly);
        }

        private void ResetClientIfNeeded(ClientHolderState currentState)
        {
            if (currentState == null)
                return;

            if (currentState.Client == null ||
                !IsConnected(currentState) && DateTime.UtcNow - currentState.StateChanged > setup.Timeout)
                ResetClient(currentState);
        }

        private void ResetClient(ClientHolderState currentState)
        {
            log.Debug($"Reseting client. Current state: {currentState}.");
            if (currentState == null)
                return;

            var newConnectionWatcher = new ConnectionWatcher(log, ProcessEvent);
            var newClient = new ZooKeeperNetExClient(
                setup.GetConnectionString(),
                setup.ToZooKeeperConnectionTimeout(),
                newConnectionWatcher,
                setup.CanBeReadOnly);

            var newState = new ClientHolderState(newClient, newConnectionWatcher, ConnectionState.Disconnected);

            var exchangedState = Interlocked.CompareExchange(ref state, newState, currentState);
            if (exchangedState != currentState)
            {
                log.Debug($"Reset is skipped. Current state: {exchangedState}.");
                newState.Dispose();
                return;
            }

            log.Debug($"Reset is successful. Old state: {currentState}. New state: {newState}.");
            currentState.Dispose();
            // TODO(kungurtsev): notify observer
        }

        private void ProcessEvent(ConnectionEvent connectionEvent)
        {
            log.Debug($"Processing event {connectionEvent}.");

            var currentState = state;

            if (!ReferenceEquals(connectionEvent.EventFrom, currentState?.ConnectionWatcher))
            {
                log.Info($"Skip stale event {connectionEvent} (watcher changed). Current state: {currentState}.");
                return;
            }

            // ReSharper disable once PossibleNullReferenceException
            var newState = new ClientHolderState(currentState.Client, currentState.ConnectionWatcher, connectionEvent.NewConnectionState);

            var exchangedState = Interlocked.CompareExchange(ref state, newState, currentState);
            if (exchangedState != currentState)
            {
                log.Info($"Skip stale event {connectionEvent} (state changed). Current state: {exchangedState}.");
                return;
            }

            log.Debug($"Process event is successful. Old state: {exchangedState}. New state: {newState}.");

            OnConnectionStateChanged.Next(newState.ConnectionState);

            if (IsConnected(newState))
                connectSignal.Set();
            else if (IsConnected(exchangedState))
                connectSignal.Reset();

            if (newState.ConnectionState == ConnectionState.Expired)
                ResetClient(newState);
        }

        private async Task<bool> WaitWithTimeout(Task task)
        {
            using (var cts = new CancellationTokenSource())
            {
                var delay = Task.Delay(setup.Timeout, cts.Token);

                var result = await Task.WhenAny(task, delay).ConfigureAwait(false);
                if (result == delay)
                {
                    return false;
                }

                cts.Cancel();
            }

            return true;
        }
    }
}