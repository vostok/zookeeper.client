using System.Collections.Generic;
using System.Linq;
using Vostok.ZooKeeper.Client.Abstractions.Model.Authentication;

namespace Vostok.ZooKeeper.Client.Helpers
{
    internal class AuthenticationInfoComparer : IEqualityComparer<AuthenticationInfo>
    {
        public static readonly AuthenticationInfoComparer Instance = new AuthenticationInfoComparer();

        public bool Equals(AuthenticationInfo x, AuthenticationInfo y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x == null || y == null)
                return false;

            return string.Equals(x.Scheme, y.Scheme) && x.Data.SequenceEqual(y.Data);
        }

        public int GetHashCode(AuthenticationInfo obj)
        {
            return (obj.Scheme.GetHashCode() * 397) ^ obj.Data.Aggregate(obj.Data.Length, (current, element) => (current * 397) ^ element.GetHashCode());
        }
    }
}