using System;
using Vostok.Commons.Threading;
using Vostok.Commons.Time;

namespace Vostok.ZooKeeper.Client.Holder
{
    internal class SuspendedManager
    {
        private readonly TimeSpan period;
        private readonly TimeSpan periodCap;
        private readonly int initialBackoffDepth;
        private readonly AtomicInt backoffDepth;

        public SuspendedManager(
            TimeSpan period,
            TimeSpan periodCap,
            int initialBackoffDepth)
        {
            this.period = period;
            this.periodCap = periodCap;
            this.initialBackoffDepth = initialBackoffDepth;

            backoffDepth = new AtomicInt(initialBackoffDepth);
        }

        public TimeBudget GetNextDelay()
        {
            // CR(iloktionov): 3. Почему нужно долго ждать перед тем, как backoff начинает работать?
            // CR(iloktionov):    По сути, нужно вызвать IncreaseDelay() 4 раза (-3 --> 1), чтобы появилась suspend-задержка.
            // CR(iloktionov):    Каждый такой "раз" это 10 секунд (=SessionTimeout), за который успевает произойти несколько попыток коннекта.

            if (backoffDepth < 0 || periodCap == TimeSpan.Zero)
                return null;

            var delayMs = Math.Min(periodCap.TotalMilliseconds, period.TotalMilliseconds * Math.Pow(2, backoffDepth));
            delayMs *= ThreadSafeRandom.NextDouble();
            
            return TimeBudget.StartNew(delayMs.Milliseconds());
        }

        public void IncreaseDelay() =>
            backoffDepth.Increment();

        public void ResetDelay() =>
            backoffDepth.Exchange(initialBackoffDepth);
    }
}