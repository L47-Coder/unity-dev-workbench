#if UNITY_EDITOR
using UnityEngine;
using DevWorkbench;

namespace DevWorkbench.Editor
{

/// <summary>
/// IMGUI 文本展示控件，自带边框、滚动、富文本、右键复制。
/// <para>通过 partial class 拆分为 Main（状态 / 样式）和 API（公开接口）。</para>
/// </summary>
public sealed partial class TextView
{
    // ── Properties ──────────────────────────────────────────────────────

    /// <summary>文本字号，默认 13。</summary>
    public float FontSize { get; set; } = 13f;

    /// <summary>是否自动换行。</summary>
    public bool WordWrap { get; set; }

    /// <summary>文本颜色，为 null 则跟随编辑器主题。</summary>
    public Color? TextColor { get; set; }

    /// <summary>新文本到达时是否自动滚动到底部。</summary>
    public bool AutoScrollToBottom { get; set; }

    /// <summary>右键菜单中"复制"的文本，默认 "复制"。</summary>
    public string CopyLabel
    {
        get => _copyLabel;
        set => _copyLabel = value ?? "复制";
    }

    // ── Draw ────────────────────────────────────────────────────────────

    /// <summary>
    /// 在给定 Rect 内绘制完整的 TextView（边框 → 滚动文本区域）。
    /// 每帧调用一次。
    /// </summary>
    public void Draw(Rect rect, string text)
    {
        var boxRect = BoxDrawer.CalcBoxRect(rect);
        if (boxRect.width < 1f || boxRect.height < 1f) return;

        BoxDrawer.DrawBox(boxRect);
        var contentRect = BoxDrawer.CalcContentRect(boxRect);

        GUI.BeginGroup(contentRect);

        var innerRect = new Rect(0f, 0f, contentRect.width, contentRect.height);
        DrawContent(innerRect, text);

        GUI.EndGroup();

        HandleRightClick(rect, text);
    }

    private void DrawContent(Rect rect, string text)
    {
        var style = GetStyle();
        // 预留竖向滚动条宽度，避免文本换行跟随 "出现滚动条" 抖动。
        var layoutWidth = rect.width - ControlsToolbar.VerticalScrollbarWidth;

        if (_cachedText != text
            || !Mathf.Approximately(_cachedLayoutWidth, layoutWidth)
            || _cachedWordWrap != WordWrap
            || !Mathf.Approximately(_cachedFontSize, FontSize))
        {
            if (_cachedText != text)
            {
                if (AutoScrollToBottom)
                    _scrollToBottom = true;
                else
                    _scrollPos = Vector2.zero;
            }
            _cachedText = text;
            _cachedLayoutWidth = layoutWidth;
            _cachedWordWrap = WordWrap;
            _cachedFontSize = FontSize;
            _content = new GUIContent(text ?? string.Empty);
            _contentHeight = style.CalcHeight(_content, layoutWidth);
        }

        if (_scrollToBottom)
        {
            _scrollPos.y = Mathf.Max(0f, _contentHeight - rect.height);
            _scrollToBottom = false;
        }

        var scrollContent = new Rect(0f, 0f, layoutWidth, _contentHeight);
        _scrollPos = GUI.BeginScrollView(rect, _scrollPos, scrollContent);
        GUI.Label(scrollContent, _content, style);
        GUI.EndScrollView();
    }
}
#endif
}
