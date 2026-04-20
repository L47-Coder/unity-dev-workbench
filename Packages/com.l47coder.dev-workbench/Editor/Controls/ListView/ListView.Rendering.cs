#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    public sealed partial class ListView
    {
        // ── Toolbar drawing ─────────────────────────────────────────────────

        private void DrawToolbar(Rect toolbarRect)
        {
            ControlsToolbar.DrawToolbarSeparator(toolbarRect);

            var pad = toolbarRect.height - ControlsToolbar.ToolbarSeparatorHeight - ControlsToolbar.SearchFieldHeight;
            var left = toolbarRect.x;
            var right = toolbarRect.xMax - pad;

            // ── Title (left-aligned) ────────────────────────────────────────
            if (!string.IsNullOrEmpty(Title))
            {
                var content = new GUIContent(Title);
                var w = ControlsToolbar.TitleStyle.CalcSize(content).x;
                GUI.Label(
                    new Rect(left, toolbarRect.y, w, toolbarRect.height), content, ControlsToolbar.TitleStyle);
                left += w + ControlsToolbar.ToolbarSectionGap;
            }

            // ── Built-in "+" button (right-aligned) ─────────────────────────
            var addSize = ControlsToolbar.SearchFieldHeight;
            var addY = toolbarRect.y + Mathf.Max(0f, pad - 0.5f);
            right -= addSize;
            if (GUI.Button(new Rect(right, addY, addSize, addSize), "+", ControlsToolbar.ButtonStyle))
                _onAddClicked?.Invoke();
            right -= pad;

            // ── Custom toolbar buttons (right-aligned, right to left) ───────
            for (var i = _toolbarButtons.Count - 1; i >= 0; i--)
            {
                var btn = _toolbarButtons[i];
                right -= btn.Width;
                var btnY = toolbarRect.y + (toolbarRect.height - ControlsToolbar.ToolbarButtonHeight) * 0.5f;
                if (GUI.Button(
                        new Rect(right, btnY, btn.Width, ControlsToolbar.ToolbarButtonHeight),
                        btn.Content, ControlsToolbar.ButtonStyle))
                    btn.OnClick?.Invoke();
                right -= ControlsToolbar.ToolbarButtonSpacing;
            }

            if (_toolbarButtons.Count > 0)
                right -= ControlsToolbar.ToolbarSectionGap - ControlsToolbar.ToolbarButtonSpacing;

            // ── Search bar (fills remaining space) ──────────────────────────
            var searchW = Mathf.Max(right - left, 20f);
            DrawSearchBar(new Rect(left, toolbarRect.y, searchW, toolbarRect.height));
        }

        // ── Row rendering ───────────────────────────────────────────────────

        private static GUIStyle _labelStyle;
        private static GUIStyle _labelStyleSelected;

        private static GUIStyle LabelStyle =>
            _labelStyle ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft };

        private static GUIStyle LabelStyleSelected =>
            _labelStyleSelected ??= new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft };

        private static Color RowBgColor(bool selected, bool alt, bool dropHighlight)
        {
            if (selected)
                return EditorGUIUtility.isProSkin
                    ? new Color(0.17f, 0.36f, 0.53f, 1f)
                    : new Color(0.24f, 0.49f, 0.91f, 1f);
            if (dropHighlight)
                return EditorGUIUtility.isProSkin
                    ? new Color(0.20f, 0.40f, 0.62f, 0.55f)
                    : new Color(0.24f, 0.49f, 0.91f, 0.30f);
            return alt
                ? (EditorGUIUtility.isProSkin
                    ? new Color(0.15f, 0.15f, 0.16f, 1f)
                    : new Color(0.96f, 0.97f, 0.98f, 1f))
                : (EditorGUIUtility.isProSkin
                    ? new Color(0.13f, 0.13f, 0.14f, 1f)
                    : new Color(1f, 1f, 1f, 1f));
        }

        private int _dropHighlightIndex = -1;

        private void DrawBody(Rect bodyRect, IReadOnlyList<string> items, List<int> filteredIndices)
        {
            var totalH = filteredIndices.Count * RowHeight;
            var needScroll = totalH > bodyRect.height;
            var innerW = Mathf.Max(bodyRect.width - (needScroll ? ControlsToolbar.VerticalScrollbarWidth : 0f), 1f);
            var viewRect = new Rect(0f, 0f, innerW, Mathf.Max(totalH, bodyRect.height));

            if (CanReorder)
            {
                TryStartReorderDrag(bodyRect, filteredIndices);
                HandleReorderDrag(bodyRect, items, filteredIndices);
            }

            if (CanReceiveDrop)
                HandleGlobalDrop(bodyRect, filteredIndices);

            _scrollPos = GUI.BeginScrollView(bodyRect, _scrollPos, viewRect);

            for (var vi = 0; vi < filteredIndices.Count; vi++)
            {
                var dataIndex = filteredIndices[vi];
                DrawRow(new Rect(0f, vi * RowHeight, innerW, RowHeight), items[dataIndex], dataIndex, vi);
            }

            if (CanReorder && _reorderInsertIndex >= 0 && Event.current.type == EventType.Repaint)
                DrawReorderIndicator(innerW, filteredIndices);

            if (_renamingIndex >= 0 &&
                Event.current.type == EventType.MouseDown &&
                Event.current.button == 0)
            {
                var clickedRenameRow = false;
                for (var i = 0; i < filteredIndices.Count; i++)
                {
                    if (filteredIndices[i] != _renamingIndex) continue;
                    var rowY = i * RowHeight;
                    clickedRenameRow = Event.current.mousePosition.y >= rowY &&
                                       Event.current.mousePosition.y < rowY + RowHeight;
                    break;
                }
                if (!clickedRenameRow)
                    CommitRename(true);
            }

            GUI.EndScrollView();
        }

        private void DrawRow(Rect rowRect, string label, int dataIndex, int visualIndex)
        {
            var isSelected = dataIndex == SelectedIndex;
            var isDropHighlight = CanReceiveDrop && dataIndex == _dropHighlightIndex;
            var isRenaming = _renamingIndex == dataIndex;

            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rowRect, RowBgColor(isSelected, visualIndex % 2 == 1, isDropHighlight));

            var labelRect = new Rect(rowRect.x + 8f, rowRect.y, Mathf.Max(0f, rowRect.width - 10f), rowRect.height);

            if (isRenaming)
            {
                DrawRenameField(labelRect);
            }
            else
            {
                var prevColor = GUI.contentColor;
                if (isSelected) GUI.contentColor = Color.white;
                GUI.Label(labelRect, label ?? string.Empty, isSelected ? LabelStyleSelected : LabelStyle);
                GUI.contentColor = prevColor;
            }

            if (isRenaming) return;

            if (Event.current.type == EventType.ContextClick &&
                rowRect.Contains(Event.current.mousePosition))
            {
                if (_renamingIndex >= 0) CommitRename(true);
                SelectedIndex = dataIndex;
                _onRowSelected?.Invoke(dataIndex, label);
                _hasPendingContextMenu = true;
                _pendingContextIndex = dataIndex;
                _pendingContextLabel = label;
                GUI.changed = true;
                Event.current.Use();
                return;
            }

            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                rowRect.Contains(Event.current.mousePosition))
            {
                if (_renamingIndex >= 0 && _renamingIndex != dataIndex)
                    CommitRename(true);
                SelectedIndex = dataIndex;
                _onRowSelected?.Invoke(dataIndex, label);

                GUI.changed = true;
                Event.current.Use();
            }
        }

        private void DrawRenameField(Rect rect)
        {
            var fieldH = EditorGUIUtility.singleLineHeight;
            rect = new Rect(rect.x, rect.y + (rect.height - fieldH) * 0.5f, rect.width, fieldH);

            var ctrl = RenameCtrl;
            GUI.SetNextControlName(ctrl);
            var wantText = _renameBuffer ?? string.Empty;
            var hasFocus = GUI.GetNameOfFocusedControl() == ctrl;

            if (_renameFocusRequest)
            {
                EditorGUI.FocusTextInControl(ctrl);
                EditorGUI.TextField(rect, wantText);

                if (hasFocus)
                {
                    var editor = GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) as TextEditor;
                    if (editor != null)
                    {
                        editor.text = wantText;
                        editor.SelectAll();
                    }
                    _renameFocusRequest = false;
                }
            }
            else
            {
                _renameBuffer = EditorGUI.TextField(rect, wantText);
            }

            if (hasFocus) _renameHadFocus = true;
        }

        private void ShowContextMenu(int dataIndex, string label)
        {
            var menu = new GenericMenu();

            if (CanRename)
                menu.AddItem(new GUIContent(_renameLabel), false, () => BeginRename(dataIndex, label));
            else
                menu.AddDisabledItem(new GUIContent(_renameLabel));

            if (CanDelete)
                menu.AddItem(new GUIContent(_deleteLabel), false, () => _onRowDeleted?.Invoke(dataIndex, label));
            else
                menu.AddDisabledItem(new GUIContent(_deleteLabel));

            menu.ShowAsContext();
        }

        private void HandleGlobalDrop(Rect bodyRect, List<int> filteredIndices)
        {
            var e = Event.current;

            if (e.type == EventType.DragExited)
            {
                _dropHighlightIndex = -1;
                return;
            }

            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)
                return;

            // 内部 reorder 拖动不走外部 drop 分支
            if (ReferenceEquals(DragAndDrop.GetGenericData(ReorderDragKey), _reorderToken))
                return;

            if (!bodyRect.Contains(e.mousePosition))
            {
                _dropHighlightIndex = -1;
                if (e.type == EventType.DragUpdated)
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                return;
            }

            var localY = e.mousePosition.y - bodyRect.y + _scrollPos.y;
            var vi = Mathf.FloorToInt(localY / RowHeight);
            var dataIndex = vi >= 0 && vi < filteredIndices.Count ? filteredIndices[vi] : -1;

            if (e.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                _dropHighlightIndex = dataIndex;
                GUI.changed = true;
                e.Use();
            }
            else
            {
                DragAndDrop.AcceptDrag();
                _dropHighlightIndex = -1;
                _onDropOnRow?.Invoke(dataIndex);
                GUI.changed = true;
                e.Use();
            }
        }

        // ── Internal reorder drag ───────────────────────────────────────────

        private void TryStartReorderDrag(Rect bodyRect, List<int> filteredIndices)
        {
            var e = Event.current;
            // 用稳定 hint 申请 controlID，保证跨帧、跨事件类型取到同一个 ID。
            _reorderControlId = GUIUtility.GetControlID(s_reorderControlHint, FocusType.Passive);

            switch (e.GetTypeForControl(_reorderControlId))
            {
                case EventType.MouseDown:
                    if (e.button != 0) break;
                    if (!bodyRect.Contains(e.mousePosition)) break;
                    var localY = e.mousePosition.y - bodyRect.y + _scrollPos.y;
                    var vi = Mathf.FloorToInt(localY / RowHeight);
                    if (vi < 0 || vi >= filteredIndices.Count) break;

                    _pressDataIndex = filteredIndices[vi];
                    _pressPos = e.mousePosition;
                    // 抢占 hotControl 以确保 Unity 后续派发 MouseDrag；
                    // 不 Use 事件，让 DrawRow 继续处理选中与 keyboard focus。
                    GUIUtility.hotControl = _reorderControlId;
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != _reorderControlId) break;
                    if (_pressDataIndex < 0) break;
                    if (Vector2.Distance(e.mousePosition, _pressPos) < ReorderDragThreshold) break;

                    _reorderFromIndex = _pressDataIndex;
                    _pressDataIndex = -1;

                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.SetGenericData(ReorderDragKey, _reorderToken);
                    DragAndDrop.SetGenericData(ReorderFromKey, _reorderFromIndex);
                    // 清空任何残留的 objectReferences / paths，
                    // 否则若 DragPerform 我们没来得及 Use，Unity 会用残留数据做默认导入。
                    DragAndDrop.objectReferences = System.Array.Empty<UnityEngine.Object>();
                    DragAndDrop.paths = System.Array.Empty<string>();
                    DragAndDrop.StartDrag("ListViewReorder");

                    GUIUtility.hotControl = 0;
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == _reorderControlId)
                        GUIUtility.hotControl = 0;
                    _pressDataIndex = -1;
                    break;
            }
        }

        private void HandleReorderDrag(Rect bodyRect, IReadOnlyList<string> items, List<int> filteredIndices)
        {
            var e = Event.current;

            if (e.type == EventType.DragExited)
            {
                _reorderInsertIndex = -1;
                // 注意：不在这里清 _reorderFromIndex —— DragExited 可能先于 DragPerform 发生。
                return;
            }

            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)
                return;

            // 只处理本控件发起的 reorder 拖动
            if (!ReferenceEquals(DragAndDrop.GetGenericData(ReorderDragKey), _reorderToken))
                return;

            var inside = bodyRect.Contains(e.mousePosition);
            var insertDataIndex = items.Count;
            if (inside)
            {
                var localY = e.mousePosition.y - bodyRect.y + _scrollPos.y;
                var insertVi = Mathf.Clamp(Mathf.RoundToInt(localY / RowHeight), 0, filteredIndices.Count);
                // 把"可见行插入位"映射回完整数据的插入位（List.Insert 语义，范围 [0, items.Count]）
                insertDataIndex = insertVi < filteredIndices.Count ? filteredIndices[insertVi] : items.Count;
            }

            if (e.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = inside
                    ? DragAndDropVisualMode.Move
                    : DragAndDropVisualMode.Rejected;
                _reorderInsertIndex = inside ? insertDataIndex : -1;
                GUI.changed = true;
                e.Use();
                return;
            }

            // DragPerform: 无论鼠标是否在 bodyRect 内都 AcceptDrag + Use，
            // 防止 Unity 接管默认处理（否则会出现"松手后转圈却不换位"的现象）。
            DragAndDrop.AcceptDrag();

            var fromObj = DragAndDrop.GetGenericData(ReorderFromKey);
            var from = fromObj is int fromIdx ? fromIdx : _reorderFromIndex;

            _reorderInsertIndex = -1;
            _reorderFromIndex = -1;

            if (inside && from >= 0 && from < items.Count &&
                insertDataIndex != from && insertDataIndex != from + 1)
            {
                _onRowReordered?.Invoke(from, insertDataIndex);
            }

            GUI.changed = true;
            e.Use();
        }

        private void DrawReorderIndicator(float innerW, List<int> filteredIndices)
        {
            // _reorderInsertIndex 是"数据索引"；要画线，需要换成"可见行"里最近的分隔位置
            var insertVi = filteredIndices.Count;
            for (var vi = 0; vi < filteredIndices.Count; vi++)
            {
                if (filteredIndices[vi] >= _reorderInsertIndex)
                {
                    insertVi = vi;
                    break;
                }
            }

            var y = insertVi * RowHeight;
            EditorGUI.DrawRect(new Rect(0f, y - 1f, innerW, 2f), ControlsToolbar.DropIndicatorColor);
        }
    }
}
#endif