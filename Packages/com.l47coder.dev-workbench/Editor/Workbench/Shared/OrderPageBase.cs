using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    /// <summary>
    /// Generic base for Manager and Component order pages.
    /// Both pages are structurally identical: load a ScriptableObject config, run Sync,
    /// then render a read-only TableView.  Only the config type, asset path, search
    /// column header, sync delegate, and entries accessor differ.
    /// </summary>
    internal abstract class OrderPageBase<TConfig, TEntry> : IPage
        where TConfig : ScriptableObject
    {
        private TConfig _config;
        private readonly TableView _tableView;

        public abstract string GroupTitle { get; }
        public abstract string TabTitle   { get; }

        protected abstract string ConfigAssetPath    { get; }
        protected abstract string SearchColumnHeader { get; }
        protected abstract void   Sync(TConfig config);
        protected abstract List<TEntry> GetEntries(TConfig config);

        protected OrderPageBase()
        {
            _tableView = new TableView
            {
                CanAdd             = false,
                CanRemove          = false,
                ShowToolbarButtons = false,
            };
        }

        public void OnFirstEnter()
        {
            _tableView.SearchField = SearchColumnHeader;
            _config = AssetDatabase.LoadAssetAtPath<TConfig>(ConfigAssetPath);
            _tableView.OnRowChanged<TEntry>((_, _) => EditorUtility.SetDirty(_config));
        }

        public void OnEnter()
        {
            if (_config == null) return;
            Sync(_config);
        }

        public void OnGUI(Rect rect)
        {
            if (_config == null)
            {
                GUI.Label(rect, "Failed to load config.", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            _tableView.Draw(rect, GetEntries(_config));
        }
    }
}
