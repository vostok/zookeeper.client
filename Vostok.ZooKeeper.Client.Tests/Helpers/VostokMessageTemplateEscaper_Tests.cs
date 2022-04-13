using FluentAssertions;
using NUnit.Framework;
using Vostok.ZooKeeper.Client.Helpers;

namespace Vostok.ZooKeeper.Client.Tests.Helpers
{
    [TestFixture]
    public class VostokMessageTemplateEscaper_Tests
    {
        [Test]
        public void Should_EscapeBrackets_CasualUsage()
        {
            VostokMessageTemplateEscaper.Escape
                    ("Session establishment complete on server {10.217.9.47:2181}, sessionid = 0x5047bed84ab9a42, negotiated timeout = 10000")
               .Should()
               .Be("Session establishment complete on server {{10.217.9.47:2181}}, sessionid = 0x5047bed84ab9a42, negotiated timeout = 10000");
        }
    }
}