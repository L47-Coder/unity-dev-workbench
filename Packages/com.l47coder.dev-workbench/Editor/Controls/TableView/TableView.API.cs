#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DevWorkbench;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    public sealed partial class TableView
    {
        public bool CanAdd { get; set; } = true;
        public bool CanRemove { get; set; } = true;
        public bool CanDrag { get; set; } = true;
        public bool CanSelect { get; set; } = true;
        public bool CanSearch { get; set; } = true;
        public string SearchField { get; set; } = "Key";

        public void OnRowSelected<T>(Action<int, T> callback) where T : ITableViewItem =>
            _onRowSelected = callback != null ? (i, obj) => callback(i, (T)obj) : null;

        public void OnRowChanged<T>(Action<int, T> callback) where T : ITableViewItem =>
            _onChange = callback != null ? (i, obj) => callback(i, (T)obj) : null;

        public void Draw<T>(Rect rect, List<T> list) where T : ITableViewItem
        {
            if (_columns == null)
                _columns = BuildColumnsFromElementType(typeof(T));

            ConsumePendingDirty();

            var evt = Event.current;
            if (evt.type == EventType.MouseDown && !rect.Contains(evt.mousePosition))
                GUIUtility.keyboardControl = 0;

            var boxRect = BoxDrawer.CalcBoxRect(rect);
            if (boxRect.width < 1f || boxRect.height < 1f) return;

            BoxDrawer.DrawBox(boxRect);
            var contentRect = BoxDrawer.CalcContentRect(boxRect);

            GUI.BeginGroup(contentRect);

            var toolbarHeight = CanSearch ? ControlsToolbar.ToolbarHeight : 0f;
            if (CanSearch)
                DrawToolbar(new Rect(0f, 0f, contentRect.width, toolbarHeight));
            else
                _searchText = string.Empty;

            var headerHeight = EditorGUIUtility.singleLineHeight + CellPadding * 2f;
            var bodyAvailH = Mathf.Max(0f, contentRect.height - toolbarHeight - headerHeight);
            var filteredIndices = GetFilteredIndices(list);
            var totalRowsH = filteredIndices.Count * ComputeRowHeight();

            var vScrollW = ControlsToolbar.VerticalScrollbarWidth;
            var probeLayout = BuildLayout(contentRect.width);
            var effectiveBodyH = bodyAvailH - (probeLayout.NeedsHorizontalScroll ? ControlsToolbar.HorizontalScrollbarHeight : 0f);
            var needVScroll = totalRowsH > effectiveBodyH;

            var viewWidth = Mathf.Max(120f, contentRect.width - (needVScroll ? vScrollW : 0f));
            var layout = needVScroll ? BuildLayout(viewWidth) : probeLayout;

            var headerRect = new Rect(0f, toolbarHeight, viewWidth, headerHeight);
            DrawHeader(headerRect, list, layout);

            if (needVScroll)
                PaintCellFrame(new Rect(viewWidth, toolbarHeight, vScrollW, headerHeight),
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
