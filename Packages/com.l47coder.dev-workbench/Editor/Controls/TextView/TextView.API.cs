#if UNITY_EDITOR
using UnityEngine;
using DevWorkbench;

namespace DevWorkbench.Editor
{

/// <summary>
/// IMGUI text-display control with its own border, scrolling, rich-text and
/// right-click copy support.
/// <para>The implementation is split across partial files: Main (state / styles) and
/// API (public surface).</para>
/// </summary>
public sealed partial class TextView
{
    // ── Properties ──────────────────────────────────────────────────────

    /// <summary>Font size. Defaults to 13.</summary>
    public float FontSize { get; set; } = 13f;

    /// <summary>Whether to word-wrap the text.</summary>
    public bool WordWrap { get; set; }

    /// <summary>Text colour. Falls back to the editor theme when null.</summary>
    public Color? TextColor { get; set; }

    /// <summary>Whether to auto-scroll to the bottom when new text arrives.</summary>
    public bool AutoScrollToBottom { get; set; }

    /// <summary>Label of the "Copy" entry in the context menu. Defaults to "Copy".</summary>
    public string CopyLabel
    {
        get => _copyLabel;
        set => _copyLabel = value ?? "Copy";
    }

    // ── Draw ────────────────────────────────────────────────────────────

    /// <summary>
    /// Draw the complete TextView inside the given rect (border → scrollable text area).
    /// Call once per frame.
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
