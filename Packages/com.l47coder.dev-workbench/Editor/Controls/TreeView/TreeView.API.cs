#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevWorkbench.Editor
{
    /// <summary>
    /// IMGUI tree-view control with its own border, toolbar (search + add button), file-system
    /// backing, drag-and-drop, rename and delete support.
    /// <para>The implementation is split across several partial files: Main (state), API
    /// (public surface), Rendering (drawing), Search, Interaction, and FileSystem (tree
    /// construction).</para>
    /// </summary>
    public sealed partial class TreeView
    {
        // ── Properties ──────────────────────────────────────────────────────

        /// <summary>Whether subfolders can be created inside a folder.</summary>
        public bool CanCreate { get; set; } = true;

        /// <summary>Whether nodes can be deleted.</summary>
        public bool CanDelete { get; set; } = true;

        /// <summary>Whether nodes can be renamed.</summary>
        public bool CanRename { get; set; } = true;

        /// <summary>Whether nodes can be drag-and-dropped.</summary>
        public bool CanDrag { get; set; } = true;

        /// <summary>File / folder names to ignore (glob patterns supported).</summary>
        public List<string> IgnoredNames { get; set; } = new();

        /// <summary>Asset path of the currently selected node (null if nothing is selected).</summary>
        public string SelectedPath => _selectedPathBacking;

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

        /// <summary>Fires when a node is selected. Argument: (assetPath).</summary>
        public void OnNodeSelected(Action<string> callback) =>
            _onNodeSelected = callback;

        /// <summary>Fires after a new folder is created. Argument: (newFolderPath).</summary>
        public void OnNodeCreated(Action<string> callback) =>
            _onNodeCreated = callback;

        /// <summary>Fires after a node is renamed. Arguments: (oldPath, newPath).</summary>
        public void OnNodeRenamed(Action<string, string> callback) =>
            _onNodeRenamed = callback;

        /// <summary>Fires after a node is deleted. Argument: (deletedPath).</summary>
        public void OnNodeDeleted(Action<string> callback) =>
            _onNodeDeleted = callback;

        /// <summary>Fires after a node is moved. Arguments: (oldPath, newPath).</summary>
        public void OnNodeMoved(Action<string, string> callback) =>
            _onNodeMoved = callback;

        /// <summary>Fires when the built-in "+" button on the toolbar is clicked.</summary>
        public void OnAddClicked(Action callback) =>
            _onAddClicked = callback;

        /// <summary>
        /// Create a subfolder inside the currently selected folder (Root or Branch).
        /// If nothing is selected, or the selection is a leaf / read-only node, the search
        /// walks upwards until it finds a valid parent.
        /// </summary>
        public void CreateFolderAtSelected()
        {
            if (!CanCreate || _root == null) return;

            TreeNode target;
            if (string.IsNullOrEmpty(_selectedPathBacking))
            {
                target = _root;
            }
            else
            {
                target = FindNodeByPath(_selectedPathBacking);
                while (target != null && target.Kind is not (NodeKind.Root or NodeKind.Branch))
                    target = target.Parent;
            }

            if (target != null)
                ExecuteCreateFolder(target);
        }

        // ── Draw ────────────────────────────────────────────────────────────

        /// <summary>
        /// Draw the complete TreeView inside the given rect (border → toolbar → body).
        /// Call once per frame.
        /// </summary>
        public void Draw(Rect rect, string path)
        {
            if (_pendingContextNode != null)
            {
                var node = _pendingContextNode;
                _pendingContextNode = null;
                ShowContextMenu(node);
            }

            CheckRenameBlur();
            HandleKeyboard();

            if (_root == null || NormalizePath(path) != _cachedRootPath)
                RebuildTree(NormalizePath(path));

            var boxRect = BoxDrawer.CalcBoxRect(rect);
            if (boxRect.width < 1f || boxRect.height < 1f) return;

            BoxDrawer.DrawBox(boxRect);
            var contentRect = BoxDrawer.CalcContentRect(boxRect);

            GUI.BeginGroup(contentRect);

            DrawToolbar(new Rect(0f, 0f, contentRect.width, ControlsToolbar.ToolbarHeight));

            var bodyRect = new Rect(
                0f, ControlsToolbar.ToolbarHeight,
                contentRect.width, Mathf.Max(0f, contentRect.height - ControlsToolbar.ToolbarHeight));

            _flatList = BuildFlatList();
            HandleGlobalDragEvents(bodyRect, _flatList);
            DrawBody(bodyRect, _flatList);

            GUI.EndGroup();
        }
    }
}
#endif
