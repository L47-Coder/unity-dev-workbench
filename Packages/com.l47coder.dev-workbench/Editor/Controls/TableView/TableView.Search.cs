#if UNITY_EDITOR
using System.Collections.Generic;
using DevWorkbench;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace DevWorkbench.Editor
{

    public sealed partial class TableView
    {
        private SearchField _searchField;
        private string _searchText = string.Empty;

        private void DrawSearchBar<T>(Rect rect, List<T> list)
        {
            _ = list;
            var searchCol = FindSearchColumn();
            var hasSearchCol = searchCol.HasValue;

            // 由表头文本（而非字段名）决定占位文本，用户可读性更高。
            var displayName = hasSearchCol ? searchCol.Value.Header : SearchField;
            var placeholder = $"Search by {displayName}...";
            var disabledHint = $"No \"{displayName}\" column in this table";

            ControlsToolbar.DrawSearchBar(
                rect, ref _searchField, ref _searchText,
                placeholder,
                enabled: hasSearchCol,
                disabledHint: disabledHint);
        }

        private ColumnDefinition? FindSearchColumn()
        {
            if (_columns == null || string.IsNullOrEmpty(SearchField)) return null;
            for (var i = 0; i < _columns.Count; i++)
            {
                var col = _columns[i];
                if (string.Equals(col.RelativePropertyPath, SearchField, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(col.Header, SearchField, System.StringComparison.OrdinalIgnoreCase))
                    return col;
            }
            return null;
        }

        private List<int> GetFilteredIndices<T>(List<T> list)
        {
            var searchCol = FindSearchColumn();
            var hasSearch = !string.IsNullOrWhiteSpace(_searchText) && searchCol.HasValue;

            if (!hasSearch) return AllIndices(list.Count);

            var lowerSearch = _searchText.ToLowerInvariant();
            var keyField = searchCol.Value.Field;
            var result = new List<int>(list.Count);
            for (var i = 0; i < list.Count; i++)
            {
                var value = keyField.GetValue(list[i]);
                if (GetFieldStringValue(value).ToLowerInvariant().Contains(lowerSearch))
                    result.Add(i);
            }
            return result;
        }

        private static List<int> AllIndices(int count)
        {
            var result = new List<int>(count);
            for (var i = 0; i < count; i++) result.Add(i);
            return result;
        }

        private static string GetFieldStringValue(object value)
        {
            if (value == null) return "null";
            return value switch
            {
                string s => s,
                int i => i.ToString(),
                float f => f.ToString(),
                bool b => b.ToString(),
                UnityEngine.Object obj => obj ? obj.name : "null",
                _ => value.ToString()
            };
        }
    }
#endif
}
