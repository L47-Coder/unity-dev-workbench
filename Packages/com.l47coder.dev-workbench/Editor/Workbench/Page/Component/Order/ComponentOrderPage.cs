using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    internal sealed class ComponentOrderPage : IPage
    {
        public string GroupTitle => "Component";
        public string TabTitle => "Order";

        private ComponentOrderConfig _config;
        private readonly TableView _tableView = new() { CanAdd = false, CanRemove = false, SearchField = "Component", ShowToolbarButtons = false };

        public void OnFirstEnter()
        {
            _config = AssetDatabase.LoadAssetAtPath<ComponentOrderConfig>(GameFramePaths.ComponentOrder);
            _tableView.OnRowChanged<ComponentOrderEntry>((_, _) => EditorUtility.SetDirty(_config));
        }

        public void OnEnter()
        {
            if (_config == null) return;
            ComponentOrderSync.Sync(_config);
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
