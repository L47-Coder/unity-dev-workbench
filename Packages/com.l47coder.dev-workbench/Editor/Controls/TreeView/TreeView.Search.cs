#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using DevWorkbench;

namespace DevWorkbench.Editor
{

public sealed partial class TreeView
{
    private SearchField _searchField;
    private string _searchText = string.Empty;
    private string _searchNormalized = string.Empty;

    private void DrawSearchBar(Rect rect)
    {
        if (ControlsToolbar.DrawSearchBar(rect, ref _searchField, ref _searchText, _searchPlaceholder))
            _searchNormalized = NormalizeSearchFilter(_searchText);
    }

    private static string NormalizeSearchFilter(string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return string.Empty;
        return new string(search.Trim()
            .Where(static c => !char.IsWhiteSpace(c) && c != '_' && c != '-')
            .ToArray()).ToLowerInvariant();
    }

    private static bool MatchesFuzzySearch(string text, string filter)
    {
        if (string.IsNullOrEmpty(filter)) return true;
        if (string.IsNullOrEmpty(text)) return false;

        var normText = NormalizeSearchFilter(text);
        if (normText.Contains(filter, StringComparison.Ordinal)) return true;

        var fi = 0;
        for (var ti = 0; ti < normText.Length && fi < filter.Length; ti++)
            if (normText[ti] == filter[fi]) fi++;
        return fi == filter.Length;
    }
}
#endif
}
