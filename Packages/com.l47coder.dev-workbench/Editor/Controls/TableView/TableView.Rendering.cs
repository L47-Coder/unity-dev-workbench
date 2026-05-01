#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DevWorkbench;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DevWorkbench.Editor
{
    public sealed partial class TableView
    {
        private void DrawToolbar(Rect toolbarRect)
        {
            ControlsToolbar.DrawToolbarSeparator(toolbarRect);

            var pad = toolbarRect.height - ControlsToolbar.ToolbarSeparatorHeight - ControlsToolbar.SearchFieldHeight;
            var left = toolbarRect.x;
            var right = toolbarRect.xMax - pad;

            var searchW = Mathf.Max(right - left, 20f);
            DrawSearchBar(new Rect(left, toolbarRect.y, searchW, toolbarRect.height));
        }

        private void DrawHeader<T>(Rect rowRect, List<T> list, TableLayout layout)
        {
            var contentWidth = Mathf.Max(rowRect.width, layout.TotalWidth);
            var viewRect = new Rect(0f, 0f, contentWidth, rowRect.height);
            var headerScroll = new Vector2(_scrollPos.x, 0f);
            GUI.BeginScrollView(rowRect, headerScroll, viewRect, GUIStyle.none, GUIStyle.none);

            var innerRect = new Rect(0f, 0f, contentWidth, rowRect.height);
            var cursorX = 0f;

            if (layout.GripWidth > 0)
            {
                var gripRect = new Rect(cursorX, innerRect.y, layout.GripWidth, innerRect.height);
                PaintCellFrame(gripRect, HeaderCellBackground, GridLineColor);
                DrawGripDots(gripRect);
                cursorX = gripRect.xMax;
            }

            var indexRect = new Rect(cursorX, innerRect.y, layout.IndexWidth, innerRect.height);
            PaintCellFrame(indexRect, HeaderCellBackground, GridLineColor);
            GUI.Label(PaddedRect(indexRect), list.Count.ToString(), HeaderCellLabelStyle);
            if (CanSelect && Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                indexRect.Contains(Event.current.mousePosition))
            {
                _selectedIndex = -1;
                Event.current.Use();
            }
            cursorX = indexRect.xMax;

            for (var i = 0; i < _columns.Count; i++)
            {
                var cell = new Rect(cursorX, innerRect.y, layout.DataColumnWidths[i], innerRect.height);
                PaintCellFrame(cell, HeaderCellBackground, GridLineColor);
                GUI.Label(PaddedRect(cell), _columns[i].Header, HeaderCellLabelStyle);

                HandleColumnResize(cell, innerRect, layout, i);

                cursorX = cell.xMax;
            }

            if (layout.ActionsWidth > 0)
            {
                var actionRect = new Rect(innerRect.xMax - layout.ActionsWidth, innerRect.y, layout.ActionsWidth, innerRect.height);
                PaintCellFrame(actionRect, HeaderCellBackground, GridLineColor);
                using (new EditorGUI.DisabledScope(!CanAdd))
                {
                    if (GUI.Button(PaddedRect(actionRect), "＋"))
                    {
                        GUI.FocusControl(null);
                        try { list.Add(Activator.CreateInstance<T>()); }
                        catch { list.Add(default); }
                        _selectedIndex = -1;
                        GUI.changed = true;
                    }
                }
            }

            GUI.EndScrollView();
        }

        private void HandleColumnResize(Rect cell, Rect rowRect, TableLayout layout, int columnIndex)
        {
            var splitterRect = new Rect(cell.xMax - 3f, rowRect.y, 6f, rowRect.height);
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.SplitResizeLeftRight);

            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;
            switch (e.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && splitterRect.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        _resizeColumnIndex = columnIndex;
                        _resizeStartMouseX = e.mousePosition.x;
                        for (var j = 0; j < _columns.Count; j++)
                            _columnPreferredWidths[j] = Mathf.Max(_columnMinWidths[j], layout.DataColumnWidths[j]);
                        _resizeStartPreferredWidth = _columnPreferredWidths[columnIndex];
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        var delta = e.mousePosition.x - _resizeStartMouseX;
                        _columnPreferredWidths[_resizeColumnIndex] = Mathf.Max(
                            _columnMinWidths[_resizeColumnIndex],
                            _resizeStartPreferredWidth + delta);
                        GUI.changed = true;
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        _resizeColumnIndex = -1;
                        e.Use();
                    }
                    break;
            }
        }

        private void DrawRows<T>(Rect bodyRect, List<T> list, List<int> filteredIndices, TableLayout layout, float viewWidth)
        {
            var isSearching = !string.IsNullOrWhiteSpace(_searchText);
            var rowCount = list.Count;
            var displayCount = filteredIndices.Count;
            var rowHeight = ComputeRowHeight();
            var uniformHeights = BuildUniformHeights(displayCount, rowHeight);
            var invalidIndices = BuildDuplicateKeyIndices(list);

            if (isSearching && _draggingOwner == this)
                EndReorderSession();

            var rowsContentWidth = Mathf.Max(viewWidth, layout.TotalWidth);
            var totalH = displayCount * rowHeight;
            var viewRect = new Rect(0f, 0f, rowsContentWidth, totalH);

            _scrollPos = GUI.BeginScrollView(bodyRect, _scrollPos, viewRect);

            var inner = new Rect(0f, 0f, rowsContentWidth, viewRect.height);

            var isDraggingThis = CanDrag && !isSearching &&
                                 _draggingOwner == this && _reorder != null &&
                                 _reorder.ArraySize == rowCount;

            if (isDraggingThis)
            {
                EnsureSessionBuffers(rowCount);
                if (Event.current.type == EventType.Repaint)
                {
                    if (!_reorder.PositionsInitialized)
                        InitializeDragPositions(inner.yMin, uniformHeights);
                    ApplyBodyScrollDelta(inner.yMin);
                    UpdateInsertSlotFromMouse(inner, uniformHeights, rowCount);
                    UpdateTargets(inner.yMin, uniformHeights);
                    StepSessionAnimation(BeginFrameDelta());
                }
            }
            else if (_draggingOwner == this && _reorder != null && _reorder.ArraySize != rowCount)
            {
                EndReorderSession();
            }

            var visualRows = BuildVisualRows(inner, uniformHeights, filteredIndices, isDraggingThis);
            var removeIndex = -1;

            foreach (var visual in visualRows.OrderBy(v => v.DrawY))
            {
                var rowRect = new Rect(inner.x, visual.DrawY, inner.width, visual.Height);
                if (visual.IsGap)
                {
                    DrawGapPlaceholder(rowRect);
                    continue;
                }
                var isInvalid = invalidIndices != null && invalidIndices.Contains(visual.RowIndex);
                DrawRow(rowRect, list, visual.RowIndex, visual.StripeIndex, isSearching, ref removeIndex, layout, isInvalid: isInvalid);
            }

            if (isDraggingThis && _reorder != null)
            {
                var floaterStripe = 0;
                for (var i = 0; i < filteredIndices.Count; i++)
                    if (filteredIndices[i] == _reorder.SourceIndex) { floaterStripe = i % 2; break; }

                var floatRect = new Rect(inner.x, _reorder.DragRowY, inner.width, _reorder.SourceRowHeight);
                DrawDragFloatingRowShadow(floatRect);
                var floatInvalid = invalidIndices != null && invalidIndices.Contains(_reorder.SourceIndex);
                DrawRow(floatRect, list, _reorder.SourceIndex, floaterStripe, isSearching, ref removeIndex, layout, isDragFloating: true, isInvalid: floatInvalid);
            }

            if (CanDrag && !isSearching)
                HandleActiveReorderLifecycle(list);

            GUI.EndScrollView();

            if (removeIndex >= 0)
            {
                if (_draggingOwner == this && _reorder?.SourceIndex == removeIndex)
                    EndReorderSession();
                list.RemoveAt(removeIndex);
                if (_selectedIndex == removeIndex) _selectedIndex = -1;
                else if (_selectedIndex > removeIndex) _selectedIndex--;
                GUI.changed = true;
            }
        }

        private static float[] BuildUniformHeights(int count, float height)
        {
            var arr = new float[count];
            for (var i = 0; i < count; i++) arr[i] = height;
            return arr;
        }

        private void DrawRow<T>(
            Rect rowRect,
            List<T> list,
            int dataIndex,
            int stripeIndex,
            bool isSearching,
            ref int removeIndex,
            TableLayout layout,
            bool isDragFloating = false,
            bool isInvalid = false)
        {
            var isSelected = CanSelect && !isDragFloating && _selectedIndex == dataIndex;
            var fill = isInvalid
                ? (EditorGUIUtility.isProSkin
                    ? new Color(0.42f, 0.10f, 0.10f, 1f)
                    : new Color(1.00f, 0.74f, 0.74f, 1f))
                : isSelected
                    ? SelectedCellBackground
                    : BodyCellBackground(stripeIndex % 2 == 1);

            var rowControlId = GUIUtility.GetControlID(
                RowReorderControlHintHash ^ GetHashCode() ^ (dataIndex * 7919), FocusType.Passive);

            var cursorX = rowRect.x;
            Rect gripRect = default;

            if (layout.GripWidth > 0)
            {
                gripRect = new Rect(cursorX, rowRect.y, layout.GripWidth, rowRect.height);
                PaintCellFrame(gripRect, fill, GridLineColor);
                if (!isSearching)
                {
                    DrawGripDots(gripRect);
                    if (!isDragFloating)
                        EditorGUIUtility.AddCursorRect(gripRect, MouseCursor.MoveArrow);
                }
                cursorX = gripRect.xMax;
            }

            var indexRect = new Rect(cursorX, rowRect.y, layout.IndexWidth, rowRect.height);
            PaintCellFrame(indexRect, fill, GridLineColor);
            GUI.Label(PaddedRect(indexRect), $"{dataIndex}", BodyIndexLabelStyle);
            HandleRowSelectInput(indexRect, dataIndex, isDragFloating ? null : (object)list[dataIndex], isDragFloating);
            cursorX = indexRect.xMax;

            for (var i = 0; i < _columns.Count; i++)
            {
                var cell = new Rect(cursorX, rowRect.y, layout.DataColumnWidths[i], rowRect.height);
                var field = _columns[i].Field;
                PaintCellFrame(cell, fill, GridLineColor);

                if (Event.current.type == EventType.ContextClick && cell.Contains(Event.current.mousePosition))
                {
                    var text = field != null ? GetFieldStringValue(field.GetValue(list[dataIndex])) : string.Empty;
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent(_copyLabel), false, () => EditorGUIUtility.systemCopyBuffer = text);
                    menu.ShowAsContext();
                    Event.current.Use();
                }
                else if (field == null)
                    EditorGUI.LabelField(PaddedRect(cell), $"Missing field: {_columns[i].RelativePropertyPath}", EditorStyles.wordWrappedMiniLabel);
                else
                {
                    using (new EditorGUI.DisabledScope(!_columns[i].Editable || isDragFloating))
                        DrawCellField(PaddedRect(cell), list, dataIndex, field, _columns[i].DropdownMethodName);
                }
                cursorX = cell.xMax;
            }

            if (layout.ActionsWidth > 0)
            {
                var actionRect = new Rect(rowRect.xMax - layout.ActionsWidth, rowRect.y, layout.ActionsWidth, rowRect.height);
                PaintCellFrame(actionRect, fill, GridLineColor);
                using (new EditorGUI.DisabledScope(isDragFloating || !CanRemove))
                {
                    if (GUI.Button(PaddedRect(actionRect), "−"))
                    {
                        GUI.FocusControl(null);
                        removeIndex = dataIndex;
                    }
                }
            }

            if (layout.GripWidth > 0 && !isSearching && !isDragFloating)
                HandleRowReorderInput(rowControlId, gripRect, rowRect, dataIndex, list.Count, rowRect.height);
        }

        private void HandleRowSelectInput(Rect indexRect, int dataIndex, object item, bool isDragFloating)
        {
            if (!CanSelect || isDragFloating || _draggingOwner == this) return;
            if (Event.current.type != EventType.MouseDown || Event.current.button != 0) return;
            if (!indexRect.Contains(Event.current.mousePosition)) return;
            if (_selectedIndex != dataIndex)
            {
                _selectedIndex = dataIndex;
                _onRowSelected?.Invoke(dataIndex, item);
            }
            GUI.FocusControl(null);
            Event.current.Use();
        }

        private void DrawCellField<T>(Rect rect, List<T> list, int index, FieldInfo field, string dropdownMethodName)
        {
            var boxed = (object)list[index];
            var value = field.GetValue(boxed);
            var type = field.FieldType;

            if (IsComponentRefList(type))
            {
                var refList = value as List<ComponentRef>;
                if (refList == null)
                {
                    refList = new List<ComponentRef>();
                    field.SetValue(boxed, refList);
                    list[index] = (T)boxed;
                    GUI.changed = true;
                    _onChange?.Invoke(index, boxed);
                }
                DrawComponentRefListCell(rect, refList, index, boxed);
                return;
            }

            if (IsStringList(type))
            {
                var stringList = value as List<string>;
                if (stringList == null)
                {
                    stringList = new List<string>();
                    field.SetValue(boxed, stringList);
                    list[index] = (T)boxed;
                    GUI.changed = true;
                    _onChange?.Invoke(index, boxed);
                }
                DrawStringListCell(rect, stringList, index, boxed);
                return;
            }

            var isSupported =
                type == typeof(string) || type == typeof(int) || type == typeof(float) ||
                type == typeof(bool) || type.IsEnum ||
                typeof(UnityEngine.Object).IsAssignableFrom(type) ||
                type == typeof(AnimationCurve) || type == typeof(Gradient) ||
                type == typeof(Color) ||
                type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4) ||
                type == typeof(Vector2Int) || type == typeof(Vector3Int) ||
                type == typeof(Quaternion) ||
                type == typeof(LayerMask);

            if (!isSupported)
            {
                EditorGUI.LabelField(rect, value?.ToString() ?? "null", EditorStyles.miniLabel);
                return;
            }

            EditorGUI.BeginChangeCheck();
            object newValue;

            if (type == typeof(string))
            {
                if (dropdownMethodName != null)
                {
                    var options = InvokeDropdownMethod(field, dropdownMethodName);
                    if (options is { Length: > 0 })
                    {
                        var cur = value as string ?? string.Empty;
                        var idx = Math.Max(0, Array.IndexOf(options, cur));
                        newValue = options[EditorGUI.Popup(rect, idx, options)];
                    }
                    else
                        newValue = EditorGUI.DelayedTextField(rect, value as string ?? string.Empty);
                }
                else
                    newValue = EditorGUI.DelayedTextField(rect, value as string ?? string.Empty);
            }
            else if (type == typeof(int))
                newValue = EditorGUI.DelayedIntField(rect, value is int iv ? iv : 0);
            else if (type == typeof(float))
                newValue = EditorGUI.DelayedFloatField(rect, value is float fv ? fv : 0f);
            else if (type == typeof(bool))
                newValue = DrawToggleCell(rect, value is bool bv && bv);
            else if (type.IsEnum)
                newValue = EditorGUI.EnumPopup(rect, (Enum)value);
            else if (type == typeof(AnimationCurve))
                newValue = EditorGUI.CurveField(rect, value as AnimationCurve ?? new AnimationCurve());
            else if (type == typeof(Gradient))
                newValue = EditorGUI.GradientField(rect, value as Gradient ?? new Gradient());
            else if (type == typeof(Color))
                newValue = EditorGUI.ColorField(rect, value is Color cv ? cv : Color.white);
            else if (type == typeof(Vector2))
                newValue = EditorGUI.Vector2Field(rect, GUIContent.none, value is Vector2 v2 ? v2 : default);
            else if (type == typeof(Vector3))
                newValue = EditorGUI.Vector3Field(rect, GUIContent.none, value is Vector3 v3 ? v3 : default);
            else if (type == typeof(Vector4))
                newValue = EditorGUI.Vector4Field(rect, GUIContent.none, value is Vector4 v4 ? v4 : default);
            else if (type == typeof(Vector2Int))
                newValue = EditorGUI.Vector2IntField(rect, GUIContent.none, value is Vector2Int vi2 ? vi2 : default);
            else if (type == typeof(Vector3Int))
                newValue = EditorGUI.Vector3IntField(rect, GUIContent.none, value is Vector3Int vi3 ? vi3 : default);
            else if (type == typeof(Quaternion))
            {
                var q = value is Quaternion qv ? qv : Quaternion.identity;
                var euler = EditorGUI.Vector3Field(rect, GUIContent.none, q.eulerAngles);
                newValue = Quaternion.Euler(euler);
            }
            else if (type == typeof(LayerMask))
            {
                var mask = value is LayerMask lm ? lm : new LayerMask();
                var concatenated = InternalEditorUtility.LayerMaskToConcatenatedLayersMask(mask);
                var picked = EditorGUI.MaskField(rect, concatenated, InternalEditorUtility.layers);
                newValue = (LayerMask)(int)InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(picked);
            }
            else
                newValue = EditorGUI.ObjectField(rect, value as UnityEngine.Object, type, true);

            if (EditorGUI.EndChangeCheck())
            {
                field.SetValue(boxed, newValue);
                list[index] = (T)boxed;
                GUI.changed = true;
                _onChange?.Invoke(index, boxed);
            }
        }

        private static readonly Color ToggleOnColor  = new(0.22f, 0.62f, 0.35f, 0.88f);
        private static readonly Color ToggleOffColor = new(0.72f, 0.22f, 0.22f, 0.88f);

        private static bool DrawToggleCell(Rect rect, bool current)
        {
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, current ? ToggleOnColor : ToggleOffColor);
                GUI.Label(rect, current ? "✓" : "✕", new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal    = { textColor = Color.white },
                    fontStyle = FontStyle.Bold,
                });
            }

            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                rect.Contains(Event.current.mousePosition))
            {
                GUI.changed = true;
                Event.current.Use();
                return !current;
            }

            return current;
        }

        private static string[] InvokeDropdownMethod(FieldInfo field, string methodName)
        {
            if (_dropdownOptionsCache.TryGetValue(field, out var cached)) return cached;

            var method = field.DeclaringType?.GetMethod(
                methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null) return null;
            var result = method.Invoke(null, null) switch
            {
                string[] arr          => arr,
                List<string> list     => list.ToArray(),
                IEnumerable<string> e => e.ToArray(),
                _                     => null,
            };
            if (result != null) _dropdownOptionsCache[field] = result;
            return result;
        }

        private static void DrawDragFloatingRowShadow(Rect rowRect)
        {
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(new Rect(rowRect.x + 2f, rowRect.y + 3f, rowRect.width, rowRect.height), new Color(0f, 0f, 0f, 0.18f));
            DrawRectOutline(rowRect, new Color(0.28f, 0.58f, 0.98f, 0.55f), 1f);
        }

        private static void DrawGapPlaceholder(Rect gap)
        {
            if (Event.current.type != EventType.Repaint) return;
            var pulse = 0.55f + 0.45f * Mathf.Sin((float)EditorApplication.timeSinceStartup * 6f);
            EditorGUI.DrawRect(gap, new Color(0.25f, 0.55f, 0.95f, 0.11f + 0.08f * pulse));
            DrawRectOutline(gap, new Color(0.32f, 0.62f, 1f, 0.28f + 0.12f * pulse), 1f);
        }

        private static void DrawGripDots(Rect cellRect)
        {
            if (Event.current.type != EventType.Repaint) return;
            var c = EditorGUIUtility.isProSkin
                ? new Color(0.55f, 0.58f, 0.62f, 1f)
                : new Color(0.35f, 0.36f, 0.40f, 1f);
            const float dotW = 14f;
            const float dotH = 1.7f;
            var cx = cellRect.x + (cellRect.width - dotW) * 0.5f;
            var cy = cellRect.y + cellRect.height * 0.5f;
            for (var row = -1; row <= 1; row++)
                EditorGUI.DrawRect(new Rect(cx, cy + row * 3.2f - dotH * 0.5f, dotW, dotH), c);
        }

        private static HashSet<int> BuildDuplicateKeyIndices<T>(List<T> list)
        {
            var keyField = typeof(T).GetField("Key",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (keyField == null || keyField.FieldType != typeof(string)) return null;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var dupes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in list)
            {
                var k = ((string)keyField.GetValue(item))?.Trim();
                if (!string.IsNullOrEmpty(k) && !seen.Add(k)) dupes.Add(k);
            }
            if (dupes.Count == 0) return null;

            var result = new HashSet<int>();
            for (var i = 0; i < list.Count; i++)
            {
                var k = ((string)keyField.GetValue(list[i]))?.Trim();
                if (!string.IsNullOrEmpty(k) && dupes.Contains(k)) result.Add(i);
            }
            return result;
        }
    }
}
#endif
