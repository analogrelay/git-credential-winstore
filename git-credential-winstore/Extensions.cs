using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Git.Credential.WinStore
{
    static class Extensions
    {
        public static TValue GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> self, TKey key, TValue def)
        {
            TValue ret;
            if (!self.TryGetValue(key, out ret))
            {
                return def;
            }
            return ret;
        }
    }
}
