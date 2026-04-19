#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DevWorkbench;

namespace DevWorkbench.Editor
{

public sealed partial class TableView
{
    // ── Layout constants ────────────────────────────────────────────────

    private const float GripCellWidth = 22f;
    private const float IndexColumnWidth = 28f;
    private const float RowButtonWidth = 24f;
    private const float CellPadding = 4f;
    private const float GridThickness = 1f;
    private const float DefaultFallbackMinWidth = 80f;

    // 滚动条实际尺寸统一从 ControlsToolbar 读取，避免各控件硬编码不一致。

    // ── Reorder animation constants ─────────────────────────────────────

    private const float RowMoveSmoothTime = 0.1f;
    private const float GapMoveSmoothTime = 0.09f;
    private const float DragRowSmoothTime = 0.065f;

    private static readonly int RowReorderControlHintHash = "TableListAttribute.Row".GetHashCode();

    // ── Static / shared state ───────────────────────────────────────────

    private static ReorderSession _reorder;
    private static TableView _draggingOwner;
    private static bool _dragTickHooked;
    private static double _lastUpdateTime;

    private static GUIStyle _headerCellLabelStyleCache;
    private static GUIStyle _bodyIndexLabelStyleCache;

    // ── Instance state ──────────────────────────────────────────────────

    private List<ColumnDefinition> _columns;
    private float[] _columnPreferredWidths;
    private float[] _columnMinWidths;
    private int _resizeColumnIndex = -1;
    private float _resizeStartMouseX;
    private float _resizeStartPreferredWidth;
    private int _selectedIndex = -1;
    private Vector2 _scrollPos;

    // ── Event callbacks ─────────────────────────────────────────────────

    private Action<int, object> _onChange;
    private Action<int, object> _onRowSelected;
    private Action _onRefreshClicked;
    private Action _onViewRefresherClicked;

    // ── Configurable labels ─────────────────────────────────────────────

    private string _searchPlaceholder = "Search...";
    private string _copyLabel = "Copy";

    // ── Internal types ──────────────────────────────────────────────────

    private struct TableLayout
    {
        /// <summary>整张表（固定列 + 数据列）的实际绘制宽度。可能超过视口宽（此时外层出现水平滚动）。</summary>
        public float TotalWidth;
        public float GripWidth;
        public float IndexWidth;
        public float ActionsWidth;
        /// <summary>数据列宽度之和。</summary>
        public float DataColumnsWidth;
        public float[] DataColumnWidths;
        /// <summary>表总宽是否超过了视口宽（意味着需要水平滚动）。</summary>
        public bool NeedsHorizontalScroll;
    }

    private sealed class ReorderSession
    {
        public int ArraySize;
        public int ControlId;
        public int SourceIndex;
        public int InsertSlot;
        public float PickupOffsetY;
        public float SourceRowHeight;
        public float[] RowCurrentY;
        public float[] RowTargetY;
        public float[] RowVelocityY;
        public float GapCurrentY;
        public float GapTargetY;
        public float GapVelocityY;
        public float DragRowY;
        public float DragRowYTarget;
        public float DragRowVelY;
        public float LastBodyTopY;
        public bool HasBodyTopY;
        public bool PositionsInitialized;
    }

    private sealed class VisualRow
    {
        public int RowIndex;
        public int StripeIndex;
        public float DrawY;
        public float Height;
        public bool IsGap;
    }

    private static GUIStyle HeaderCellLabelStyle =>
        _headerCellLabelStyleCache ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };

    private static GUIStyle BodyIndexLabelStyle =>
        _bodyIndexLabelStyleCache ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };

    private static Color HeaderCellBackground =>
        EditorGUIUtility.isProSkin
            ? new Color(0.24f, 0.26f, 0.29f, 1f)
            : new Color(0.86f, 0.88f, 0.93f, 1f);

    private static Color BodyCellBackground(bool alt) =>
        EditorGUIUtility.isProSkin
            ? (alt ? new Color(0.15f, 0.15f, 0.16f, 1f) : new Color(0.13f, 0.13f, 0.14f, 1f))
            : (alt ? new Color(0.96f, 0.97f, 0.98f, 1f) : new Color(1f, 1f, 1f, 1f));

    private static Color GridLineColor =>
        EditorGUIUtility.isProSkin
            ? new Color(0.22f, 0.22f, 0.24f, 1f)
            : new Color(0.62f, 0.64f, 0.68f, 1f);

    private static Color SelectedCellBackground =>
        EditorGUIUtility.isProSkin
            ? new Color(0.17f, 0.37f, 0.62f, 1f)
            : new Color(0.24f, 0.48f, 0.90f, 1f);
}
#endif
}
