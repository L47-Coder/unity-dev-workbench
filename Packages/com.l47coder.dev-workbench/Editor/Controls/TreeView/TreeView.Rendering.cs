#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    public sealed partial class TreeView
    {
        // ── Toolbar drawing ─────────────────────────────────────────────────

        private void DrawToolbar(Rect toolbarRect)
        {
            ControlsToolbar.DrawToolbarSeparator(toolbarRect);

            var pad = toolbarRect.height - ControlsToolbar.ToolbarSeparatorHeight - ControlsToolbar.SearchFieldHeight;
            var left = toolbarRect.x;
            var right = toolbarRect.xMax - pad;

            // ── Built-in "+" button (right-aligned) ─────────────────────────
            var addSize = ControlsToolbar.SearchFieldHeight;
            var addY = toolbarRect.y + Mathf.Max(0f, pad - 0.5f);
            right -= addSize;
            if (GUI.Button(new Rect(right, addY, addSize, addSize), "+", ControlsToolbar.ButtonStyle))
                _onAddClicked?.Invoke();
            right -= pad;

            // ── Search bar (fills remaining space) ──────────────────────────
            var searchW = Mathf.Max(right - left, 20f);
            DrawSearchBar(new Rect(left, toolbarRect.y, searchW, toolbarRect.height));
        }

        // ── Row styles ──────────────────────────────────────────────────────

        private static GUIStyle _labelStyle;
        private static GUIStyle _labelStyleBold;
        private static GUIStyle _foldoutStyle;

        private static GUIStyle LabelStyle =>
            _labelStyle ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft };

        private static GUIStyle LabelStyleBold =>
            _labelStyleBold ??= new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft };

        private static GUIStyle FoldoutStyle =>
            _foldoutStyle ??= new GUIStyle(EditorStyles.foldout);

        private static Color NodeTextColor(NodeKind kind) => kind switch
        {
            NodeKind.Root => EditorGUIUtility.isProSkin
                ? new Color(1.00f, 1.00f, 1.00f, 1f)
                : new Color(0.08f, 0.08f, 0.08f, 1f),
            NodeKind.Branch => EditorGUIUtility.isProSkin
                ? new Color(1.00f, 0.72f, 0.30f, 1f)
                : new Color(0.70f, 0.36f, 0.00f, 1f),
            NodeKind.FolderLeaf or NodeKind.FileLeaf => EditorGUIUtility.isProSkin
                ? new Color(0.55f, 0.90f, 0.55f, 1f)
                : new Color(0.10f, 0.50f, 0.12f, 1f),
            NodeKind.ReadOnlyFile or NodeKind.ReadOnlyFolder => EditorGUIUtility.isProSkin
                ? new Color(0.65f, 0.65f, 0.65f, 0.50f)
                : new Color(0.40f, 0.40f, 0.40f, 0.50f),
            _ => EditorGUIUtility.isProSkin
                ? new Color(0.78f, 0.78f, 0.78f, 1f)
                : new Color(0.22f, 0.22f, 0.22f, 1f),
        };

        private static Color RowBg(int index, bool selected, bool dropFolder = false)
        {
            if (selected)
                return EditorGUIUtility.isProSkin
                    ? new Color(0.17f, 0.36f, 0.53f, 1f)
                    : new Color(0.24f, 0.49f, 0.91f, 1f);
            if (dropFolder)
                return EditorGUIUtility.isProSkin
                    ? new Color(0.20f, 0.40f, 0.62f, 0.55f)
                    : new Color(0.24f, 0.49f, 0.91f, 0.30f);
            return index % 2 == 0
                ? (EditorGUIUtility.isProSkin ? new Color(0.19f, 0.19f, 0.19f, 1f) : new Color(0.84f, 0.84f, 0.84f, 1f))
                : (EditorGUIUtility.isProSkin ? new Color(0.17f, 0.17f, 0.17f, 1f) : new Color(0.78f, 0.78f, 0.78f, 1f));
        }

        // ── Body drawing ────────────────────────────────────────────────────

        private void DrawBody(Rect bodyRect, List<FlatNode> flatList)
        {
            var totalHeight = flatList.Count * RowHeight;
            var needScroll = totalHeight > bodyRect.height;
            var innerWidth = Mathf.Max(bodyRect.width - (needScroll ? ControlsToolbar.VerticalScrollbarWidth : 0f), 1f);
            var viewRect = new Rect(0f, 0f, innerWidth, Mathf.Max(totalHeight, bodyRect.height));

            _scrollPos = GUI.BeginScrollView(bodyRect, _scrollPos, viewRect);
            for (var i = 0; i < flatList.Count; i++)
                DrawRow(new Rect(0f, i * RowHeight, innerWidth, RowHeight), flatList[i], i);

            if (_renamingPath != null &&
                Event.current.type == EventType.MouseDown &&
                Event.current.button == 0)
            {
                var clickedRenameRow = false;
                for (var i = 0; i < flatList.Count; i++)
                {
                    if (!string.Equals(flatList[i].Node.FullPath, _renamingPath,
                            StringComparison.OrdinalIgnoreCase)) continue;
                    var rowY = i * RowHeight;
                    clickedRenameRow = Event.current.mousePosition.y >= rowY &&
                                       Event.current.mousePosition.y < rowY + RowHeight;
                    break;
                }
                if (!clickedRenameRow)
                    CommitRename(true);
            }

            if (_dropLineRow >= 0 && Event.current.type == EventType.Repaint)
            {
                var lineColor = ControlsToolbar.DropIndicatorColor;
                const float lineH = 1.5f;
                const float tickH = 6f;
                const float tickW = 1.5f;
                var lineX = _dropLineDepth * IndentWidth + ArrowWidth;
                var lineY = _dropLineRow * RowHeight - lineH * 0.5f;
                var lineW = Mathf.Max(0f, innerWidth - lineX - tickW);

                EditorGUI.DrawRect(new Rect(lineX, lineY - (tickH - lineH) * 0.5f, tickW, tickH), lineColor);
                EditorGUI.DrawRect(new Rect(lineX + tickW, lineY, lineW, lineH), lineColor);
            }

            GUI.EndScrollView();
        }

        // ── Row drawing ─────────────────────────────────────────────────────

        private void DrawRow(Rect rowRect, FlatNode flat, int rowIndex)
        {
            var node = flat.Node;
            var isSelected = string.Equals(node.FullPath, _selectedPathBacking, StringComparison.OrdinalIgnoreCase);
            var isDropFolder = string.Equals(node.FullPath, _dropFolderPath, StringComparison.OrdinalIgnoreCase);

            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rowRect, RowBg(rowIndex, isSelected, isDropFolder));

            var x = rowRect.x + flat.Depth * IndentWidth;

            var arrowRect = new Rect(x, rowRect.y, ArrowWidth, RowHeight);
            DrawExpandArrow(arrowRect, node);
            x += ArrowWidth;

            var iconRect = new Rect(x, rowRect.y + (RowHeight - IconSize) * 0.5f, IconSize, IconSize);
            DrawNodeIcon(iconRect, node);
            x += IconSize + 2f;

            var labelRect = new Rect(x, rowRect.y, Mathf.Max(0f, rowRect.xMax - x - 2f), RowHeight);
            var isRenaming = _renamingPath != null &&
                             string.Equals(node.FullPath, _renamingPath, StringComparison.OrdinalIgnoreCase);
            if (isRenaming)
            {
                DrawRenameField(labelRect);
            }
            else
            {
                var isSearching = !string.IsNullOrEmpty(_searchNormalized);
                var displayName = node.Kind == NodeKind.Root && isSearching ? "Search results" : node.Name;

                var prevColor = GUI.contentColor;
                GUI.contentColor = isSelected ? new Color(1f, 1f, 1f, 1f) : NodeTextColor(node.Kind);
                GUI.Label(labelRect, displayName, node.Kind == NodeKind.Root ? LabelStyleBold : LabelStyle);
                GUI.contentColor = prevColor;
            }

            if (!isRenaming)
                HandleRowInput(rowRect, arrowRect, flat);
        }

        private static void DrawExpandArrow(Rect arrowRect, TreeNode node)
        {
            if (node.Children == null || node.Children.Count == 0) return;

            var newExpanded = EditorGUI.Foldout(arrowRect, node.IsExpanded, GUIContent.none, true, FoldoutStyle);
            if (newExpanded == node.IsExpanded) return;
            node.IsExpanded = newExpanded;
            GUI.changed = true;
        }

        private static void DrawNodeIcon(Rect rect, TreeNode node)
        {
            if (Event.current.type != EventType.Repaint) return;

            Texture icon = node.Kind switch
            {
                NodeKind.Root => EditorGUIUtility.IconContent("FolderOpened Icon").image,
                _ => AssetDatabase.GetCachedIcon(node.FullPath)
                     ?? EditorGUIUtility.IconContent(
                         node.Kind is NodeKind.Branch or NodeKind.ReadOnlyFolder
                             ? "Folder Icon"
                             : "DefaultAsset Icon").image
            };

            if (icon != null)
                GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit);
        }

        private void DrawRenameField(Rect rect)
        {
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
    }
}
#endif