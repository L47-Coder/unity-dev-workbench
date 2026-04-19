#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DevWorkbench;
using UnityEngine;

namespace DevWorkbench.Editor
{

    /// <summary>
    /// IMGUI list control with its own border, toolbar (search + custom buttons) and support
    /// for row selection, rename, delete, external drag-and-drop and internal reordering.
    /// <para>The implementation is split across partial files: Main (state), API (public
    /// surface), Rendering (drawing + reorder interaction) and Search (search + glob).</para>
    /// </summary>
    public sealed partial class ListView
    {
        // ── Properties ──────────────────────────────────────────────────────

        /// <summary>Title shown on the left of the toolbar. Hidden when null or empty.</summary>
        public string Title { get; set; }

        /// <summary>Whether rows can be renamed via F2 / right-click.</summary>
        public bool CanRename { get; set; } = true;

        /// <summary>Whether rows can be deleted via Delete key / right-click.</summary>
        public bool CanDelete { get; set; } = true;

        /// <summary>Whether external objects can be dropped onto rows.</summary>
        public bool CanReceiveDrop { get; set; }

        /// <summary>Whether rows can be reordered by left-dragging.</summary>
        public bool CanReorder { get; set; }

        /// <summary>Row names to ignore (glob patterns supported, e.g. <c>*Config</c>). Matching rows are hidden.</summary>
        public List<string> IgnoredNames { get; set; } = new();

        /// <summary>Index of the currently selected row (-1 if none).</summary>
        public int SelectedIndex { get; private set; } = -1;

        /// <summary>Placeholder text for the search field. Defaults to "Search...".</summary>
        public string SearchPlaceholder
        {
            get => _searchPlaceholder;
            set => _searchPlaceholder = value ?? string.Empty;
        }

        /// <summary>Label of the "Rename" entry in the context menu. Defaults to "Rename".</summary>
        public string RenameLabel
        {
            get => _renameLabel;
            set => _renameLabel = value ?? "Rename";
        }

        /// <summary>Label of the "Delete" entry in the context menu. Defaults to "Delete".</summary>
        public string DeleteLabel
        {
            get => _deleteLabel;
            set => _deleteLabel = value ?? "Delete";
        }

        // ── Event subscriptions ─────────────────────────────────────────────

        /// <summary>Fires when a row is clicked / selected. Arguments: (index, label).</summary>
        public void OnRowSelected(Action<int, string> callback) =>
            _onRowSelected = callback;

        /// <summary>Fires after a row is renamed. Arguments: (index, oldName, newName).</summary>
        public void OnRowRenamed(Action<int, string, string> callback) =>
            _onRowRenamed = callback;

        /// <summary>Fires when a row is deleted. Arguments: (index, label).</summary>
        public void OnRowDeleted(Action<int, string> callback) =>
            _onRowDeleted = callback;

        /// <summary>Fires when an external object is dropped onto a row. Argument: (targetIndex).</summary>
        public void OnDropOnRow(Action<int> callback) =>
            _onDropOnRow = callback;

        /// <summary>
        /// Fires when a row is reordered internally. Arguments: (fromDataIndex, toDataIndex).
        /// <para><c>toDataIndex</c> follows <see cref="List{T}.Insert"/> semantics with range
        /// <c>[0, items.Count]</c>, i.e. "remove from the original position first, then insert
        /// before that index".</para>
        /// </summary>
        public void OnRowReordered(Action<int, int> callback) =>
            _onRowReordered = callback;

        /// <summary>Fires when the built-in "+" button on the toolbar is clicked.</summary>
        public void OnAddClicked(Action callback) =>
            _onAddClicked = callback;

        // ── Toolbar buttons ─────────────────────────────────────────────────

        /// <summary>Append a custom button on the right of the toolbar (shown to the left of "+").</summary>
        public void AddToolbarButton(GUIContent content, Action onClick, float width = 22f) =>
            _toolbarButtons.Add(new ToolbarButtonDef(content, onClick, width));

        /// <summary>Remove all custom toolbar buttons.</summary>
        public void ClearToolbarButtons() => _toolbarButtons.Clear();

        // ── Draw ────────────────────────────────────────────────────────────

        /// <summary>
        /// Draw the complete ListView inside the given rect (border → toolbar → list body).
        /// Call once per frame.
        /// </summary>
        public void Draw(Rect rect, IReadOnlyList<string> items)
        {
            CheckRenameBlur();
            HandleKeyboard();

            if (_hasPendingContextMenu)
            {
                _hasPendingContextMenu = false;
                var idx = _pendingContextIndex;
                var label = _pendingContextLabel;
                _pendingContextLabel = null;
                ShowContextMenu(idx, label);
            }

            if (_pendingBeginRename && SelectedIndex >= 0 && SelectedIndex < items.Count)
            {
                BeginRename(SelectedIndex, items[SelectedIndex]);
                _pendingBeginRename = false;
            }

            if (_pendingDelete && SelectedIndex >= 0 && SelectedIndex < items.Count)
            {
                _onRowDeleted?.Invoke(SelectedIndex, items[SelectedIndex]);
                _pendingDelete = false;
            }

            var boxRect = BoxDrawer.CalcBoxRect(rect);
            if (boxRect.width < 1f || boxRect.height < 1f) return;

            BoxDrawer.DrawBox(boxRect);
            var contentRect = BoxDrawer.CalcContentRect(boxRect);

            GUI.BeginGroup(contentRect);

            DrawToolbar(new Rect(0f, 0f, contentRect.width, ControlsToolbar.ToolbarHeight));

            if (SelectedIndex >= items.Count)
            {
                SelectedIndex = items.Count - 1;
                GUI.changed = true;
            }

            if (_renamingIndex >= 0 && _renamingIndex >= items.Count)
                CommitRename(false);

            var bodyRect = new Rect(
                0f, ControlsToolbar.ToolbarHeight,
                contentRect.width, Mathf.Max(0f, contentRect.height - ControlsToolbar.ToolbarHeight));

            var filtered = GetFilteredIndices(items);
            DrawBody(bodyRect, items, filtered);

            GUI.EndGroup();
        }
    }
#endif
}
