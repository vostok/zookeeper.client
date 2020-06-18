using System;
using Vostok.Commons.Threading;
using Vostok.Commons.Time;

namespace Vostok.ZooKeeper.Client.Holder
{
    internal class SuspendedManager
    {
        private readonly TimeSpan period;
        private readonly TimeSpan periodCap;
        private readonly double maxJitterFraction;
        private readonly int initialBackoffDepth;
        private readonly AtomicInt backoffDepth;

        public SuspendedManager(
            TimeSpan period,
            TimeSpan periodCap,
            double maxJitterFraction,
            int initialBackoffDepth)
        {
            this.period = period;
            this.periodCap = periodCap;
            this.maxJitterFraction = maxJitterFraction;
            this.initialBackoffDepth = initialBackoffDepth;

            backoffDepth = new AtomicInt(initialBackoffDepth);
        }

        public TimeBudget GetNextDelay()
        {
            var baseDelayMs = Math.Min(periodCap.TotalMilliseconds, period.TotalMilliseconds * Math.Max(0, backoffDepth));

            if (baseDelayMs <= 0)
                return null;

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