using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace DevWorkbench.Editor
{
    // 按项目里活跃的 BaseComponent 子类增量同步 ComponentOrderConfig.Entries。
    // 形态刻意与 ManagerOrderSync 对齐；幂等，资产齐全时零写盘。
    // 被 ComponentOrderPage.OnEnter 和 ComponentViewerPage.OnWorkbenchOpen 共用。
    internal static class ComponentOrderSync
    {
        public static void Sync(ComponentOrderConfig config)
        {
            if (config == null) return;

            var live = CollectComponentTypeNames();
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

        private static HashSet<string> CollectComponentTypeNames()
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch { continue; }

                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract) continue;
                    if (!typeof(BaseComponent).IsAssignableFrom(t)) continue;
                    if (t == typeof(BaseComponent)) continue;
                    result.Add(t.Name);
                }
            }
            return result;
        }
    }
}
