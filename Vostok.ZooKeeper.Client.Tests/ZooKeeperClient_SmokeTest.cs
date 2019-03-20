using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Vostok.Commons.Time;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;

namespace Vostok.ZooKeeper.Client.Tests
{
    [TestFixture, Explicit]
    internal class ZooKeeperClient_SmokeTest : TestsBase
    {
        private readonly Random random = new Random();
        private readonly string path = $"/some/long/path/a/b/c/e-";
        private ZooKeeperClient client;

        [Test, Explicit]
        public async Task SmokeTest()
        {
            var cts = new CancellationTokenSource(60.Seconds());
            client = GetClient();

            var tasks = new List<Task>
            {
                EnsembleThread(cts.Token),
                KillSessionThread(cts.Token)
            };

            tasks.AddRange(Enumerable.Range(0, 10).Select(_ => ClientThread(cts.Token)));

            await Task.WhenAll(tasks);

            using (client = GetClient())
            {
                var children = await client.GetChildrenAsync("/some/long/path/a/b/c");
                Log.Info("Created nodes: " + string.Join(", ", children.ChildrenNames.OrderBy(x => x)));
            }
        }

        private async Task EnsembleThread(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await SleepRandom();
                Ensemble.Stop();
                await SleepRandom();
                Ensemble.Start();
            }
        }

        private async Task KillSessionThread(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await SleepRandom();
                await SleepRandom();
                try
                {
                    await KillSession(client, Ensemble.ConnectionString);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        private async Task ClientThread(CancellationToken token)
        {
            var created = new List<string>();
            while (!token.IsCancellationRequested)
            {
                var result = await client.CreateAsync(new CreateRequest(path, CreateMode.PersistentSequential));
                if (result.IsSuccessful)
                    created.Add(result.NewPath);

                await SleepRandom();
            }

            client.Dispose();

            Log.Info("Created thread nodes: " + string.Join(", ", created.OrderBy(x => x)));
        }

        private Task SleepRandom()
        {
            double sleep;
            lock (random)
            {
                sleep = random.NextDouble();
            }

            sleep = DefaultTimeout.TotalMilliseconds * 3 * sleep;

            return Task.Delay((int)sleep);
        }
    }
}