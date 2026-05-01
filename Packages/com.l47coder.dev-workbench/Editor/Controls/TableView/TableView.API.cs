#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    /// <summary>
    /// IMGUI table control with its own border, toolbar (search + refresh / view-script
    /// buttons), column headers, inline row editing and drag-to-reorder.
    /// <para>The implementation is split across partial files: State, API, Rendering, Search,
    /// Layout, Columns and Reorder.</para>
    /// </summary>
    public sealed partial class TableView
    {
        // ── Properties ──────────────────────────────────────────────────────

        /// <summary>Whether rows can be reordered by dragging.</summary>
        public bool CanDrag { get; set; } = true;

        /// <summary>Whether new rows can be added from the toolbar.</summary>
        public bool CanAdd { get; set; } = true;

        /// <summary>Whether rows can be removed.</summary>
        public bool CanRemove { get; set; } = true;

        /// <summary>Whether clicking the index column selects the row.</summary>
        public bool CanSelect { get; set; } = true;

        /// <summary>Whether to show the refresh / view-script buttons on the right of the toolbar.</summary>
        public bool ShowToolbarButtons { get; set; } = true;

        /// <summary>Field (field name or column header) used for searching. Defaults to "Key".</summary>
        public string SearchField { get; set; } = "Key";

        /// <summary>Index of the currently selected row (-1 if none).</summary>
        public int SelectedIndex
        {
            get => _selectedIndex;
            set => _selectedIndex = value;
        }

        /// <summary>Placeholder text for the search field. Defaults to "Search...".</summary>
        public string SearchPlaceholder
        {
            get => _searchPlaceholder;
            set => _searchPlaceholder = value ?? "Search...";
        }

        /// <summary>Label of the "Copy" entry in the context menu. Defaults to "Copy".</summary>
        public string CopyLabel
        {
            get => _copyLabel;
            set => _copyLabel = value ?? "Copy";
        }

        // ── Event subscriptions ─────────────────────────────────────────────

        /// <summary>Fires when row data changes. Arguments: (index, item).</summary>
        public void OnRowChanged<T>(Action<int, T> callback) =>
            _onChange = callback != null ? (i, obj) => callback(i, (T)obj) : null;

        /// <summary>Fires when a row is selected. Arguments: (index, item).</summary>
        public void OnRowSelected<T>(Action<int, T> callback) =>
            _onRowSelected = callback != null ? (i, obj) => callback(i, (T)obj) : null;

        /// <summary>Fires when the toolbar "refresh" button is clicked.</summary>
        public void OnRefreshClicked(Action callback) =>
            _onRefreshClicked = callback;

        /// <summary>Fires when the toolbar "view refresher" button is clicked.</summary>
        public void OnViewRefresherClicked(Action callback) =>
            _onViewRefresherClicked = callback;

        /// <summary>
        /// 追加一个按钮列（显示在所有数据列右侧、"−" 删除列之前）。
        /// 首次调用 <see cref="Draw{T}"/> 时自动合并进列定义。
        /// </summary>
        /// <param name="header">列头文字。</param>
        /// <param name="buttonLabel">每行按钮的文字。</param>
        /// <param name="width">列宽（像素）。</param>
        /// <param name="callback">点击时回调，参数为行数据索引。</param>
        public void AddButtonColumn(string header, string buttonLabel, float width, Action<int> callback)
        {
            _pendingButtonColumns.Add(new ColumnDefinition(header, buttonLabel, width, callback));
            _columns = null; // 强制在下次 Draw 时重建列
            _columnMinWidths = null;
            _columnPreferredWidths = null;
        }

        /// <summary>清空所有已添加的按钮列。</summary>
        public void ClearButtonColumns()
        {
            _pendingButtonColumns.Clear();
            _columns = null;
            _columnMinWidths = null;
            _columnPreferredWidths = null;
        }

        // ── Draw ────────────────────────────────────────────────────────────

        /// <summary>
        /// Draw the complete TableView inside the given rect (border → toolbar → header → body).
        /// Call once per frame.
        /// </summary>
        public void Draw<T>(Rect rect, List<T> list)
        {
            if (_columns == null)
            {
                _columns = BuildColumnsFromElementType(typeof(T));
                if (_pendingButtonColumns.Count > 0)
                    _columns.AddRange(_pendingButtonColumns);
            }
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
}
#endif
