using System;
using org.apache.zookeeper;
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

        public static NodeStat FromZooKeeperStat(this org.apache.zookeeper.data.Stat stat) => stat == null ? null : new NodeStat(stat.getCzxid(), stat.getMzxid(), stat.getPzxid(), stat.getCtime(), stat.getMtime(), stat.getVersion(), stat.getCversion(), stat.getAversion(), stat.getEphemeralOwner(), stat.getDataLength(), stat.getNumChildren());

        public static string ToZooKeeperConnectionString(this ZooKeeperClientSetup setup)
        {
            var connectionString = setup.GetConnectionString();
            if (!string.IsNullOrEmpty(setup.Namespace?.TrimStart('/')))
                connectionString += "/" + setup.Namespace.TrimStart('/');
            return connectionString;
        }

        public static ZooKeeperStatus FromZooKeeperExcetion(this KeeperException exception)
        {
            switch (exception.getCode())
            {
                case KeeperException.Code.CONNECTIONLOSS:
                    return ZooKeeperStatus.ConnectionLoss;
                case KeeperException.Code.OPERATIONTIMEOUT:
                    return ZooKeeperStatus.Timeout;
                case KeeperException.Code.BADARGUMENTS:
                    return ZooKeeperStatus.BadArguments;
                case KeeperException.Code.NONODE:
                    return ZooKeeperStatus.NodeNotFound;
                case KeeperException.Code.BADVERSION:
                    return ZooKeeperStatus.VersionConflict;
                case KeeperException.Code.NOCHILDRENFOREPHEMERALS:
                    return ZooKeeperStatus.ChildrenForEphemeralsAreNotAllowed;
                case KeeperException.Code.NODEEXISTS:
                    return ZooKeeperStatus.NodeAlreadyExists;
                case KeeperException.Code.NOTEMPTY:
                    return ZooKeeperStatus.NodeHasChildren;
                case KeeperException.Code.SESSIONEXPIRED:
                    return ZooKeeperStatus.SessionExpired;
                case KeeperException.Code.SESSIONMOVED:
                    return ZooKeeperStatus.SessionMoved;
                case KeeperException.Code.NOTREADONLY:
                    return ZooKeeperStatus.NotReadonlyOperation;
            }

            return ZooKeeperStatus.UnknownError;
        }

        public static int ToZooKeeperConnectionTimeout(this ZooKeeperClientSetup setup) => (int)setup.Timeout.TotalMilliseconds;
    }
}