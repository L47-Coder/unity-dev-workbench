using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DevWorkbench;

namespace DevWorkbench.Editor
{

// TableColumnAttribute 已迁移至 Runtime 程序集（Runtime/Frame/Attribute/TableColumnAttribute.cs）。

#if UNITY_EDITOR
/// <summary>
/// IMGUI 表格控件，自带边框、工具栏（搜索 + 刷新 / 查看按钮）、列头 / 行编辑 / 拖拽排序。
/// <para>通过 partial class 拆分为 State、API、Rendering、Search、Layout、Columns、Reorder。</para>
/// </summary>
public sealed partial class TableView
{
    // ── Properties ──────────────────────────────────────────────────────

    /// <summary>是否允许拖拽排序行。</summary>
    public bool CanDrag { get; set; } = true;

    /// <summary>是否允许在表头添加行。</summary>
    public bool CanAdd { get; set; } = true;

    /// <summary>是否允许删除行。</summary>
    public bool CanRemove { get; set; } = true;

    /// <summary>是否允许点击索引列选中行。</summary>
    public bool CanSelect { get; set; } = true;

    /// <summary>是否显示工具栏右侧的刷新 / 查看脚本按钮。</summary>
    public bool ShowToolbarButtons { get; set; } = true;

    /// <summary>按哪个字段搜索（字段名或列头名），默认 "Key"。</summary>
    public string SearchField { get; set; } = "Key";

    /// <summary>当前选中行索引（-1 表示无选中）。</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => _selectedIndex = value;
    }

    /// <summary>搜索栏占位文本，默认 "搜索..."。</summary>
    public string SearchPlaceholder
    {
        get => _searchPlaceholder;
        set => _searchPlaceholder = value ?? "搜索...";
    }

    /// <summary>右键菜单中"复制"的文本，默认 "复制"。</summary>
    public string CopyLabel
    {
        get => _copyLabel;
        set => _copyLabel = value ?? "复制";
    }

    // ── Event subscriptions ─────────────────────────────────────────────

    /// <summary>行数据变更时触发，参数：(index, item)。</summary>
    public void OnRowChanged<T>(Action<int, T> callback) =>
        _onChange = callback != null ? (i, obj) => callback(i, (T)obj) : null;

    /// <summary>行被选中时触发，参数：(index, item)。</summary>
    public void OnRowSelected<T>(Action<int, T> callback) =>
        _onRowSelected = callback != null ? (i, obj) => callback(i, (T)obj) : null;

    /// <summary>工具栏"刷新"按钮被点击时触发。</summary>
    public void OnRefreshClicked(Action callback) =>
        _onRefreshClicked = callback;

    /// <summary>工具栏"查看刷新脚本"按钮被点击时触发。</summary>
    public void OnViewRefresherClicked(Action callback) =>
        _onViewRefresherClicked = callback;

    // ── Draw ────────────────────────────────────────────────────────────

    /// <summary>
    /// 在给定 Rect 内绘制完整的 TableView（边框 → 工具栏 → 列头 → 表体）。
    /// 每帧调用一次。
    /// </summary>
    public void Draw<T>(Rect rect, List<T> list)
    {
        _columns ??= BuildColumnsFromElementType(typeof(T));
        ConsumePendingDirty();

        var evt = Event.current;
        if (evt.type == EventType.MouseDown && !rect.Contains(evt.mousePosition))
            GUIUtility.keyboardControl = 0;

        var boxRect = BoxDrawer.CalcBoxRect(rect);
        if (boxRect.width < 1f || boxRect.height < 1f) return;

        BoxDrawer.DrawBox(boxRect);
        var contentRect = BoxDrawer.CalcContentRect(boxRect);

        GUI.BeginGroup(contentRect);

        DrawToolbar(new Rect(0f, 0f, contentRect.width, ControlsToolbar.ToolbarHeight), list);

        var headerHeight = EditorGUIUtility.singleLineHeight + CellPadding * 2f;
        var bodyAvailH = Mathf.Max(0f, contentRect.height - ControlsToolbar.ToolbarHeight - headerHeight);
        var filteredIndices = GetFilteredIndices(list);
        var totalRowsH = filteredIndices.Count * ComputeRowHeight();

        // 先以"不预留竖条"的全宽试算一次，得到水平滚动是否需要。
        // 若出现水平条会占掉 body 底部一条高度，必须把这段计入"行区域是否放得下"
        // 的判断，否则会出现"没超高却被迫加竖条"的连锁反应。
        var vScrollW = ControlsToolbar.VerticalScrollbarWidth;
        var probeLayout = BuildLayout(contentRect.width);
        var effectiveBodyH = bodyAvailH - (probeLayout.NeedsHorizontalScroll ? ControlsToolbar.HorizontalScrollbarHeight : 0f);
        var needVScroll = totalRowsH > effectiveBodyH;

        var viewWidth = Mathf.Max(120f, contentRect.width - (needVScroll ? vScrollW : 0f));
        var layout = needVScroll ? BuildLayout(viewWidth) : probeLayout;

        // 表头区域使用视口宽度（水平滚动时内部用无条 ScrollView 与 body 同步）。
        var headerRect = new Rect(0f, ControlsToolbar.ToolbarHeight, viewWidth, headerHeight);
        DrawHeader(headerRect, list, layout);

        // 竖条正上方的"虚拟角"：补一块 header 背景色，避免 header 右端与 body 右端之间
        // 露出一条竖条宽度的外层底色。虚拟角宽度必须与 Unity 实际竖条宽度一致，
        // 否则会啃到 header 最右列的几像素。
        if (needVScroll)
            PaintCellFrame(new Rect(viewWidth, ControlsToolbar.ToolbarHeight, vScrollW, headerHeight),
                HeaderCellBackground, GridLineColor);

        var bodyRect = new Rect(0f, headerRect.yMax, contentRect.width,
            Mathf.Max(0f, contentRect.height - headerRect.yMax));
        DrawRows(bodyRect, list, filteredIndices, layout, viewWidth);

        GUI.EndGroup();

        if (evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition))
        {
            GUIUtility.keyboardControl = 0;
            evt.Use();
        }
    }
}
#endif
}
