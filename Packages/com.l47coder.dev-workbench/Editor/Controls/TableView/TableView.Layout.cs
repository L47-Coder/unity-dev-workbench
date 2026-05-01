#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    public sealed partial class TableView
    {
        private TableLayout BuildLayout(float viewWidth)
        {
            var gripWidth = CanDrag ? GripCellWidth + CellPadding * 2f : 0f;
            var indexWidth = IndexColumnWidth + CellPadding * 2f;
            var actionsWidth = (CanAdd || CanRemove) ? RowButtonWidth + CellPadding * 2f : 0f;
            var fixedTotal = gripWidth + indexWidth + actionsWidth;
            var availableForData = Mathf.Max(10f, viewWidth - fixedTotal);

            EnsureColumnSizing();

            var count = _columns.Count;
            var widths = new float[count];
            var baseTotal = 0f;
            for (var i = 0; i < count; i++)
            {
                var w = Mathf.Max(_columnPreferredWidths[i], _columnMinWidths[i]);
                widths[i] = w;
                baseTotal += w;
            }

            float dataColumnsWidth;
            bool needsHorizontalScroll;
            if (count == 0)
            {
                dataColumnsWidth = availableForData;
                needsHorizontalScroll = false;
            }
            else if (baseTotal >= availableForData || _resizeColumnIndex >= 0)
            {
                dataColumnsWidth = baseTotal;
                needsHorizontalScroll = baseTotal > availableForData + 0.5f;
            }
            else
            {
                var spare = availableForData - baseTotal;
                var perCol = spare / count;
                for (var i = 0; i < count; i++) widths[i] += perCol;
                dataColumnsWidth = availableForData;
                needsHorizontalScroll = false;
            }

            return new TableLayout
            {
                TotalWidth = fixedTotal + dataColumnsWidth,
                GripWidth = gripWidth,
                IndexWidth = indexWidth,
                ActionsWidth = actionsWidth,
                DataColumnsWidth = dataColumnsWidth,
                DataColumnWidths = widths,
                NeedsHorizontalScroll = needsHorizontalScroll,
            };
        }

        private static float ComputeRowHeight() => EditorGUIUtility.singleLineHeight + CellPadding * 2f;

        private static void PaintCellFrame(Rect outer, Color fill, Color grid)
        {
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(outer, fill);
            DrawRectOutline(outer, grid, GridThickness);
        }

        private static void DrawRectOutline(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static Rect PaddedRect(Rect outer) => new(
            outer.x + CellPadding,
            outer.y + CellPadding,
            Mathf.Max(0f, outer.width - CellPadding * 2f),
            Mathf.Max(0f, outer.height - CellPadding * 2f));
    }
}
#endif
