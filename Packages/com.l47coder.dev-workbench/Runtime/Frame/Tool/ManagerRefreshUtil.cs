#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace DevWorkbench
{
    /// <summary>
    /// Editor-only helpers used by <see cref="IManagerRefresher"/>
    /// implementations to reconcile a serialised list with a set of target keys.
    /// Entries whose keys are no longer present are removed; entries for
    /// newly-seen keys are produced through a user-supplied factory.
    /// </summary>
    public static class ManagerRefreshUtil
    {
        /// <summary>
        /// Synchronise <paramref name="list"/> with <paramref name="targets"/>
        /// in place.
        /// </summary>
        /// <remarks>
        /// Entries whose key is <c>null</c> / empty / whitespace are treated as
        /// stale and removed — this keeps half-filled rows in the Inspector
        /// from crashing the refresh on <see cref="Dictionary{TKey,TValue}"/>'s
        /// "null key not allowed" guard.
        /// </remarks>
        /// <typeparam name="T">The list element type (typically the Manager's Data struct).</typeparam>
        /// <param name="list">The mutable list backing the config asset.</param>
        /// <param name="targets">The authoritative key/value map the list must mirror.</param>
        /// <param name="keyOf">Projects a list element to its key.</param>
        /// <param name="factory">Constructs a new element from a <c>(key, value)</c> pair.</param>
        public static void Sync<T>(
            List<T> list,
            IReadOnlyDictionary<string, string> targets,
            Func<T, string> keyOf,
            Func<string, string, T> factory)
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var key = keyOf(list[i]);
                if (string.IsNullOrWhiteSpace(key) || !targets.ContainsKey(key))
                    list.RemoveAt(i);
            }

            // 上面已经把空白 key 的条目全删了，这里 keyOf 的产物必定非空；
            // 直接喂给 HashSet / Dictionary 不会再触发 null-key 异常。
            var existing = new HashSet<string>(list.Select(keyOf), StringComparer.Ordinal);
            foreach (var pair in targets
                     .Where(kv => !existing.Contains(kv.Key))
                     .OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                list.Add(factory(pair.Key, pair.Value));
            }
        }
    }
}
#endif
