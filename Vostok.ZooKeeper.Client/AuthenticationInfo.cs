using System.Text;
using JetBrains.Annotations;
using Vostok.ZooKeeper.Client.Abstractions.Model;

namespace Vostok.ZooKeeper.Client
{
    /// <summary>
    /// Represents authentication info.
    /// </summary>
    [PublicAPI]
    public class AuthenticationInfo
    {
        public AuthenticationInfo([NotNull] string scheme, [NotNull] byte[] data)
        {
            Scheme = scheme;
            Data = data;
        }

        public static AuthenticationInfo Digest([NotNull] string login, [NotNull] string password)
        {
            var data = Encoding.UTF8.GetBytes($"{login}:{password}");
            return new AuthenticationInfo(AuthSchemes.Digest, data);
        }

        [NotNull]
        public string Scheme { get; }

        [NotNull]
        public byte[] Data { get; }
    }
}