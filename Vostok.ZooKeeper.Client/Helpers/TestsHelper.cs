using System;
using System.Threading;
using System.Threading.Tasks;
using Vostok.Commons.Helpers.Extensions;
using Vostok.Commons.Time;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client.Helpers
{
    internal static class TestsHelper
    {
        public static Task KillSession(ZooKeeperClient client, string connectionString, TimeSpan timeout) =>
            KillSession(client.SessionId, client.SessionPassword, client.OnConnectionStateChanged, connectionString, timeout);

        public static async Task KillSession(long sessionId, byte[] sessionPassword, IObservable<ConnectionState> onConnectionStateChanged, string connectionString, TimeSpan timeout)
        {
            var zooKeeper = new ZooKeeperNetExClient(connectionString, 5000, null, sessionId, sessionPassword);
            try
            {
                var budged = TimeBudget.StartNew(timeout);
                var observer = new WaitStateObserver(ConnectionState.Expired);
                onConnectionStateChanged.Subscribe(observer);

                while (!budged.HasExpired)
                {
                    if (zooKeeper.getState().Equals(ZooKeeperNetExClient.States.CONNECTED))
                    {
                        break;
                    }
                    Thread.Sleep(100);
                }

                await zooKeeper.closeAsync().ConfigureAwait(false);

                if (await observer.Signal.Task.WaitAsync(budged.Remaining).ConfigureAwait(false))
                    return;
                
                throw new TimeoutException($"Expected to kill session within {timeout}, but failed to do so.");
            }
            finally
            {
                await zooKeeper.closeAsync().ConfigureAwait(false);
            }
        }
        
        private class WaitStateObserver : IObserver<ConnectionState>
        {
            public readonly TaskCompletionSource<bool> Signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly ConnectionState desiredState;

            public WaitStateObserver(ConnectionState desiredState)
            {
                this.desiredState = desiredState;
            }

            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
            }

            public void OnNext(ConnectionState value)
            {
                if (value == desiredState)
                    Signal.SetResult(true);
            }
        }
    }
}