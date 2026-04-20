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
        // Popup 是独立 EditorWindow，其内部的 GUI.changed 不会传到父窗口。
        // Popup 修改数据时置位该标记，Draw<T> 开头消费一次把 GUI.changed=true 传给父面板。

        private bool _pendingDirty;

        private void ConsumePendingDirty()
        {
            if (!_pendingDirty) return;
            _pendingDirty = false;
            GUI.changed = true;
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

        // ── Popup content ──────────────────────────────────────────────────

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

                // 预判是否需要滚动 → 若需要，表头宽度也右缩一个滚动条宽度，使 + / − 对齐
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
                    // 取消键盘焦点：否则新增行时原先聚焦的 TextField 会把"残留的缓冲值"
                    // 写到新行（IMGUI 按绘制顺序分配控件 ID）导致显示错位。
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

                    // 使用即时 TextField 而非 DelayedTextField：PopupWindow 被外部点击关闭时
                    // 焦点直接丢失、OnGUI 不再调用，DelayedTextField 的暂存值无处提交，
                    // 会导致"写完一行失焦后不写回"的偶发丢数据。即时写回彻底规避该坑。
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
                    // 同上：删除后若不清焦点，IMGUI 会把焦点保留在"同坐标的下一行"，
                    // 视觉上像是编辑位置错乱。
                    GUI.FocusControl(null);
                    editorWindow.Repaint();
                }

                GUI.EndScrollView();
            }
        }
    }
}
#endif
