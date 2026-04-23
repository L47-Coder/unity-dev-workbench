using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace DevWorkbench.Editor
{
    // 按项目里活跃的 BaseManager 子类增量同步 ManagerOrderConfig.Entries：
    // 新类型追加末尾、失效条目丢弃、既有顺序保持。幂等，零变化时不写盘。
    internal static class ManagerOrderSync
    {
        public static void Sync(ManagerOrderConfig config)
        {
            if (config == null) return;

            var live = new HashSet<string>(
                TypeCache.GetTypesDerivedFrom<BaseManager>()
                    .Where(t => !t.IsAbstract)
                    .Select(t => t.Name),
                StringComparer.Ordinal);

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
    }
}
