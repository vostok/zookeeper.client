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
            // CR(iloktionov): 1. А почему получился linear, а не exponential backoff?
            // CR(iloktionov): 2. Jitter у нас вышел довольно слабенький (+/- 0.1 от периода), он не размажет нагрузку равномерно во времени.
            // CR(iloktionov):    Предлагаю вместо него одно из двух (оба размазывают гораздо лучше):
            // CR(iloktionov):    full jitter: delay = rnd(0, min(cap, base * 2 ^ depth))
            // CR(iloktionov):    equal jitter: 
            // CR(iloktionov):      temp =  min(cap, base * 2 ^ depth)
            // CR(iloktionov):      delay = temp/2 + rnd(0, temp/2)
            // CR(iloktionov): 3. Почему нужно долго ждать перед тем, как backoff начинает работать?
            // CR(iloktionov):    По сути, нужно вызвать IncreaseDelay() 4 раза (-3 --> 1), чтобы появилась suspend-задержка.
            // CR(iloktionov):    Каждый такой "раз" это 10 секунд (=SessionTimeout), за который успевает произойти несколько попыток коннекта.

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