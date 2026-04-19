#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DevWorkbench;
using UnityEditor;
using UnityEditor.AddressableAssets;

internal sealed class AssetManagerRefresher : IManagerRefresher
{
    private const string BuiltInDataGroupName = "Built In Data";

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
            if (string.Equals(group.Name, BuiltInDataGroupName, StringComparison.OrdinalIgnoreCase)) continue;

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
