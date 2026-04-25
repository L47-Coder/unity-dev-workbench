using System;
using System.Collections.Generic;
using System.Linq;

namespace DevWorkbench.Editor
{
    public static class ManagerRefreshUtil
    {
        public static void Sync<T>(List<T> list, IReadOnlyDictionary<string, string> targets, Func<T, string> keyOf, Func<string, string, T> factory)
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var key = keyOf(list[i]);
                if (string.IsNullOrWhiteSpace(key) || !targets.ContainsKey(key))
                    list.RemoveAt(i);
            }

            var existing = new HashSet<string>(list.Select(keyOf), StringComparer.Ordinal);
            foreach (var pair in targets.Where(kv => !existing.Contains(kv.Key)).OrderBy(kv => kv.Key, StringComparer.Ordinal))
                list.Add(factory(pair.Key, pair.Value));
        }
    }
}
