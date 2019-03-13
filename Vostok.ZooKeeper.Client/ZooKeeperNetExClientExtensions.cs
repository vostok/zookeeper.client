using System;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client
{
    internal static class ZooKeeperNetExClientExtensions
    {
        public static void Dispose(this ZooKeeperNetExClient client)
        {
            try
            {
                client.closeAsync().Wait();
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public static long GetSessionId(this ZooKeeperNetExClient client)
        {
            try
            {
                return client?.getSessionId() ?? 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static byte[] GetSessionPassword(this ZooKeeperNetExClient client)
        {
            try
            {
                return client?.getSessionPasswd();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Touch(this ZooKeeperNetExClient client)
        {
        }
    }
}