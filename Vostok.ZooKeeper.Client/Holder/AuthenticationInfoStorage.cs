using System.Collections.Generic;
using System.Linq;
using Vostok.Commons.Collections;
using Vostok.ZooKeeper.Client.Abstractions.Model.Authentication;

namespace Vostok.ZooKeeper.Client.Holder
{
    internal class AuthenticationInfoStorage
    {
        private readonly HashSet<AuthenticationInfo> rules;
        private readonly object sync;

        public AuthenticationInfoStorage()
        {
            rules = new HashSet<AuthenticationInfo>(ByReferenceEqualityComparer<AuthenticationInfo>.Instance);
            sync = new object();
        }

        public void Add(AuthenticationInfo authenticationInfo)
        {
            lock (sync)
            {
                rules.Add(authenticationInfo);
            }
        }

        public IReadOnlyList<AuthenticationInfo> GetAll()
        {
            lock (sync)
            {
                return rules.ToList().AsReadOnly();
            }
        }
    }
}