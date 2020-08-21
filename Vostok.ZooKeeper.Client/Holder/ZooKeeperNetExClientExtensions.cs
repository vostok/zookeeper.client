using System;
using System.Runtime.CompilerServices;
using Vostok.Commons.Time;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Client.Holder
{
    internal static class ZooKeeperNetExClientExtensions
    {
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

        public static TimeSpan? GetSessionTimeout(this ZooKeeperNetExClient client)
        {
            try
            {
                return client?.getSessionTimeout().Milliseconds();
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ReSharper disable once UnusedParameter.Global
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Touch(this ZooKeeperNetExClient client)
        {
        }
    }
}