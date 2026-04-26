using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace DevWorkbench.Editor
{
    internal static class ComponentConfigInstaller
    {
        private const string ConfigClassSuffix = "ComponentConfig";
        private const string ComponentRootAssetPath = GameProjectPaths.ComponentRoot;

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
                if (!typeName.EndsWith(ConfigClassSuffix, StringComparison.Ordinal))
                {
                    Debug.LogWarning($"[ComponentConfigInstaller] Type '{typeName}' does not follow the '*ComponentConfig' naming convention. Skipping.");
                    continue;
                }

                var componentName = typeName[..^ConfigClassSuffix.Length];
                if (string.IsNullOrEmpty(componentName)) continue;

                var assetPath = LocateConfigAssetPath(typeName, componentName);
                var address = ComponentAddressConvention.AddressOf(componentName);

                var info = new ConfigEntryInfo
                {
                    ConfigType = type,
                    ComponentName = componentName,
                    AssetPath = assetPath,
                    Address = address,
                    AssetExists = !string.IsNullOrEmpty(assetPath) && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath)),
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

                if (ComponentCreationService.EnsureAssetAndAddressable(info.ComponentName, info.AssetPath, info.Address))
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

        private static string LocateConfigAssetPath(string typeName, string componentName)
        {
            var fileName = $"{typeName}.asset";
            var found = ComponentAssetIndex.FindComponentAsset(fileName);
            if (!string.IsNullOrEmpty(found)) return found;

            return $"{ComponentRootAssetPath}/{componentName}/{fileName}";
        }
    }
}
