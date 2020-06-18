using System;
using Vostok.Commons.Threading;
using Vostok.Commons.Time;

namespace Vostok.ZooKeeper.Client.Holder
{
    internal class SuspendedManager
    {
        private readonly TimeSpan sendPeriod;
        private readonly TimeSpan sendPeriodCap;
        private readonly double maxJitterFraction;
        private readonly int initialBackoffDepth;
        private readonly AtomicInt backoffDepth;

        public SuspendedManager(
            TimeSpan sendPeriod,
            TimeSpan sendPeriodCap,
            double maxJitterFraction,
            int initialBackoffDepth)
        {
            this.sendPeriod = sendPeriod;
            this.sendPeriodCap = sendPeriodCap;
            this.maxJitterFraction = maxJitterFraction;
            this.initialBackoffDepth = initialBackoffDepth;

            backoffDepth = new AtomicInt(initialBackoffDepth);
        }

        public TimeBudget GetNextDelay()
        {
            var baseDelayMs = Math.Min(sendPeriodCap.TotalMilliseconds, sendPeriod.TotalMilliseconds * Math.Max(0, backoffDepth));

            var delay = TimeSpan.FromMilliseconds(baseDelayMs);

            var jitter = delay.Multiply(Random(-maxJitterFraction, maxJitterFraction));

            return TimeBudget.CreateNew(delay + jitter);
        }

        public void IncreaseDelay() =>
            backoffDepth.Increment();

        public void ResetDelay() =>
            backoffDepth.Exchange(initialBackoffDepth);

        private static double Random(double from, double to)
            => from + (to - from) * ThreadSafeRandom.NextDouble();
    }
}