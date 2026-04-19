#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using DevWorkbench;

namespace DevWorkbench.Editor
{

/// <summary>
/// IMGUI 列表控件，自带边框、工具栏（搜索 + 自定义按钮）、行选择 / 重命名 / 删除 / 外部拖放 / 内部拖动换位。
/// <para>通过 partial class 拆分为 Main（状态）、API（公开接口）、Rendering（绘制 + reorder 交互）、Search（搜索 + glob）。</para>
/// </summary>
public sealed partial class ListView
{
    // ── Properties ──────────────────────────────────────────────────────

    /// <summary>工具栏左侧显示的标题，为 null 或空则不显示。</summary>
    public string Title { get; set; }

    /// <summary>是否允许 F2 / 右键重命名行。</summary>
    public bool CanRename { get; set; } = true;

    /// <summary>是否允许 Delete / 右键删除行。</summary>
    public bool CanDelete { get; set; } = true;

    /// <summary>是否接受外部拖放到行上。</summary>
    public bool CanReceiveDrop { get; set; }

    /// <summary>是否允许按住左键拖动行内部换位。</summary>
    public bool CanReorder { get; set; }

    /// <summary>忽略的行名（支持 glob，如 <c>*Config</c>）。匹配到的项不会在列表中显示。</summary>
    public List<string> IgnoredNames { get; set; } = new();

    /// <summary>当前选中行索引（-1 表示无选中）。</summary>
    public int SelectedIndex { get; private set; } = -1;

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

    /// <summary>行被点击选中时触发，参数：(index, label)。</summary>
    public void OnRowSelected(Action<int, string> callback) =>
        _onRowSelected = callback;

    /// <summary>行被重命名后触发，参数：(index, oldName, newName)。</summary>
    public void OnRowRenamed(Action<int, string, string> callback) =>
        _onRowRenamed = callback;

    /// <summary>行被删除时触发，参数：(index, label)。</summary>
    public void OnRowDeleted(Action<int, string> callback) =>
        _onRowDeleted = callback;

    /// <summary>外部对象拖放到行上时触发，参数：(targetIndex)。</summary>
    public void OnDropOnRow(Action<int> callback) =>
        _onDropOnRow = callback;

    /// <summary>
    /// 行内部拖动换位时触发，参数：(fromDataIndex, toDataIndex)。
    /// <para><c>toDataIndex</c> 采用 <see cref="List{T}.Insert"/> 语义，取值范围 <c>[0, items.Count]</c>，
    /// 表示"先从原位置移除，再插入到该索引之前"。</para>
    /// </summary>
    public void OnRowReordered(Action<int, int> callback) =>
        _onRowReordered = callback;

    /// <summary>工具栏内置 "+" 按钮被点击时触发。</summary>
    public void OnAddClicked(Action callback) =>
        _onAddClicked = callback;

    // ── Toolbar buttons ─────────────────────────────────────────────────

    /// <summary>在工具栏右侧追加一个自定义按钮（显示在 "+" 左边）。</summary>
    public void AddToolbarButton(GUIContent content, Action onClick, float width = 22f) =>
        _toolbarButtons.Add(new ToolbarButtonDef(content, onClick, width));

    /// <summary>移除所有自定义工具栏按钮。</summary>
    public void ClearToolbarButtons() => _toolbarButtons.Clear();

    // ── Draw ────────────────────────────────────────────────────────────

    /// <summary>
    /// 在给定 Rect 内绘制完整的 ListView（边框 → 工具栏 → 列表体）。
    /// 每帧调用一次。
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
