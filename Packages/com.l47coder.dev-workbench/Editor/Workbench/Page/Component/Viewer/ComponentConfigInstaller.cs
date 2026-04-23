using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // 批量确保每个 BaseComponentConfig 子类对应的 .asset 存在、挂在 "ComponentConfig"
    // 组、地址为 "ComponentConfig/<Name>"。Component 侧无 Refresher 机制。
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

        public static int EnsureAllRegistered()
        {
            var changed = 0;
            foreach (var info in Collect())
            {
                if (info.AddressMatches) continue;
                if (!info.AssetExists && string.IsNullOrEmpty(info.AssetPath)) continue;

                ComponentCreationService.EnsureAssetAndAddressable(
                    info.ComponentName, info.AssetPath, info.Address);
                changed++;
            }
            return changed;
        }

        private static IEnumerable<Type> EnumerateConcreteConfigTypes()
        {
            foreach (var t in TypeCache.GetTypesDerivedFrom<BaseComponentConfig>())
            {
                if (t.IsAbstract) continue;
                yield return t;
            }
        }

        // 优先用 ComponentAssetIndex 查已存在的 .asset（容忍嵌套层级）；找不到时回落到
        // 约定路径：Assets/Game/Component/<ComponentName>/<TypeName>.asset。
        private static string LocateConfigAssetPath(string typeName, string componentName)
        {
            var fileName = $"{typeName}.asset";
            var found = ComponentAssetIndex.FindComponentAsset(fileName);
            if (!string.IsNullOrEmpty(found)) return found;

            return $"{ComponentRootAssetPath}/{componentName}/{fileName}";
        }
    }
}
