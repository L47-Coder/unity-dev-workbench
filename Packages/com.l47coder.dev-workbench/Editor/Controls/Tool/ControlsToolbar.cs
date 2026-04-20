#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace DevWorkbench.Editor
{
    /// <summary>
    /// ListView / TreeView / TableView 共用的工具栏 / 滚动条 / 搜索栏基础设施。
    /// 在此集中管理常量、样式和绘制函数，避免各控件各写一份导致像素级不一致。
    /// <para>命名约定：控件对外的公开 API 由各自的 <c>*.API.cs</c> 暴露，本类不做对外暴露。</para>
    /// </summary>
    public static class ControlsToolbar
    {
        // ── Toolbar layout constants ────────────────────────────────────────

        /// <summary>工具栏整体高度（含底部 1px 分割线）。</summary>
        public const float ToolbarHeight = 20f;

        /// <summary>工具栏底部分割线厚度。</summary>
        public const float ToolbarSeparatorHeight = 1f;

        /// <summary>工具栏分段（标题 / 搜索 / 按钮组）之间的水平间距。</summary>
        public const float ToolbarSectionGap = 4f;

        /// <summary>工具栏内相邻按钮之间的水平间距。</summary>
        public const float ToolbarButtonSpacing = 2f;

        /// <summary>工具栏内文字按钮高度（仅自定义按钮使用，系统 "+" 按钮与搜索框同高）。</summary>
        public const float ToolbarButtonHeight = 16f;

        /// <summary>搜索框高度（与工具栏 "+" 按钮同高，用于居中对齐）。</summary>
        public const float SearchFieldHeight = 18f;

        // ── ScrollView actual sizes ─────────────────────────────────────────

        /// <summary>
        /// Unity ScrollView 实际占用的竖向滚动条宽度。
        /// 必须与 <see cref="GUI.BeginScrollView(Rect, Vector2, Rect)"/> 内部使用的值一致，
        /// 否则计算的 viewWidth 与实际可视宽会差 1~2px，表现为"虚拟角啃掉最右列"或对不齐。
        /// </summary>
        public static float VerticalScrollbarWidth
        {
            get
            {
                var w = GUI.skin?.verticalScrollbar?.fixedWidth ?? 0f;
                return w > 0f ? w : 15f;
            }
        }

        /// <summary>Unity ScrollView 实际占用的水平滚动条高度。</summary>
        public static float HorizontalScrollbarHeight
        {
            get
            {
                var h = GUI.skin?.horizontalScrollbar?.fixedHeight ?? 0f;
                return h > 0f ? h : 15f;
            }
        }

        // ── Shared drag / drop visuals ──────────────────────────────────────

        /// <summary>拖动 / 换位操作的统一指示线颜色（随 ProSkin 切换）。</summary>
        public static Color DropIndicatorColor =>
            EditorGUIUtility.isProSkin
                ? new Color(0.35f, 0.65f, 1.00f, 1f)
                : new Color(0.10f, 0.45f, 0.85f, 1f);

        // ── Shared GUI styles ───────────────────────────────────────────────

        private static GUIStyle _titleStyle;
        private static GUIStyle _buttonStyle;
        private static GUIStyle _searchPlaceholderStyle;
        private static bool _searchPlaceholderProSkin;

        /// <summary>工具栏左侧标题样式（加粗、垂直居中）。</summary>
        public static GUIStyle TitleStyle =>
            _titleStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };

        /// <summary>工具栏按钮样式（居中对齐，适用于文字与 icon）。</summary>
        public static GUIStyle ButtonStyle =>
            _buttonStyle ??= new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 12,
                padding = new RectOffset(2, 2, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                alignment = TextAnchor.MiddleCenter,
                fixedWidth = 0,
                fixedHeight = 0,
                contentOffset = new Vector2(0, -1f),
            };

        /// <summary>搜索栏占位文本样式，跟随 ProSkin 切换颜色。</summary>
        public static GUIStyle SearchPlaceholderStyle
        {
            get
            {
                if (_searchPlaceholderStyle != null && _searchPlaceholderProSkin == EditorGUIUtility.isProSkin)
                    return _searchPlaceholderStyle;
                _searchPlaceholderProSkin = EditorGUIUtility.isProSkin;
                _searchPlaceholderStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    fontSize = 9,
                    normal =
                {
                    textColor = _searchPlaceholderProSkin
                        ? new Color(0.62f, 0.64f, 0.68f, 0.95f)
                        : new Color(0.42f, 0.45f, 0.50f, 0.95f),
                },
                };
                return _searchPlaceholderStyle;
            }
        }

        // ── Drawing helpers ─────────────────────────────────────────────────

        /// <summary>在工具栏底部画 1px 分割线（仅 Repaint 生效）。</summary>
        public static void DrawToolbarSeparator(Rect toolbarRect)
        {
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(
                new Rect(toolbarRect.x, toolbarRect.yMax - ToolbarSeparatorHeight,
                    toolbarRect.width, ToolbarSeparatorHeight),
                BoxDrawer.BorderColor);
        }

        /// <summary>
        /// 绘制带占位文本的搜索栏。调用方负责 ref-存储 <paramref name="searchField"/> 与 <paramref name="searchText"/>。
        /// 返回文本是否发生变化；变化时会同步设置 <see cref="GUI.changed"/>。
        /// </summary>
        /// <param name="rect">给搜索栏使用的外部 Rect（控件会按 <see cref="SearchFieldHeight"/> 纵向居中）。</param>
        /// <param name="searchField">持久化的 <see cref="SearchField"/> 实例；为 null 会自动创建。</param>
        /// <param name="searchText">当前搜索文本，会被就地更新。</param>
        /// <param name="placeholder">空文本时显示的提示。</param>
        /// <param name="enabled">是否可输入；false 时文本被强制为空并显示 <paramref name="disabledHint"/>。</param>
        /// <param name="disabledHint">disabled 状态下代替 placeholder 的提示。</param>
        public static bool DrawSearchBar(
            Rect rect,
            ref SearchField searchField,
            ref string searchText,
            string placeholder,
            bool enabled = true,
            string disabledHint = null)
        {
            searchField ??= new SearchField();

            var fieldRect = new Rect(
                rect.x,
                rect.y + (rect.height - SearchFieldHeight) * 0.5f,
                rect.width,
                SearchFieldHeight);

            // Unity 自带 SearchTextField 样式默认过大，临时覆盖以贴合 18px 行高。
            var sStyle = GUI.skin.FindStyle("SearchTextField")
                         ?? GUI.skin.FindStyle("ToolbarSearchTextField");
            var origSize = 0;
            var origAlign = TextAnchor.UpperLeft;
            if (sStyle != null)
            {
                origSize = sStyle.fontSize;
                origAlign = sStyle.alignment;
                sStyle.fontSize = 11;
                sStyle.alignment = TextAnchor.MiddleLeft;
            }

            string newText;
            using (new EditorGUI.DisabledScope(!enabled))
                newText = searchField.OnGUI(fieldRect, enabled ? searchText ?? string.Empty : string.Empty);

            if (sStyle != null) { sStyle.fontSize = origSize; sStyle.alignment = origAlign; }

            if (!enabled) newText = string.Empty;

            if (string.IsNullOrEmpty(newText) && Event.current.type == EventType.Repaint)
            {
                var hint = !enabled && !string.IsNullOrEmpty(disabledHint) ? disabledHint : placeholder;
                if (!string.IsNullOrEmpty(hint))
                    GUI.Label(
                        new Rect(fieldRect.x + 18f, fieldRect.y, fieldRect.width - 36f, fieldRect.height),
                        hint, SearchPlaceholderStyle);
            }

            if (newText == searchText) return false;
            searchText = newText;
            GUI.changed = true;
            return true;
        }
    }
}
#endif