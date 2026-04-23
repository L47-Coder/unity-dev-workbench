#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using DevWorkbench;

internal sealed class AssetManagerRefresher : IManagerRefresher
{
    // 框架内部的配置型 Group，只存储 SO 配置资产，不需要通过 AssetManager 加载。
    // 注意："Prefab" Group 不在排除列表内——PrefabManager 依赖 AssetManager 来
    // 加载 Prefab 资产，若排除该 Group 会导致 PrefabManager.LoadPrefabAsync 运行时报错。
    // 如果项目新增了其他纯配置型 Group，可在此追加。
    private static readonly HashSet<string> ExcludedGroupNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Built In Data",
        "ManagerConfig",
        "ComponentConfig",
        "Frame",
    };

    public void Refresh(BaseManagerConfig config)
    {
        var typed = (AssetManagerConfig)config;

        ManagerRefreshUtil.Sync(
            typed.EditorConfigs,
            CollectTargets(),
            static item => item.Key,
            static (key, address) => new AssetManagerData { Key = key, AssetAddress = address });

        EditorUtility.SetDirty(typed);
        AssetDatabase.SaveAssets();
    }

    private static Dictionary<string, string> CollectTargets()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) return result;

        foreach (var group in settings.groups)
        {
            if (group == null) continue;
            if (ExcludedGroupNames.Contains(group.Name)) continue;

            foreach (var entry in group.entries)
            {
                if (entry == null) continue;
                if (string.IsNullOrWhiteSpace(entry.address)) continue;
                result[entry.address] = entry.address;
            }
        }
        return result;
    }
}
#endif
