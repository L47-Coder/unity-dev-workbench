#if UNITY_EDITOR
using UnityEditor;
using DevWorkbench;

internal sealed class TManagerRefresher : IManagerRefresher
{
    public void Refresh(BaseManagerConfig config)
    {
        var typed = (TManagerConfig)config;
        var list = typed.EditorConfigs;

        // TODO: implement the custom refresh logic here.

        EditorUtility.SetDirty(typed);
        AssetDatabase.SaveAssets();
    }
}
#endif
