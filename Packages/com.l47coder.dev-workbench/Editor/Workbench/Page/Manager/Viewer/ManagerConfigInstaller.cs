using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // 批量确保每个 BaseManagerConfig 子类对应的 .asset 存在、挂在 "ManagerConfig" 组、
    // 地址为 "ManagerConfig/<Name>"；并提供批跑 IManagerRefresher 的入口。
    internal static class ManagerConfigInstaller
    {
        private const string ManagerRootAssetPath = "Assets/Game/Manager";

        public sealed class ConfigEntryInfo
        {
            public Type ConfigType;
            public string ManagerName;
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
                var managerName = ManagerAddressConvention.ManagerNameOf(type);
                if (string.IsNullOrEmpty(managerName)) continue;

                var assetPath = LocateConfigAssetPath(type.Name, managerName);
                var address = ManagerAddressConvention.AddressOf(managerName);

                var info = new ConfigEntryInfo
                {
                    ConfigType = type,
                    ManagerName = managerName,
                    AssetPath = assetPath,
                    Address = address,
                    AssetExists = !string.IsNullOrEmpty(assetPath) && AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath) != null,
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

                ManagerCreationService.EnsureAssetAndAddressable(
                    info.ManagerName, info.AssetPath, info.Address);
                changed++;
            }
            return changed;
        }

        public static int RunAllRefreshers()
        {
            var executed = 0;
            foreach (var info in Collect())
            {
                if (!info.AssetExists) continue;

                var asset = AssetDatabase.LoadAssetAtPath<BaseManagerConfig>(info.AssetPath);
                if (asset == null) continue;

                var refresher = ResolveRefresher(info);
                if (refresher == null) continue;

                try
                {
                    refresher.Refresh(asset);
                    executed++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ManagerConfigInstaller] Running {info.ManagerName}ManagerRefresher failed: {ex}");
                }
            }
            return executed;
        }

        private static IEnumerable<Type> EnumerateConcreteConfigTypes()
        {
            foreach (var t in TypeCache.GetTypesDerivedFrom<BaseManagerConfig>())
            {
                if (t.IsAbstract) continue;
                yield return t;
            }
        }

        // 优先用 ManagerAssetIndex 查已存在的 .asset（容忍嵌套层级）；找不到时回落到
        // 约定路径：Assets/Game/Manager/<ManagerName>/<TypeName>.asset。
        private static string LocateConfigAssetPath(string typeName, string managerName)
        {
            var fileName = $"{typeName}.asset";
            var found = ManagerAssetIndex.FindManagerAsset(fileName);
            if (!string.IsNullOrEmpty(found)) return found;

            return $"{ManagerRootAssetPath}/{managerName}/{fileName}";
        }

        private static IManagerRefresher ResolveRefresher(ConfigEntryInfo info)
        {
            var refresherType = ManagerRefresherLocator.FindRefresherType(info.ManagerName, info.AssetPath);
            if (refresherType == null) return null;

            try { return (IManagerRefresher)Activator.CreateInstance(refresherType); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ManagerConfigInstaller] Failed to instantiate {refresherType.Name}: {ex.Message}");
                return null;
            }
        }
    }
}
