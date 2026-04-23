using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace DevWorkbench.Editor
{
    // 按项目里活跃的 BaseManager 子类增量同步 ManagerOrderConfig.Entries：
    //   - 新增：AppDomain 中出现但 Entries 里没有的 → 追加到末尾
    //   - 移除：Entries 里有但 AppDomain 里找不到的（被删/改名）→ 丢弃
    //   - 保留：已存在的顺序和字段值不动
    //
    // 幂等；资产齐全时零写盘。被 ManagerOrderPage.OnEnter 和
    // ManagerViewerPage.OnWorkbenchOpen 共用。
    internal static class ManagerOrderSync
    {
        public static void Sync(ManagerOrderConfig config)
        {
            if (config == null) return;

            var live = CollectManagerTypeNames();
            var removed = config.Entries.RemoveAll(e => !live.Contains(e.Name)) > 0;

            var existing = new HashSet<string>(config.Entries.Select(e => e.Name), StringComparer.Ordinal);
            var added = false;
            foreach (var name in live.Where(n => !existing.Contains(n)))
            {
                config.Entries.Add(new ManagerOrderEntry { Name = name });
                added = true;
            }

            if (!removed && !added) return;

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        private static HashSet<string> CollectManagerTypeNames()
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
                    if (!typeof(BaseManager).IsAssignableFrom(t)) continue;
                    if (t == typeof(BaseManager)) continue;
                    result.Add(t.Name);
                }
            }
            return result;
        }
    }
}
