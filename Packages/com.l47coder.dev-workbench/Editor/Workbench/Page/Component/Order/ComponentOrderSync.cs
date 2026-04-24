using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace DevWorkbench.Editor
{
    internal static class ComponentOrderSync
    {
        public static void Sync(ComponentOrderConfig config)
        {
            if (config == null) return;

            var live = new HashSet<string>(TypeCache.GetTypesDerivedFrom<BaseComponent>().Where(t => !t.IsAbstract).Select(t => t.Name), StringComparer.Ordinal);

            var removed = config.Entries.RemoveAll(e => !live.Contains(e.Name)) > 0;

            var existing = new HashSet<string>(config.Entries.Select(e => e.Name), StringComparer.Ordinal);
            var added = false;
            foreach (var name in live.Where(n => !existing.Contains(n)))
            {
                config.Entries.Add(new ComponentOrderEntry { Name = name });
                added = true;
            }

            if (!removed && !added) return;

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
    }
}
