using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ZooKeeper.Client
{
    [PublicAPI]
    public class ZooKeeperClient : IZooKeeperClient
    {
        public Task<CreateZooKeeperResult> CreateAsync(CreateZooKeeperRequest request) => throw new NotImplementedException();

        public Task<DeleteZooKeeperResult> DeleteAsync(DeleteZooKeeperRequest request) => throw new NotImplementedException();

        public Task<SetDataZooKeeperResult> SetDataAsync(SetDataZooKeeperRequest request) => throw new NotImplementedException();

        public Task<ExistsZooKeeperResult> ExistsAsync(ExistsZooKeeperRequest request) => throw new NotImplementedException();

        public Task<GetChildrenZooKeeperResult> GetChildrenAsync(GetChildrenZooKeeperRequest request) => throw new NotImplementedException();

        public Task<GetChildrenWithStatZooKeeperResult> GetChildrenWithStatAsync(GetChildrenZooKeeperRequest request) => throw new NotImplementedException();

        public Task<GetDataZooKeeperResult> GetDataAsync(GetDataZooKeeperRequest request) => throw new NotImplementedException();

        public IObservable<ConnectionState> OnConnectionStateChanged { get; }

        public bool IsConnected { get; }

        public long SessionId { get; }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}