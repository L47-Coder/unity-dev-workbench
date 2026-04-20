using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // 扫描所有 BaseComponentConfig 子类，批量确保：
    //   1. 对应的 <Name>ComponentConfig.asset 已存在；
    //   2. 已挂载到 "ComponentConfig" 组，且 address = "ComponentConfig/<Name>"。
    // 与 ManagerConfigInstaller 刻意保持对称；Component 侧不走 Refresher 机制
    // （IComponentRefresher 不存在），所以这里没有 RunAllRefreshers。
    internal static class ComponentConfigInstaller
    {
        private const string ConfigClassSuffix = "ComponentConfig";
        private const string ComponentRootAssetPath = "Assets/Game/Component";

        public sealed class ConfigEntryInfo
        {
            public Type ConfigType;
            public string ComponentName;
            public string AssetPath;
            public string Address;
            public bool AssetExists;
            public bool HasAddressableEntry;
            public bool AddressMatches;
        }

        // ── 扫描 ──────────────────────────────────────────────────────────────────

        public static List<ConfigEntryInfo> Collect()
        {
            var result = new List<ConfigEntryInfo>();
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            foreach (var type in EnumerateConcreteConfigTypes())
            {
                var typeName = type.Name;
                if (!typeName.EndsWith(ConfigClassSuffix, StringComparison.Ordinal)) continue;

                var componentName = typeName[..^ConfigClassSuffix.Length];
                if (string.IsNullOrEmpty(componentName)) continue;

                var assetPath = LocateConfigAssetPath(typeName, componentName);
                var address = $"{ComponentCreatorState.AddressableGroupName}/{componentName}";

                var info = new ConfigEntryInfo
                {
                    ConfigType = type,
                    ComponentName = componentName,
                    AssetPath = assetPath,
                    Address = address,
                    AssetExists = !string.IsNullOrEmpty(assetPath)
                        && AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath) != null,
                };

                if (settings != null && info.AssetExists)
                {
                    var guid = AssetDatabase.AssetPathToGUID(info.AssetPath);
                    var entry = settings.FindAssetEntry(guid);
                    info.HasAddressableEntry = entry != null;
                    info.AddressMatches = entry != null && entry.address == address;
                }

                result.Add(info);
            }

            return result;
        }

        // ── 批量安装 ──────────────────────────────────────────────────────────────

        public static int EnsureAllRegistered()
        {
            var changed = 0;
            foreach (var info in Collect())
            {
                if (info.AddressMatches) continue;
                if (!info.AssetExists && string.IsNullOrEmpty(info.AssetPath)) continue;

                // 直接复用 ComponentCreator 的公共 API（内部会：创建 asset / 建组 / 注册 entry / SaveAssets）。
                ComponentCreationService.EnsureAssetAndAddressable(
                    info.ComponentName, info.AssetPath, info.Address);
                changed++;
            }
            return changed;
        }

        // ── 内部工具 ──────────────────────────────────────────────────────────────

        private static IEnumerable<Type> EnumerateConcreteConfigTypes()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch { continue; }

                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract) continue;
                    if (!typeof(BaseComponentConfig).IsAssignableFrom(t)) continue;
                    if (t == typeof(BaseComponentConfig)) continue;
                    yield return t;
                }
            }
        }

        // 优先用 ComponentAssetIndex 在 Component 根目录下查找已存在的 .asset（容忍嵌套层级）；
        // 找不到时回落到约定路径：Assets/Game/Component/<ComponentName>/<TypeName>.asset。
        private static string LocateConfigAssetPath(string typeName, string componentName)
        {
            var fileName = $"{typeName}.asset";
            var found = ComponentAssetIndex.FindComponentAsset(fileName);
            if (!string.IsNullOrEmpty(found)) return found;

            return $"{ComponentRootAssetPath}/{componentName}/{fileName}";
        }
    }
}
