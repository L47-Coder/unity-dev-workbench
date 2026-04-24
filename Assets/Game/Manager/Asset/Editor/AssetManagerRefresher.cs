using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using DevWorkbench;

internal static partial class a
{
    public static partial void A() { }
}

internal static partial class a
{

}
internal static partial class a
{

}


internal sealed class AssetManagerRefresher : IManagerRefresher
{
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
