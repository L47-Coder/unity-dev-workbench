using UnityEditor;
using UnityEngine;
using DevWorkbench;

namespace DevWorkbench.Editor
{

internal sealed class ComponentOrderPage : IPage
{
    public string GroupTitle => "Component";
    public string TabTitle => "顺序";

    private ComponentOrderConfig _config;
    private readonly TableView _tableView = new() { CanAdd = false, CanRemove = false, SearchField = "Component", ShowToolbarButtons = false };

    public void OnStart()
    {
        FrameAssetInstaller.EnsureAddressablesInitialized();
        _config = FrameAssetInstaller.EnsureComponentOrderAsset();
        _tableView.OnRowChanged<ComponentOrderEntry>((_, _) => EditorUtility.SetDirty(_config));
    }

    public void OnEnter()
    {
        if (_config == null) return;
        FrameAssetInstaller.SyncComponentOrderEntries(_config);
    }

    public void OnGUI(Rect rect)
    {
        if (_config == null)
        {
            GUI.Label(rect, "配置加载失败", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        _tableView.Draw(rect, _config.Entries);
    }
}
}
