using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using DevWorkbench;

internal sealed class AssetManagerRefresher : IManagerRefresher
{
    // Framework-internal config groups that only hold SO config assets.
    // These are loaded directly by the framework and must not be exposed through AssetManager.
    // Note: "Prefab" is intentionally NOT excluded because PrefabManager loads prefabs via
    // AssetManager.LoadAssetAsync(prefabAddress); excluding it would break LoadPrefabAsync at runtime.
    // If your project adds more pure-config groups, append their names here.
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
