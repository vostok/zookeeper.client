using System;
using org.apache.zookeeper;
using org.apache.zookeeper.data;
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

        public static NodeStat FromZooKeeperStat(this Stat stat) => stat == null ? null : new NodeStat(stat.getCzxid(), stat.getMzxid(), stat.getPzxid(), stat.getCtime(), stat.getMtime(), stat.getVersion(), stat.getCversion(), stat.getAversion(), stat.getEphemeralOwner(), stat.getDataLength(), stat.getNumChildren());

        public static ZooKeeperStatus FromZooKeeperException(this KeeperException exception)
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
                    return ZooKeeperStatus.VersionsMismatch;
                case KeeperException.Code.NOCHILDRENFOREPHEMERALS:
                    return ZooKeeperStatus.ChildrenForEphemeralAreNotAllowed;
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

        public static NodeChangedEventType FromZooKeeperEventType(this Watcher.Event.EventType type)
        {
            switch (type)
            {
                case Watcher.Event.EventType.NodeCreated:
                    return NodeChangedEventType.Created;
                case Watcher.Event.EventType.NodeDeleted:
                    return NodeChangedEventType.Deleted;
                case Watcher.Event.EventType.NodeDataChanged:
                    return NodeChangedEventType.DataChanged;
                case Watcher.Event.EventType.NodeChildrenChanged:
                    return NodeChangedEventType.ChildrenChanged;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public static ConnectionState FromZooKeeperState(this Watcher.Event.KeeperState state)
        {
            switch (state)
            {
                case Watcher.Event.KeeperState.SyncConnected:
                    return ConnectionState.Connected;
                case Watcher.Event.KeeperState.ConnectedReadOnly:
                    return ConnectionState.ConnectedReadonly;
                case Watcher.Event.KeeperState.Expired:
                    return ConnectionState.Expired;
                case Watcher.Event.KeeperState.Disconnected:
                case Watcher.Event.KeeperState.AuthFailed:
                default:
                    return ConnectionState.Disconnected;
            }
        }

        public static int ToZooKeeperConnectionTimeout(this ZooKeeperClientSetup setup) => (int)setup.Timeout.TotalMilliseconds;
    }
}