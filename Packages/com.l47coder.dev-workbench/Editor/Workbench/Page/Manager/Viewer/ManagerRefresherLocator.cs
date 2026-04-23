using System;
using System.IO;
using UnityEditor;

namespace DevWorkbench.Editor
{
    // 统一的 Refresher 查找工具：
    //   1. 先按"和 *ManagerConfig.asset 同目录、文件名为 <Name>ManagerRefresher.cs"的旧约定找
    //      （适合用户通过 DevWorkbench Creator 生成的 Refresher）；
    //   2. 失败则跨 AppDomain 按类名 <Name>ManagerRefresher 查找实现了 IManagerRefresher 的类型
    //      （适合包内置的 AssetManagerRefresher 等位于 UPM 包 Runtime 目录下的 Refresher）。
    internal static class ManagerRefresherLocator
    {
        private const string RefresherSuffix = "ManagerRefresher";

        public static Type FindRefresherType(string managerName, string assetPath)
        {
            var typeName = $"{managerName}{RefresherSuffix}";

            var script = TryLoadScriptBesideAsset(typeName, assetPath);
            if (script != null)
            {
                var t = script.GetClass();
                if (IsValidRefresherType(t)) return t;
            }

            return FindRefresherTypeInAppDomain(typeName);
        }

        public static MonoScript FindRefresherScript(string managerName, string assetPath)
        {
            var typeName = $"{managerName}{RefresherSuffix}";

            var script = TryLoadScriptBesideAsset(typeName, assetPath);
            if (script != null && IsValidRefresherType(script.GetClass()))
                return script;

            var type = FindRefresherTypeInAppDomain(typeName);
            if (type == null) return null;

            // 反查类型对应的 MonoScript asset（包内 .cs 也在 AssetDatabase 里有索引）。
            var guids = AssetDatabase.FindAssets($"t:MonoScript {typeName}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (monoScript != null && monoScript.GetClass() == type) return monoScript;
            }
            return null;
        }

        private static MonoScript TryLoadScriptBesideAsset(string typeName, string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            var folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder)) return null;

            var path = $"{folder}/{typeName}.cs";
            return AssetDatabase.LoadAssetAtPath<MonoScript>(path);
        }

        private static Type FindRefresherTypeInAppDomain(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch { continue; }

                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract) continue;
                    if (!string.Equals(t.Name, typeName, StringComparison.Ordinal)) continue;
                    if (!IsValidRefresherType(t)) continue;
                    return t;
                }
            }
            return null;
        }

        private static bool IsValidRefresherType(Type t) =>
            t != null && !t.IsAbstract && typeof(IManagerRefresher).IsAssignableFrom(t);
    }
}
