using System;
using System.Threading.Tasks;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client.Operations
{
    internal abstract class BaseOperation<TRequest, TResult>
    {
        public readonly TRequest Request;

        protected BaseOperation(TRequest request)
        {
            Request = request;
        }

        public abstract Task<TResult> Execute(ZooKeeperNetExClient client);

        public abstract TResult CreateUnsuccessfulResult(ZooKeeperStatus status, Exception exception);
    }
}