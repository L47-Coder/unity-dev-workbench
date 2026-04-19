#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using DevWorkbench;

namespace DevWorkbench.Editor
{

/// <summary>
/// IMGUI 树形控件，自带边框、工具栏（搜索 + 添加按钮）、文件系统驱动、拖放 / 重命名 / 删除。
/// <para>通过 partial class 拆分为 Main（状态）、API（公开接口）、Rendering（绘制）、Search（搜索）、
/// Interaction（交互）、FileSystem（文件树构建）。</para>
/// </summary>
public sealed partial class TreeView
{
    // ── Properties ──────────────────────────────────────────────────────

    /// <summary>是否允许在文件夹内创建子文件夹。</summary>
    public bool CanCreate { get; set; } = true;

    /// <summary>是否允许删除节点。</summary>
    public bool CanDelete { get; set; } = true;

    /// <summary>是否允许重命名节点。</summary>
    public bool CanRename { get; set; } = true;

    /// <summary>是否允许拖放节点。</summary>
    public bool CanDrag { get; set; } = true;

    /// <summary>忽略的文件 / 文件夹名（支持 glob）。</summary>
    public List<string> IgnoredNames { get; set; } = new();

    /// <summary>当前选中节点的资产路径（null 表示无选中）。</summary>
    public string SelectedPath => _selectedPathBacking;

    /// <summary>搜索栏占位文本，默认 "搜索..."。</summary>
    public string SearchPlaceholder
    {
        get => _searchPlaceholder;
        set => _searchPlaceholder = value ?? string.Empty;
    }

    /// <summary>右键菜单中"重命名"的文本，默认 "重命名"。</summary>
    public string RenameLabel
    {
        get => _renameLabel;
        set => _renameLabel = value ?? "重命名";
    }

    /// <summary>右键菜单中"删除"的文本，默认 "删除"。</summary>
    public string DeleteLabel
    {
        get => _deleteLabel;
        set => _deleteLabel = value ?? "删除";
    }

    // ── Event subscriptions ─────────────────────────────────────────────

    /// <summary>节点被选中时触发，参数：(assetPath)。</summary>
    public void OnNodeSelected(Action<string> callback) =>
        _onNodeSelected = callback;

    /// <summary>新文件夹被创建后触发，参数：(newFolderPath)。</summary>
    public void OnNodeCreated(Action<string> callback) =>
        _onNodeCreated = callback;

    /// <summary>节点被重命名后触发，参数：(oldPath, newPath)。</summary>
    public void OnNodeRenamed(Action<string, string> callback) =>
        _onNodeRenamed = callback;

    /// <summary>节点被删除后触发，参数：(deletedPath)。</summary>
    public void OnNodeDeleted(Action<string> callback) =>
        _onNodeDeleted = callback;

    /// <summary>节点被移动后触发，参数：(oldPath, newPath)。</summary>
    public void OnNodeMoved(Action<string, string> callback) =>
        _onNodeMoved = callback;

    /// <summary>工具栏内置 "+" 按钮被点击时触发。</summary>
    public void OnAddClicked(Action callback) =>
        _onAddClicked = callback;

    /// <summary>
    /// 在当前选中的文件夹（Root 或 Branch）内创建子文件夹。
    /// 若未选中、或选中的是叶子节点 / 只读节点，则向上寻找。
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
    /// 在给定 Rect 内绘制完整的 TreeView（边框 → 工具栏 → 树体）。
    /// 每帧调用一次。
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
#endif
}
