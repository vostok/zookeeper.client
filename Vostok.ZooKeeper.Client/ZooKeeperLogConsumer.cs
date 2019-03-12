using System;
using System.Diagnostics;
using org.apache.utils;
using Vostok.Logging.Abstractions;

namespace Vostok.ZooKeeper.Client
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
            var localLog = log.ForContext(className);

            switch (severity)
            {
                case TraceLevel.Error:
                    localLog.Error(exception, message);
                    break;
                case TraceLevel.Info:
                    localLog.Info(exception, message);
                    break;
                case TraceLevel.Off:
                    break;
                case TraceLevel.Verbose:
                    localLog.Debug(exception, message);
                    break;
                case TraceLevel.Warning:
                    localLog.Warn(exception, message);
                    break;
            }
        }
    }
}