using NUnit.Framework;
using Vostok.Tracing;
using Vostok.Tracing.Abstractions;

namespace Vostok.ZooKeeper.Client.Tests
{
    [SetUpFixture]
    internal class SetUpFixture
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            TracerProvider.Configure(new Tracer(new TracerSettings(new DevNullSpanSender())));
        }
    }
}