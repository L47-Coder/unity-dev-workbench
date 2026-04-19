#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DevWorkbench;

namespace DevWorkbench.Editor
{

public sealed partial class TreeView
{
    private const string LeafMarkerFileName = "_leaf.json";
    private const float RowHeight = 20f;
    private const float IndentWidth = 14f;
    private const float ArrowWidth = 16f;
    private const float IconSize = 16f;
    private const float DragHoverExpandDelay = 0.35f;
    private const string RenameControlName = "TreeViewRenameField";

    // ── Configurable labels ─────────────────────────────────────────────

    private string _searchPlaceholder = "搜索...";
    private string _renameLabel = "重命名";
    private string _deleteLabel = "删除";

    // ── Core state ──────────────────────────────────────────────────────

    private string _selectedPathBacking;
    private string _cachedRootPath;
    private TreeNode _root;
    private Vector2 _scrollPos;
    private List<FlatNode> _flatList = new();
    private TreeNode _pendingContextNode;

    private Action<string> _onNodeSelected;
    private Action<string> _onNodeCreated;
    private Action<string, string> _onNodeRenamed;
    private Action<string> _onNodeDeleted;
    private Action<string, string> _onNodeMoved;
    private Action _onAddClicked;

    private static string _projectRoot;
    private static string ProjectRoot =>
        _projectRoot ??= NormalizePath(Path.GetDirectoryName(Application.dataPath));

    private static string NormalizePath(string path) =>
        string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/').TrimEnd('/');

    private static string GetParentPath(string path) =>
        NormalizePath(Path.GetDirectoryName(NormalizePath(path)));

    private static string ToAssetPath(string path)
    {
        var normalized = NormalizePath(path);
        if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Assets", StringComparison.OrdinalIgnoreCase))
            return normalized;

        var root = ProjectRoot;
        if (!string.IsNullOrEmpty(root) &&
            normalized.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
            return normalized[(root.Length + 1)..];

        return normalized;
    }
}
#endif
}
