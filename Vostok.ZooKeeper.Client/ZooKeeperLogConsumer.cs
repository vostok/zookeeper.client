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
            this.log = log;
        }

        public void Log(TraceLevel severity, string className, string message, Exception exception)
        {
            switch (severity)
            {
                case TraceLevel.Error:
                    log.ForContext(className).Error(exception, message);
                    break;
                case TraceLevel.Info:
                    log.ForContext(className).Info(exception, message);
                    break;
                case TraceLevel.Off:
                    break;
                case TraceLevel.Verbose:
                    log.ForContext(className).Debug(exception, message);
                    break;
                case TraceLevel.Warning:
                    log.ForContext(className).Warn(exception, message);
                    break;
                default:
                    break;
            }
        }
    }
}