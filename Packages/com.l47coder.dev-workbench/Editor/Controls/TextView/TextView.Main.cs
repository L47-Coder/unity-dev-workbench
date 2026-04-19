#if UNITY_EDITOR
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using DevWorkbench;

namespace DevWorkbench.Editor
{

public sealed partial class TextView
{
    private static readonly Regex StripTagsRegex = new(@"<[^>]+>", RegexOptions.Compiled);

    private string _copyLabel = "Copy";

    private static Color DefaultTextColor =>
        EditorGUIUtility.isProSkin
            ? new Color(0.62f, 0.62f, 0.64f, 1f)
            : new Color(0.40f, 0.40f, 0.42f, 1f);

    private GUIStyle _style;
    private float _styleBuiltFontSize = -1f;
    private bool _styleBuiltWordWrap;
    private Color _styleBuiltColor;

    private string _cachedText;
    private float _cachedLayoutWidth;
    private bool _cachedWordWrap;
    private float _cachedFontSize = -1f;
    private GUIContent _content;
    private float _contentHeight;

    private Vector2 _scrollPos;
    private bool _scrollToBottom;

    private GUIStyle GetStyle()
    {
        var color = TextColor ?? DefaultTextColor;
        if (_style != null
            && Mathf.Approximately(_styleBuiltFontSize, FontSize)
            && _styleBuiltWordWrap == WordWrap
            && _styleBuiltColor == color)
            return _style;

        _styleBuiltFontSize = FontSize;
        _styleBuiltWordWrap = WordWrap;
        _styleBuiltColor = color;
        _style = new GUIStyle(EditorStyles.label)
        {
            richText = true,
            wordWrap = WordWrap,
            font = EditorStyles.standardFont,
            fontSize = Mathf.Max(1, (int)FontSize),
        };
        _style.normal.textColor = color;
        _style.hover.textColor = color;
        _style.active.textColor = color;
        _style.focused.textColor = color;
        return _style;
    }

    private void HandleRightClick(Rect rect, string text)
    {
        var evt = Event.current;
        if (evt.type != EventType.ContextClick || !rect.Contains(evt.mousePosition))
            return;

        var plain = StripTagsRegex.Replace(text ?? string.Empty, string.Empty);
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent(_copyLabel), false, () => GUIUtility.systemCopyBuffer = plain);
        menu.ShowAsContext();
        evt.Use();
    }
}
#endif
}
