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
                if (!targets.ContainsKey(keyOf(list[i])))
                    list.RemoveAt(i);
            }

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
