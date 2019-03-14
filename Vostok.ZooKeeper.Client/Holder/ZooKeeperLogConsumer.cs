using System;
using System.Diagnostics;
using org.apache.utils;
using Vostok.Logging.Abstractions;

namespace Vostok.ZooKeeper.Client.Holder
{
    internal class ZooKeeperLogConsumer : ILogConsumer
    {
        private readonly ILog log;

        public ZooKeeperLogConsumer(ILog log)
        {
            this.log = log.ForContext("ZooKeeperNetExClient");
        }

        public void Log(TraceLevel severity, string className, string message, Exception exception)
        {
            switch (severity)
            {
                case TraceLevel.Error:
                    log.Error(exception, message);
                    break;
                case TraceLevel.Info:
                    log.Info(exception, message);
                    break;
                case TraceLevel.Verbose:
                    log.Debug(exception, message);
                    break;
                case TraceLevel.Warning:
                    log.Warn(exception, message);
                    break;
            }
        }
    }
}