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

        public void OnFirstEnter()
        {
            // 架构完整性由 DevWindow 开窗时的 DevWindowFrameworkGuard.Ensure 统一兜底，
            // 这里只负责 UI 一次性初始化；Order 资产直接 Load，不再重复 Ensure。
            _config = AssetDatabase.LoadAssetAtPath<ManagerOrderConfig>(GameFramePaths.ManagerOrder);
            _tableView.OnRowChanged<ManagerOrderEntry>((_, _) => EditorUtility.SetDirty(_config));
        }

        public void OnEnter()
        {
            if (_config == null) return;
            ManagerOrderSync.Sync(_config);
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
