#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevWorkbench.Editor
{
    public sealed partial class ListView
    {
        private const float RowHeight = 22f;
        private const string RenameControlName = "ListViewRenameField";

        // ── Configurable labels (defaults for context menu & search) ────────

        private string _renameLabel = "Rename";
        private string _deleteLabel = "Delete";
        private string _searchPlaceholder = "Search...";

        // ── Core state ──────────────────────────────────────────────────────

        private Vector2 _scrollPos;
        private Action<int, string> _onRowSelected;
        private Action<int, string, string> _onRowRenamed;
        private Action<int, string> _onRowDeleted;
        private Action<int> _onDropOnRow;
        private Action<int, int> _onRowReordered;

        // ── Reorder drag state ──────────────────────────────────────────────

        private const string ReorderDragKey = "ListViewReorderToken";
        private const string ReorderFromKey = "ListViewReorderFromIndex";
        private const float ReorderDragThreshold = 4f;
        // GetControlID 的稳定 hint：没有 hint 时 ID 在不同事件类型下可能变化，
        // 导致 hotControl 匹配失败、MouseDrag 分支进不去（表现为"只能拖动一次"之类的怪现象）。
        private static readonly int s_reorderControlHint = "ListViewReorderControl".GetHashCode();

        private readonly object _reorderToken = new();
        private int _reorderControlId;
        private int _pressDataIndex = -1;
        private Vector2 _pressPos;
        private int _reorderFromIndex = -1;
        private int _reorderInsertIndex = -1;

        private int _renamingIndex = -1;
        private string _renamingOriginal;
        private string _renameBuffer;
        private bool _renameFocusRequest;
        private bool _renameHadFocus;
        private int _renameGeneration;
        private string RenameCtrl => $"{RenameControlName}_{_renameGeneration}";

        private bool _hasPendingContextMenu;
        private int _pendingContextIndex;
        private string _pendingContextLabel;

        private Action _onAddClicked;
        private readonly List<ToolbarButtonDef> _toolbarButtons = new();

        private readonly struct ToolbarButtonDef
        {
            public readonly GUIContent Content;
            public readonly Action OnClick;
            public readonly float Width;
            public ToolbarButtonDef(GUIContent content, Action onClick, float width)
            {
                Content = content;
                OnClick = onClick;
                Width = width;
            }
        }

        // ── Rename logic ────────────────────────────────────────────────────

        private void BeginRename(int dataIndex, string label)
        {
            GUI.FocusControl(string.Empty);
            GUIUtility.keyboardControl = 0;
            UnityEditor.EditorGUIUtility.editingTextField = false;
            _renameGeneration++;
            _renamingIndex = dataIndex;
            _renamingOriginal = label;
            _renameBuffer = label;
            _renameFocusRequest = true;
            _renameHadFocus = false;
        }

        private void CommitRename(bool accept)
        {
            var idx = _renamingIndex;
            var original = _renamingOriginal;
            var buffer = _renameBuffer;
            _renamingIndex = -1;
            _renamingOriginal = null;
            _renameBuffer = null;
            _renameFocusRequest = false;
            _renameHadFocus = false;
            if (!accept || idx < 0 || original == null) return;
            if (buffer != null && buffer != original)
                _onRowRenamed?.Invoke(idx, original, buffer);
        }

        private void CheckRenameBlur()
        {
            if (_renamingIndex < 0 || !_renameHadFocus) return;
            if (Event.current.type == EventType.Layout) return;
            if (GUI.GetNameOfFocusedControl() != RenameCtrl)
                CommitRename(true);
        }

        // ── Keyboard shortcuts ──────────────────────────────────────────────

        private void HandleKeyboard()
        {
            if (Event.current.type != EventType.KeyDown) return;

            if (_renamingIndex >= 0)
            {
                if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                { CommitRename(true); Event.current.Use(); }
                else if (Event.current.keyCode == KeyCode.Escape)
                { CommitRename(false); Event.current.Use(); }
                return;
            }

            if (SelectedIndex < 0) return;

            if (Event.current.keyCode == KeyCode.F2 && CanRename)
            { _pendingBeginRename = true; Event.current.Use(); }
            else if (Event.current.keyCode == KeyCode.Delete && CanDelete)
            { _pendingDelete = true; Event.current.Use(); }
        }

        private bool _pendingBeginRename;
        private bool _pendingDelete;
    }
}
#endif
