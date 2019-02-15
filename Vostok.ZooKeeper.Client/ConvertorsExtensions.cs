using System;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using CreateMode = org.apache.zookeeper.CreateMode;

namespace Vostok.ZooKeeper.Client
{
    internal static class ConvertorsExtensions
    {
        public static CreateMode ToZooKeeperMode(this Abstractions.Model.CreateMode mode)
        {
            switch (mode)
            {
                case Abstractions.Model.CreateMode.Persistent:
                    return CreateMode.PERSISTENT;
                case Abstractions.Model.CreateMode.Ephemeral:
                    return CreateMode.EPHEMERAL;
                case Abstractions.Model.CreateMode.PersistentSequential:
                    return CreateMode.PERSISTENT_SEQUENTIAL;
                case Abstractions.Model.CreateMode.EphemeralSequential:
                    return CreateMode.EPHEMERAL_SEQUENTIAL;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        public static Stat FromZooKeeperStat(this org.apache.zookeeper.data.Stat stat) => stat == null ? null : new Stat(stat.getCzxid(), stat.getMzxid(), stat.getPzxid(), stat.getCtime(), stat.getMtime(), stat.getVersion(), stat.getCversion(), stat.getAversion(), stat.getEphemeralOwner(), stat.getDataLength(), stat.getNumChildren());

        public static string ToZooKeeperConnectionString(this ZooKeeperClientSetup setup)
        {
            var connectionString = setup.GetConnectionString();
            if (!string.IsNullOrEmpty(setup.Namespace?.TrimStart('/')))
                connectionString += "/" + setup.Namespace.TrimStart('/');
            return connectionString;
        }

        public static int ToZooKeeperConnectionTimeout(this ZooKeeperClientSetup setup) => (int)setup.Timeout.TotalMilliseconds;
    }
}