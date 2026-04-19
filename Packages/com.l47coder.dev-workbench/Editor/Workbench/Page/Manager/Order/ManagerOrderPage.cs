using DevWorkbench;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{

    internal sealed class ManagerOrderPage : IPage
    {
        public string GroupTitle => "Manager";
        public string TabTitle => "Order";

        private ManagerOrderConfig _config;
        private readonly TableView _tableView = new() { CanAdd = false, CanRemove = false, SearchField = "Manager", ShowToolbarButtons = false };

        public void OnStart()
        {
            FrameAssetInstaller.EnsureAddressablesInitialized();
            _config = FrameAssetInstaller.EnsureManagerOrderAsset();
            _tableView.OnRowChanged<ManagerOrderEntry>((_, _) => EditorUtility.SetDirty(_config));
        }

        public void OnEnter()
        {
            if (_config == null) return;
            FrameAssetInstaller.SyncManagerOrderEntries(_config);
        }

        public void OnGUI(Rect rect)
        {
            if (_config == null)
            {
                GUI.Label(rect, "Failed to load config.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _tableView.Draw(rect, _config.Entries);
        }
    }
}
