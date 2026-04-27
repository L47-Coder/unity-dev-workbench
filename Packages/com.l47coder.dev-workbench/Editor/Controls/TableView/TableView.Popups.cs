#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    public sealed partial class TableView
    {
        // ── Pending dirty flag ─────────────────────────────────────────────
        // Popup 是独立窗口，其内部的 GUI.changed 不会传到父窗口。
        // Popup 修改数据时置位该标记，Draw<T> 开头消费一次把 GUI.changed=true 传给父面板。

        private bool _pendingDirty;

        private void ConsumePendingDirty()
        {
            if (!_pendingDirty) return;
            _pendingDirty = false;
            GUI.changed = true;
        }

        // ── List<ComponentRef> cell ────────────────────────────────────────

        private void DrawComponentRefListCell(Rect rect, List<ComponentRef> refList, int rowIndex, object rowItem)
        {
            var summary = refList.Count == 0
                ? "(empty)"
                : $"[{refList.Count}] {string.Join(", ", refList)}";

            if (GUI.Button(rect, summary, StringListSummaryStyle))
            {
                GUI.FocusControl(null);
                ComponentTypeKeyCollector.Invalidate();
                var popup = new ComponentRefListPopup(refList, () =>
                {
                    _pendingDirty = true;
                    _onChange?.Invoke(rowIndex, rowItem);
                });
                PopupWindow.Show(rect, popup);
            }
        }

        // ── List<string> cell ──────────────────────────────────────────────

        private static bool IsStringList(Type type) =>
            type.IsGenericType &&
            type.GetGenericTypeDefinition() == typeof(List<>) &&
            type.GetGenericArguments()[0] == typeof(string);

        private void DrawStringListCell(Rect rect, List<string> listValue, int rowIndex, object rowItem)
        {
            var summary = listValue.Count == 0
                ? "(empty)"
                : $"[{listValue.Count}] {string.Join(", ", listValue)}";

            if (GUI.Button(rect, summary, StringListSummaryStyle))
            {
                GUI.FocusControl(null);
                var popup = new StringListPopup(listValue, () =>
                {
                    _pendingDirty = true;
                    _onChange?.Invoke(rowIndex, rowItem);
                });
                PopupWindow.Show(rect, popup);
            }
        }

        private static GUIStyle _stringListSummaryStyle;
        private static GUIStyle StringListSummaryStyle =>
            _stringListSummaryStyle ??= new GUIStyle(EditorStyles.textField)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                padding = new RectOffset(4, 4, 0, 0),
            };

        // ── StringListPopup ────────────────────────────────────────────────

        private sealed class StringListPopup : PopupWindowContent
        {
            private const float HeaderH = 20f;
            private const float RowH = 20f;
            private const float RowGap = 2f;
            private const float HeaderToBodyGap = 4f;
            private const float Padding = 6f;
            private const float BtnW = 22f;
            private const float ScrollbarW = 14f;
            private const float BodyMaxH = 280f;
            private const float WindowW = 300f;

            private readonly List<string> _list;
            private readonly Action _onChanged;
            private Vector2 _scroll;

            public StringListPopup(List<string> list, Action onChanged)
            {
                _list = list;
                _onChanged = onChanged;
            }

            public override Vector2 GetWindowSize()
            {
                var rows = _list.Count;
                float totalH;
                if (rows == 0)
                {
                    totalH = Padding * 2f + HeaderH;
                }
                else
                {
                    var desired = rows * RowH + (rows - 1) * RowGap;
                    var bodyH = Mathf.Min(desired, BodyMaxH);
                    totalH = Padding * 2f + HeaderH + HeaderToBodyGap + bodyH;
                }
                return new Vector2(WindowW, totalH);
            }

            public override void OnGUI(Rect rect)
            {
                var inner = new Rect(rect.x + Padding, rect.y + Padding,
                    rect.width - Padding * 2f, rect.height - Padding * 2f);

                var bodyAvailH = inner.height - HeaderH - HeaderToBodyGap;
                var contentH = _list.Count * RowH + Mathf.Max(0, _list.Count - 1) * RowGap;
                var needScroll = _list.Count > 0 && contentH > bodyAvailH + 0.5f;
                var rightInset = needScroll ? ScrollbarW : 0f;

                var headerRect = new Rect(inner.x, inner.y, inner.width - rightInset, HeaderH);
                DrawHeader(headerRect);

                if (_list.Count == 0) return;

                var bodyRect = new Rect(inner.x, headerRect.yMax + HeaderToBodyGap, inner.width,
                    Mathf.Max(0f, inner.height - HeaderH - HeaderToBodyGap));
                DrawBody(bodyRect, needScroll);
            }

            private void DrawHeader(Rect headerRect)
            {
                var addRect = new Rect(headerRect.xMax - BtnW, headerRect.y, BtnW, headerRect.height);
                var labelRect = new Rect(headerRect.x, headerRect.y,
                    headerRect.width - BtnW - 4f, headerRect.height);

                EditorGUI.LabelField(labelRect, $"Strings ({_list.Count})", EditorStyles.miniBoldLabel);

                if (GUI.Button(addRect, "＋"))
                {
                    _list.Add(string.Empty);
                    _onChanged?.Invoke();
                    GUI.FocusControl(null);
                    editorWindow.Repaint();
                }
            }

            private void DrawBody(Rect bodyRect, bool needScroll)
            {
                var viewW = needScroll ? bodyRect.width - ScrollbarW : bodyRect.width;
                var contentH = _list.Count * RowH + Mathf.Max(0, _list.Count - 1) * RowGap;
                var viewRect = new Rect(0f, 0f, viewW, Mathf.Max(contentH, bodyRect.height));

                _scroll = GUI.BeginScrollView(bodyRect, _scroll, viewRect);

                var removeIdx = -1;
                for (var i = 0; i < _list.Count; i++)
                {
                    var rowY = i * (RowH + RowGap);
                    var fieldRect = new Rect(0f, rowY, viewW - BtnW - 4f, RowH);
                    var btnRect = new Rect(viewW - BtnW, rowY, BtnW, RowH);

                    EditorGUI.BeginChangeCheck();
                    var newVal = EditorGUI.TextField(fieldRect, _list[i] ?? string.Empty);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _list[i] = newVal;
                        _onChanged?.Invoke();
                    }

                    if (GUI.Button(btnRect, "−"))
                        removeIdx = i;
                }

                if (removeIdx >= 0)
                {
                    _list.RemoveAt(removeIdx);
                    _onChanged?.Invoke();
                    GUI.FocusControl(null);
                    editorWindow.Repaint();
                }

                GUI.EndScrollView();
            }
        }

        // ── ComponentRefListPopup ──────────────────────────────────────────
        // 与 StringListPopup 架构完全相同（PopupWindowContent + PopupWindow.Show）。
        // 每行改为双下拉框：左侧选组件类型，右侧选数据 Key。

        private sealed class ComponentRefListPopup : PopupWindowContent
        {
            private const float HeaderH = 20f;
            private const float RowH = 20f;
            private const float RowGap = 2f;
            private const float HeaderToBodyGap = 4f;
            private const float Padding = 6f;
            private const float BtnW = 22f;
            private const float ColGap = 4f;
            private const float ScrollbarW = 14f;
            private const float BodyMaxH = 280f;
            private const float WindowW = 360f;

            private readonly List<ComponentRef> _list;
            private readonly Action _onChanged;
            private Vector2 _scroll;
            private readonly List<ComponentTypeGroup> _groups;
            private readonly string[] _componentNames;

            public ComponentRefListPopup(List<ComponentRef> list, Action onChanged)
            {
                _list   = list;
                _onChanged = onChanged;
                _groups = ComponentTypeKeyCollector.CollectGrouped();
                _componentNames = new string[_groups.Count];
                for (var i = 0; i < _groups.Count; i++)
                    _componentNames[i] = _groups[i].ComponentClassName;
            }

            public override Vector2 GetWindowSize()
            {
                var rows = _list.Count;
                float totalH;
                if (rows == 0)
                {
                    totalH = Padding * 2f + HeaderH;
                }
                else
                {
                    var desired = rows * RowH + (rows - 1) * RowGap;
                    var bodyH = Mathf.Min(desired, BodyMaxH);
                    totalH = Padding * 2f + HeaderH + HeaderToBodyGap + bodyH;
                }
                return new Vector2(WindowW, totalH);
            }

            public override void OnGUI(Rect rect)
            {
                var inner = new Rect(rect.x + Padding, rect.y + Padding,
                    rect.width - Padding * 2f, rect.height - Padding * 2f);

                var bodyAvailH = inner.height - HeaderH - HeaderToBodyGap;
                var contentH = _list.Count * RowH + Mathf.Max(0, _list.Count - 1) * RowGap;
                var needScroll = _list.Count > 0 && contentH > bodyAvailH + 0.5f;
                var rightInset = needScroll ? ScrollbarW : 0f;

                var headerRect = new Rect(inner.x, inner.y, inner.width - rightInset, HeaderH);
                DrawHeader(headerRect);

                if (_list.Count == 0) return;

                var bodyRect = new Rect(inner.x, headerRect.yMax + HeaderToBodyGap, inner.width,
                    Mathf.Max(0f, inner.height - HeaderH - HeaderToBodyGap));
                DrawBody(bodyRect, needScroll);
            }

            private void DrawHeader(Rect headerRect)
            {
                var addRect   = new Rect(headerRect.xMax - BtnW, headerRect.y, BtnW, headerRect.height);
                var labelRect = new Rect(headerRect.x, headerRect.y, headerRect.width - BtnW - 4f, headerRect.height);

                EditorGUI.LabelField(labelRect, $"Components ({_list.Count})", EditorStyles.miniBoldLabel);

                using (new EditorGUI.DisabledScope(_groups.Count == 0))
                {
                    if (GUI.Button(addRect, "＋"))
                    {
                        AddDefaultRow();
                        _onChanged?.Invoke();
                        GUI.FocusControl(null);
                        editorWindow.Repaint();
                    }
                }
            }

            private void DrawBody(Rect bodyRect, bool needScroll)
            {
                var viewW    = needScroll ? bodyRect.width - ScrollbarW : bodyRect.width;
                var contentH = _list.Count * RowH + Mathf.Max(0, _list.Count - 1) * RowGap;
                var viewRect = new Rect(0f, 0f, viewW, Mathf.Max(contentH, bodyRect.height));

                _scroll = GUI.BeginScrollView(bodyRect, _scroll, viewRect);

                var removeIdx = -1;
                for (var i = 0; i < _list.Count; i++)
                {
                    var rowY = i * (RowH + RowGap);
                    DrawRow(new Rect(0f, rowY, viewW, RowH), i, viewW, ref removeIdx);
                }

                if (removeIdx >= 0)
                {
                    _list.RemoveAt(removeIdx);
                    _onChanged?.Invoke();
                    GUI.FocusControl(null);
                    editorWindow.Repaint();
                }

                GUI.EndScrollView();
            }

            private void DrawRow(Rect rowRect, int listIndex, float viewW, ref int removeIdx)
            {
                var typeKey = _list[listIndex].TypeKey ?? string.Empty;
                ParseTypeKey(typeKey, out var compClass, out var dataKey);

                var dataW   = viewW - BtnW - ColGap;
                var colW    = (dataW - ColGap) / 2f;
                var popH    = EditorGUIUtility.singleLineHeight;
                var popY    = rowRect.y + (rowRect.height - popH) * 0.5f;
                var compRect   = new Rect(rowRect.x, popY, colW, popH);
                var keyRect    = new Rect(compRect.xMax + ColGap, popY, colW, popH);
                var removeRect = new Rect(viewW - BtnW, rowRect.y, BtnW, rowRect.height);

                var compIdx = Array.IndexOf(_componentNames, compClass);

                if (compIdx < 0)
                {
                    var warnStyle = new GUIStyle(EditorStyles.miniLabel)
                        { normal = { textColor = new Color(0.9f, 0.4f, 0.4f) } };
                    EditorGUI.LabelField(compRect, string.IsNullOrEmpty(compClass) ? "?" : $"{compClass} (?)", warnStyle);
                    EditorGUI.LabelField(keyRect, dataKey, warnStyle);
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    var newCompIdx = EditorGUI.Popup(compRect, compIdx, _componentNames);
                    if (EditorGUI.EndChangeCheck() && newCompIdx >= 0 && newCompIdx < _groups.Count)
                    {
                        var g  = _groups[newCompIdx];
                        var fk = g.DataKeys.Length > 0 ? g.DataKeys[0] : string.Empty;
                        _list[listIndex] = new ComponentRef { TypeKey = $"{g.ComponentClassName}_{fk}" };
                        _onChanged?.Invoke();
                        if (GUI.Button(removeRect, "−")) removeIdx = listIndex;
                        editorWindow.Repaint();
                        return;
                    }

                    var keys = _groups[compIdx].DataKeys;
                    if (keys.Length > 0)
                    {
                        var keyIdx = Array.IndexOf(keys, dataKey);
                        if (keyIdx < 0) keyIdx = 0;

                        EditorGUI.BeginChangeCheck();
                        var newKeyIdx = EditorGUI.Popup(keyRect, keyIdx, keys);
                        if (EditorGUI.EndChangeCheck())
                        {
                            _list[listIndex] = new ComponentRef { TypeKey = $"{compClass}_{keys[newKeyIdx]}" };
                            _onChanged?.Invoke();
                            editorWindow.Repaint();
                        }
                    }
                    else
                    {
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUI.Popup(keyRect, 0, new[] { "(no keys)" });
                    }
                }

                if (GUI.Button(removeRect, "−"))
                    removeIdx = listIndex;
            }

            private void AddDefaultRow()
            {
                if (_groups.Count == 0) return;
                var g  = _groups[0];
                var fk = g.DataKeys.Length > 0 ? g.DataKeys[0] : string.Empty;
                _list.Add(new ComponentRef { TypeKey = $"{g.ComponentClassName}_{fk}" });
            }

            private static void ParseTypeKey(string typeKey, out string compClass, out string dataKey)
            {
                var underscore = typeKey?.IndexOf('_') ?? -1;
                if (underscore > 0)
                {
                    compClass = typeKey[..underscore];
                    dataKey   = typeKey[(underscore + 1)..];
                }
                else
                {
                    compClass = typeKey ?? string.Empty;
                    dataKey   = string.Empty;
                }
            }
        }
    }
}
#endif
