using Vostok.ZooKeeper.Client.Abstractions.Model;

namespace Vostok.ZooKeeper.Client.Helpers
{
    internal static class ZooKeeperStatusExtensions
    {
        public static bool IsMundaneError(this ZooKeeperStatus status)
            => status == ZooKeeperStatus.NodeAlreadyExists ||
               status == ZooKeeperStatus.NodeNotFound ||
               status == ZooKeeperStatus.NodeHasChildren;
    }
}