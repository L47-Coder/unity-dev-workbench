#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using DevWorkbench;

namespace DevWorkbench.Editor
{

public sealed partial class TableView
{
    private readonly struct ColumnDefinition
    {
        public readonly string Header;
        public readonly string RelativePropertyPath;
        public readonly bool Editable;
        public readonly FieldInfo Field;
        /// <summary>该列允许的最小像素宽（由字段类型推导）。</summary>
        public readonly float MinWidth;

        public ColumnDefinition(string header, string relPath, bool editable, FieldInfo field, float minWidth)
        {
            Header = header;
            RelativePropertyPath = relPath;
            Editable = editable;
            Field = field;
            MinWidth = minWidth;
        }
    }

    private static List<ColumnDefinition> BuildColumnsFromElementType(Type elementType)
    {
        if (elementType == null) return new List<ColumnDefinition>();

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        return elementType
            .GetFields(flags)
            .Where(IsSerializedField)
            .OrderBy(f => f.MetadataToken)
            .Select(ToColumnDefinition)
            .Where(c => c.HasValue)
            .Select(c => c.Value)
            .ToList();
    }

    private static ColumnDefinition? ToColumnDefinition(FieldInfo field)
    {
        var attr = field.GetCustomAttribute<TableColumnAttribute>(false);
        if (attr != null && !attr.Visible) return null;

        var header = string.IsNullOrWhiteSpace(attr?.Header)
            ? ObjectNames.NicifyVariableName(field.Name)
            : attr.Header;
        return new ColumnDefinition(
            header,
            field.Name,
            attr?.Editable ?? true,
            field,
            GetDefaultMinWidth(field.FieldType));
    }

    private static bool IsSerializedField(FieldInfo field) =>
        !field.IsStatic &&
        !field.IsDefined(typeof(NonSerializedAttribute), false) &&
        !field.IsDefined(typeof(HideInInspector), false) &&
        (field.IsPublic || field.IsDefined(typeof(SerializeField), false));

    /// <summary>
    /// 按字段类型返回该列的默认最小像素宽度。
    /// 这些数值基于 Unity 编辑器默认字号下、单行控件不被明显截断的最小可读宽度。
    /// </summary>
    private static float GetDefaultMinWidth(Type type)
    {
        if (type == typeof(bool)) return 40f;
        if (type == typeof(int) || type == typeof(float) || type.IsEnum) return 120f;
        if (type == typeof(Color)) return 120f;
        if (type == typeof(string)) return 140f;
        if (type == typeof(LayerMask)) return 120f;
        if (type == typeof(AnimationCurve) || type == typeof(Gradient)) return 120f;
        if (IsStringList(type)) return 120f;
        if (type == typeof(Vector2) || type == typeof(Vector2Int)) return 140f;
        if (type == typeof(Vector3) || type == typeof(Vector3Int) || type == typeof(Quaternion)) return 210f;
        if (type == typeof(Vector4)) return 280f;
        if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return 140f;
        return DefaultFallbackMinWidth;
    }

    /// <summary>
    /// 确保 <see cref="_columnMinWidths"/> / <see cref="_columnPreferredWidths"/> 与当前列集合匹配。
    /// 首次分配时把 Preferred 初始化为 Min；列数变化时重建。
    /// </summary>
    private void EnsureColumnSizing()
    {
        var count = _columns.Count;
        if (_columnMinWidths == null || _columnMinWidths.Length != count)
        {
            _columnMinWidths = new float[count];
            for (var i = 0; i < count; i++) _columnMinWidths[i] = _columns[i].MinWidth;
        }
        if (_columnPreferredWidths == null || _columnPreferredWidths.Length != count)
        {
            _columnPreferredWidths = new float[count];
            for (var i = 0; i < count; i++) _columnPreferredWidths[i] = _columnMinWidths[i];
        }
    }
}
#endif
}
