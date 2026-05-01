using UnityEditor;
using DevWorkbench;
using DevWorkbench.Editor;
using System.Collections.Generic;

internal static class AManagerRefresher
{
    private const string ConfigAssetPath = "Assets/Game/Manager/A/AManagerConfig.asset";

    [EditorSync]
    public static void Run()
    {
        var cfg = AssetDatabase.LoadAssetAtPath<AManagerConfig>(ConfigAssetPath);
        if (cfg == null) return;

        var list = (List<AManagerData>)cfg.GetConfigList();

        // TODO: implement the custom refresh logic here.

        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
    }
}
