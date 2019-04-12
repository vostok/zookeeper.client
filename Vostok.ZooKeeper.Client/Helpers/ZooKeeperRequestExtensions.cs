using Vostok.ZooKeeper.Client.Abstractions.Model.Request;

namespace Vostok.ZooKeeper.Client.Helpers
{
    internal static class ZooKeeperRequestExtensions
    {
        public static bool IsModifyingRequest(this ZooKeeperRequest request)
        {
            return !(request is GetRequest);
        }
    }
}