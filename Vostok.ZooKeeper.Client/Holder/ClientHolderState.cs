using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Commons.Time;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client.Holder
{
    internal class ClientHolderState : IDisposable
    {
        public readonly ConnectionState ConnectionState;
        public readonly IConnectionWatcher ConnectionWatcher;
        public readonly TaskCompletionSource<ClientHolderState> NextState = new TaskCompletionSource<ClientHolderState>(TaskCreationOptions.RunContinuationsAsynchronously);
        // CR(iloktionov): TimeBudget, который прилетает сюда (из 2 возможных мест), всегда создаётся через CreateNew(), а не StartNew(). 
        // CR(iloktionov): Значит, он не начинает тикать и никогда не истечёт (а на это сверху есть всякие проверки). Почему так? 
        public readonly TimeBudget TimeBeforeReset;
        public readonly bool IsSuspended;
        public readonly bool IsConnected;
        public readonly string ConnectionString;
        private readonly Lazy<ZooKeeperNetExClient> lazyClient;
        // CR(iloktionov): Зачем здесь эти settings? Кажется, они не используются.
        private readonly ZooKeeperClientSettings settings;
        private readonly DateTime created = DateTime.UtcNow;
        
        public ClientHolderState(
            Lazy<ZooKeeperNetExClient> client,
            IConnectionWatcher connectionWatcher,
            ConnectionState connectionState,
            string connectionString,
            ZooKeeperClientSettings settings)
        {
            lazyClient = client;
            ConnectionState = connectionState;
            ConnectionString = connectionString;
            ConnectionWatcher = connectionWatcher;
            TimeBeforeReset = TimeBudget.CreateNew(settings.Timeout);
            IsConnected = ConnectionState.IsConnected(settings.CanBeReadOnly);
            this.settings = settings;
        }

        // CR(iloktionov): Может, заменить для ясности на фабричные методы с названиями + private-конструктор?
        // CR(iloktionov): А то не очень-то очевидна разница и назначение разных конструкторов лишь по набору аргументов.
        public ClientHolderState(
            TimeBudget suspended,
            ZooKeeperClientSettings settings)
        {
            IsSuspended = true;
            ConnectionState = ConnectionState.Disconnected;
            TimeBeforeReset = suspended;
            this.settings = settings;
        }

        [CanBeNull]
        public ZooKeeperNetExClient Client
        {
            get
            {
                try
                {
                    return lazyClient?.Value;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        [Pure]
        public ClientHolderState WithConnectionState(ConnectionState newConnectionState) =>
            new ClientHolderState(
                lazyClient,
                ConnectionWatcher,
                newConnectionState,
                ConnectionString,
                settings);

        public void Dispose()
        {
            try
            {
                Client?.closeAsync().Wait();
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public override string ToString() =>
            IsSuspended
                ? $"{ConnectionState} (suspended for {TimeBeforeReset.Remaining.ToPrettyString()}) at {created.ToLocalTime():s}"
                : $"{ConnectionState} at {created.ToLocalTime():s}";
    }
}