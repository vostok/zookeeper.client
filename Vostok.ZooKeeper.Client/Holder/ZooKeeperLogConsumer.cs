using System;
using System.Diagnostics;
using org.apache.utils;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Helpers;

namespace Vostok.ZooKeeper.Client.Holder
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
            var escapedMessage = VostokMessageTemplateEscaper.Escape(message);
            switch (severity)
            {
                case TraceLevel.Error:
                    log.Error(exception, escapedMessage);
                    break;
                case TraceLevel.Info:
                    log.Info(exception, escapedMessage);
                    break;
                case TraceLevel.Verbose:
                    log.Debug(exception, escapedMessage);
                    break;
                case TraceLevel.Warning:
                    log.Warn(exception, escapedMessage);
                    break;
            }
        }
    }
}