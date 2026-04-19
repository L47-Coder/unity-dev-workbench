#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using DevWorkbench;

namespace DevWorkbench.Editor
{

public sealed partial class TableView
{
    /// <summary>
    /// 计算当前帧的表格布局。规则：
    /// <list type="bullet">
    /// <item>每列先取 <c>base[i] = max(PreferredWidth[i], MinWidth[i])</c>；</item>
    /// <item>若 <c>Σ base &gt;= 可用数据宽</c>：用 base 作为最终列宽，总宽溢出 → 外层水平滚动；</item>
    /// <item>若 <c>Σ base &lt; 可用数据宽</c>：把剩余空间按列数均摊到每列（纯视觉拉伸，不改 Preferred）。</item>
    /// </list>
    /// </summary>
    /// <param name="viewWidth">视口宽度（绘制区域宽度，已扣除竖向滚动条）。</param>
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
            // 拖动列宽期间一律不均摊：让被拖列的右边界精确贴合鼠标。
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
#endif
}
