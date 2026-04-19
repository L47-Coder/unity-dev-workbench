using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using DevWorkbench;

namespace DevWorkbench.Editor
{

// 扫描所有 BaseManagerConfig 子类，批量确保：
//   1. 对应的 <Name>ManagerConfig.asset 已存在；
//   2. 已挂载到 "ManagerConfig" 组，且 address = "ManagerConfig/<Name>"。
// 同时提供批量执行 IManagerRefresher 的能力，免去用户挨个点刷新按钮。
internal static class ManagerConfigInstaller
{
    private const string ConfigClassSuffix = "ManagerConfig";
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

    // ── 扫描 ──────────────────────────────────────────────────────────────────

    public static List<ConfigEntryInfo> Collect()
    {
        var result = new List<ConfigEntryInfo>();
        var settings = AddressableAssetSettingsDefaultObject.Settings;

        foreach (var type in EnumerateConcreteConfigTypes())
        {
            var typeName = type.Name;
            if (!typeName.EndsWith(ConfigClassSuffix, StringComparison.Ordinal)) continue;

            var managerName = typeName[..^ConfigClassSuffix.Length];
            if (string.IsNullOrEmpty(managerName)) continue;

            var assetPath = LocateConfigAssetPath(typeName, managerName);
            var address = $"{FrameAssetInstaller.ManagerConfigGroupName}/{managerName}";

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

    // ── 批量安装 ──────────────────────────────────────────────────────────────

    public static int EnsureAllRegistered()
    {
        var changed = 0;
        foreach (var info in Collect())
        {
            if (info.AddressMatches) continue;

            if (!info.AssetExists && string.IsNullOrEmpty(info.AssetPath)) continue;

            // 直接复用已有的公共 API（内部会：创建 asset / 建组 / 注册 entry / SaveAssets）。
            ManagerCreationService.EnsureAssetAndAddressable(
                info.ManagerName, info.AssetPath, info.Address);
            changed++;
        }
        return changed;
    }

    // ── 批量执行 Refresher ────────────────────────────────────────────────────

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
                Debug.LogError($"[ManagerConfigInstaller] 执行 {info.ManagerName}ManagerRefresher 失败：{ex}");
            }
        }
        return executed;
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
                if (!typeof(BaseManagerConfig).IsAssignableFrom(t)) continue;
                if (t == typeof(BaseManagerConfig)) continue;
                yield return t;
            }
        }
    }

    // 优先用 ManagerAssetIndex 在 Manager 根目录下查找已存在的 .asset（容忍嵌套层级）；
    // 找不到时回落到约定路径：Assets/Game/Manager/<ManagerName>/<TypeName>.asset。
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
            Debug.LogWarning($"[ManagerConfigInstaller] 实例化 {refresherType.Name} 失败：{ex.Message}");
            return null;
        }
    }
}
}
