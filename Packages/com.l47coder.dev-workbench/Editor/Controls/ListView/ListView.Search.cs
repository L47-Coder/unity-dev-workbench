#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace DevWorkbench.Editor
{
    public sealed partial class ListView
    {
        private SearchField _searchField;
        private string _searchText = string.Empty;

        private void DrawSearchBar(Rect rect) =>
            ControlsToolbar.DrawSearchBar(rect, ref _searchField, ref _searchText, _searchPlaceholder);

        private List<int> GetFilteredIndices(IReadOnlyList<string> items)
        {
            var result = new List<int>(items.Count);
            var hasSearch = !string.IsNullOrWhiteSpace(_searchText);
            var lower = hasSearch ? _searchText.ToLowerInvariant() : null;

            for (var i = 0; i < items.Count; i++)
            {
                var name = items[i] ?? string.Empty;
                if (IsIgnored(name)) continue;
                if (hasSearch && !name.ToLowerInvariant().Contains(lower)) continue;
                result.Add(i);
            }
            return result;
        }

        // ── Ignore (glob) ───────────────────────────────────────────────────

        private bool IsIgnored(string name)
        {
            if (IgnoredNames == null || IgnoredNames.Count == 0) return false;
            foreach (var pattern in IgnoredNames)
            {
                if (string.IsNullOrEmpty(pattern)) continue;
                if (MatchesGlobPattern(name, pattern)) return true;
            }
            return false;
        }

        private static bool MatchesGlobPattern(string name, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return false;
            return GlobMatch(name.ToLowerInvariant(), pattern.ToLowerInvariant(), 0, 0);
        }

        private static bool GlobMatch(string text, string pattern, int ti, int pi)
        {
            while (ti < text.Length && pi < pattern.Length)
            {
                if (pattern[pi] == '?')
                { ti++; pi++; }
                else if (pattern[pi] == '*')
                {
                    while (pi < pattern.Length && pattern[pi] == '*') pi++;
                    if (pi == pattern.Length) return true;
                    while (ti < text.Length)
                    {
                        if (GlobMatch(text, pattern, ti, pi)) return true;
                        ti++;
                    }
                    return false;
                }
                else if (pattern[pi] == text[ti])
                { ti++; pi++; }
                else return false;
            }
            while (pi < pattern.Length && pattern[pi] == '*') pi++;
            return ti == text.Length && pi == pattern.Length;
        }
    }
}
#endif
