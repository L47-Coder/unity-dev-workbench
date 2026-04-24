using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using DevWorkbench;

internal sealed class ComponentManagerRefresher : IManagerRefresher
{
    private const string TargetGroupName = "ComponentConfig";

    public void Refresh(BaseManagerConfig config)
    {
        var typed = (ComponentManagerConfig)config;

        ManagerRefreshUtil.Sync(
            typed.EditorConfigs,
            CollectTargets(),
            static item => item.Key,
            static (key, address) => new ComponentManagerData { Key = key, ComponentConfigAddress = address });

        EditorUtility.SetDirty(typed);
        AssetDatabase.SaveAssets();
    }

    private static Dictionary<string, string> CollectTargets()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) return result;

        var group = settings.groups.FirstOrDefault(g =>
            g != null && string.Equals(g.Name, TargetGroupName, StringComparison.Ordinal));
        if (group == null) return result;

        foreach (var entry in group.entries)
        {
            if (entry == null) continue;
            if (string.IsNullOrWhiteSpace(entry.address)) continue;
            result[entry.address] = entry.address;
        }
        return result;
    }
}
