#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    public sealed partial class TreeView
    {
        private string _renamingPath;
        private string _renameBuffer;
        private bool _renameFocusRequest;
        private bool _renameHadFocus;
        private int _renameGeneration;
        // 每次 BeginRename 递增 generation，保证 TextField 控件名唯一，避免 Unity 内部状态污染
        private string RenameCtrl => $"{RenameControlName}_{_renameGeneration}";

        private string _dragSourcePath;
        private string _dropFolderPath;
        private int _dropLineRow = -1;
        private int _dropLineDepth;
        private string _dropLineParentPath;
        private string _dragHoverPath;
        private double _dragHoverStartTime;

        private void CheckRenameBlur()
        {
            if (_renamingPath == null || !_renameHadFocus) return;
            if (Event.current.type == EventType.Layout) return;
            if (GUI.GetNameOfFocusedControl() != RenameCtrl)
                CommitRename(true);
        }

        private void HandleKeyboard()
        {
            if (Event.current.type != EventType.KeyDown) return;

            if (_renamingPath != null)
            {
                if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                { CommitRename(true); Event.current.Use(); }
                else if (Event.current.keyCode == KeyCode.Escape)
                { CommitRename(false); Event.current.Use(); }
                return;
            }

            if (string.IsNullOrEmpty(_selectedPathBacking)) return;
            var node = FindNodeByPath(_selectedPathBacking);
            if (node == null) return;

            if (Event.current.keyCode == KeyCode.F2 && CanRename && CanOperateNode(node))
            { BeginRename(node); Event.current.Use(); }
            else if (Event.current.keyCode == KeyCode.Delete && CanDelete && CanOperateNode(node))
            { ExecuteDelete(node); Event.current.Use(); }
        }

        private void HandleRowInput(Rect rowRect, Rect arrowRect, FlatNode flat)
        {
            var node = flat.Node;
            var e = Event.current;

            if (e.type == EventType.ContextClick && rowRect.Contains(e.mousePosition))
            {
                if (!string.Equals(_selectedPathBacking, node.FullPath, StringComparison.OrdinalIgnoreCase))
                {
                    CommitRename(true);
                    _selectedPathBacking = node.FullPath;
                    _onNodeSelected?.Invoke(_selectedPathBacking);
                }
                _pendingContextNode = node;
                GUI.changed = true;
                e.Use();
                return;
            }

            if (arrowRect.Contains(e.mousePosition)) return;

            if (e.type == EventType.MouseDown && e.button == 0 && rowRect.Contains(e.mousePosition))
            {
                if (_renamingPath != null &&
                    !string.Equals(_renamingPath, node.FullPath, StringComparison.OrdinalIgnoreCase))
                    CommitRename(true);

                _selectedPathBacking = node.FullPath;
                _onNodeSelected?.Invoke(_selectedPathBacking);
                GUI.changed = true;

                if (node.Kind != NodeKind.Root)
                {
                    bool isFolder = node.Kind is NodeKind.Branch or NodeKind.FolderLeaf or NodeKind.ReadOnlyFolder;
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(node.FullPath);
                    if (obj != null) EditorGUIUtility.PingObject(obj);

                    if (e.clickCount == 2)
                    {
                        if (isFolder && node.Children != null && node.Children.Count > 0)
                            node.IsExpanded = !node.IsExpanded;
                        else if (!isFolder && obj != null)
                            AssetDatabase.OpenAsset(obj);
                    }
                }

                if (CanDrag && CanOperateNode(node))
                    _dragSourcePath = node.FullPath;

                e.Use();
            }

            if (e.type == EventType.MouseDrag && e.button == 0 &&
                string.Equals(_dragSourcePath, node.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.paths = new[] { _dragSourcePath };
                DragAndDrop.objectReferences = Array.Empty<UnityEngine.Object>();
                DragAndDrop.StartDrag(Path.GetFileName(_dragSourcePath));
                _dragSourcePath = null;
                e.Use();
            }

            if (e.type == EventType.MouseUp) _dragSourcePath = null;
        }

        private void HandleGlobalDragEvents(Rect contentRect, List<FlatNode> flatList)
        {
            var e = Event.current;

            if (e.type == EventType.DragExited)
            { ClearDropState(true); return; }

            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;
            if (!string.IsNullOrEmpty(_searchNormalized))
            { DragAndDrop.visualMode = DragAndDropVisualMode.Rejected; return; }
            if (DragAndDrop.paths == null || DragAndDrop.paths.Length == 0) return;

            var sourcePath = NormalizePath(DragAndDrop.paths[0]);

            if (!contentRect.Contains(e.mousePosition))
            {
                ClearDropState(false);
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                return;
            }

            var localY = e.mousePosition.y - contentRect.y + _scrollPos.y;
            var idx = Mathf.FloorToInt(localY / RowHeight);
            var yInRow = localY - idx * RowHeight;

            ComputeDropState(flatList, sourcePath, idx, yInRow);

            bool valid = _dropFolderPath != null || _dropLineRow >= 0;
            DragAndDrop.visualMode = valid ? DragAndDropVisualMode.Move : DragAndDropVisualMode.Rejected;
            GUI.changed = true;

            if (e.type == EventType.DragPerform && valid)
            {
                DragAndDrop.AcceptDrag();
                var targetFolder = _dropFolderPath ?? _dropLineParentPath;
                if (!string.IsNullOrEmpty(targetFolder))
                    ExecuteMove(sourcePath, targetFolder);
                ClearDropState(true);
                e.Use();
            }
            else if (e.type == EventType.DragUpdated)
            {
                e.Use();
            }
        }

        private void ComputeDropState(List<FlatNode> flatList, string sourcePath, int idx, float yInRow)
        {
            _dropFolderPath = null;
            _dropLineRow = -1;
            _dropLineParentPath = null;

            if (idx < 0) return;

            // 鼠标在所有行下方 → 插入到 Root 末尾
            if (idx >= flatList.Count)
            {
                if (_root != null && CanDropToNode(_root, sourcePath))
                {
                    _dropLineRow = flatList.Count;
                    _dropLineDepth = 1;
                    _dropLineParentPath = _root.FullPath;
                }
                return;
            }

            var flat = flatList[idx];
            var node = flat.Node;
            bool isFolder = node.Kind == NodeKind.Root || node.Kind == NodeKind.Branch;
            bool canDropInto = isFolder && CanDropToNode(node, sourcePath);
            float upperZone = RowHeight * 0.25f;

            if (isFolder)
            {
                if (yInRow < upperZone)
                {
                    // 文件夹上 25%：插入到该文件夹上方（放入其父节点）
                    TrySetInsertLine(flatList, sourcePath, idx, flat.Depth, node.Parent ?? _root);
                }
                else if (!node.IsExpanded && yInRow > RowHeight - upperZone)
                {
                    // 折叠文件夹下 25%：插入到该文件夹下方
                    TrySetInsertLine(flatList, sourcePath, idx + 1, flat.Depth, node.Parent ?? _root);
                }
                else if (canDropInto)
                {
                    _dropFolderPath = node.FullPath;
                    UpdateHoverExpand(node);
                }
                else
                {
                    // 文件夹不可接受，回退为插入线
                    if (yInRow < RowHeight * 0.5f)
                        TrySetInsertLine(flatList, sourcePath, idx, flat.Depth, node.Parent ?? _root);
                    else
                        TrySetInsertLine(flatList, sourcePath, idx + 1, flat.Depth, node.Parent ?? _root);
                }
            }
            else
            {
                if (yInRow < RowHeight * 0.5f)
                    TrySetInsertLine(flatList, sourcePath, idx, flat.Depth, node.Parent ?? _root);
                else
                    TrySetInsertLine(flatList, sourcePath, idx + 1, flat.Depth, node.Parent ?? _root);
            }
        }

        private void TrySetInsertLine(List<FlatNode> flatList, string sourcePath, int lineRow, int depth, TreeNode parentNode)
        {
            if (parentNode == null || !CanDropToNode(parentNode, sourcePath)) return;
            _dropLineRow = Mathf.Min(lineRow, flatList.Count);
            _dropLineDepth = depth;
            _dropLineParentPath = parentNode.FullPath;
        }

        private void ClearDropState(bool clearHover)
        {
            _dropFolderPath = null;
            _dropLineRow = -1;
            _dropLineDepth = 0;
            _dropLineParentPath = null;
            if (clearHover)
                _dragHoverPath = null;
        }

        private bool CanDropToNode(TreeNode target, string sourcePath)
        {
            if (target == null) return false;
            if (target.Kind == NodeKind.FolderLeaf ||
                target.Kind == NodeKind.FileLeaf ||
                target.Kind == NodeKind.ReadOnlyFile ||
                target.Kind == NodeKind.ReadOnlyFolder) return false;

            var tgt = target.FullPath;
            var src = NormalizePath(sourcePath);
            if (string.Equals(src, tgt, StringComparison.OrdinalIgnoreCase)) return false;
            if (tgt.StartsWith(src + "/", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private void UpdateHoverExpand(TreeNode node)
        {
            if (node.Kind != NodeKind.Branch && node.Kind != NodeKind.Root) return;
            if (node.IsExpanded) { _dragHoverPath = null; return; }

            if (!string.Equals(_dragHoverPath, node.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                _dragHoverPath = node.FullPath;
                _dragHoverStartTime = EditorApplication.timeSinceStartup;
                return;
            }

            if (EditorApplication.timeSinceStartup - _dragHoverStartTime >= DragHoverExpandDelay)
            {
                node.IsExpanded = true;
                _dragHoverPath = null;
                GUI.changed = true;
            }
        }

        private void BeginRename(TreeNode node)
        {
            GUI.FocusControl(string.Empty);
            GUIUtility.keyboardControl = 0;
            EditorGUIUtility.editingTextField = false;
            _renameGeneration++;
            _renamingPath = node.FullPath;
            _renameBuffer = node.Name;
            _renameFocusRequest = true;
            _renameHadFocus = false;
        }

        private void CommitRename(bool accept)
        {
            var path = _renamingPath;
            var buffer = _renameBuffer;
            _renamingPath = null;
            _renameBuffer = null;
            _renameFocusRequest = false;
            _renameHadFocus = false;
            if (!accept || path == null) return;
            var node = FindNodeByPath(path);
            if (node != null) ExecuteRename(node, buffer);
        }

        private void ShowContextMenu(TreeNode node)
        {
            var menu = new GenericMenu();

            switch (node.Kind)
            {
                case NodeKind.Root:
                    break;

                case NodeKind.Branch:
                    if (CanRename)
                        menu.AddItem(new GUIContent(_renameLabel), false, () => BeginRename(node));
                    else
                        menu.AddDisabledItem(new GUIContent(_renameLabel));

                    if (CanDelete && !HasAnyLeafDescendant(node))
                        menu.AddItem(new GUIContent(_deleteLabel), false, () => ExecuteDelete(node));
                    break;

                case NodeKind.FolderLeaf:
                case NodeKind.FileLeaf:
                    if (CanRename)
                        menu.AddItem(new GUIContent(_renameLabel), false, () => BeginRename(node));
                    else
                        menu.AddDisabledItem(new GUIContent(_renameLabel));

                    if (CanDelete)
                        menu.AddItem(new GUIContent(_deleteLabel), false, () => ExecuteDelete(node));
                    else
                        menu.AddDisabledItem(new GUIContent(_deleteLabel));
                    break;
            }

            if (menu.GetItemCount() > 0)
                menu.ShowAsContext();
        }
    }
}
#endif