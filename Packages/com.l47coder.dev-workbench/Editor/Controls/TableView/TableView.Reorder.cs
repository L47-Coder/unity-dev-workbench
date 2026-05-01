#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    public sealed partial class TableView
    {
        private void HandleRowReorderInput(int controlId, Rect handleRect, Rect rowRect, int rowIndex, int arraySize, float rowHeight)
        {
            var e = Event.current;
            switch (e.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (e.button != 0 || !handleRect.Contains(e.mousePosition)) return;
                    GUI.FocusControl(null);
                    GUIUtility.hotControl = controlId;
                    BeginReorderSession(controlId, rowIndex, rowRect, rowHeight, arraySize);
                    e.Use();
                    return;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId) e.Use();
                    return;
            }
        }

        private void BeginReorderSession(int controlId, int rowIndex, Rect rowRect, float rowHeight, int arraySize)
        {
            _draggingOwner = this;
            _reorder = new ReorderSession
            {
                ArraySize = arraySize,
                ControlId = controlId,
                SourceIndex = rowIndex,
                InsertSlot = rowIndex,
                PickupOffsetY = Event.current.mousePosition.y - rowRect.yMin,
                SourceRowHeight = rowHeight,
                RowCurrentY = new float[arraySize],
                RowTargetY = new float[arraySize],
                RowVelocityY = new float[arraySize],
                GapCurrentY = rowRect.yMin,
                GapTargetY = rowRect.yMin,
                DragRowYTarget = rowRect.yMin,
                DragRowY = rowRect.yMin,
                PositionsInitialized = false
            };
            _lastUpdateTime = EditorApplication.timeSinceStartup;
            HookRepaintTick();
        }

        private void HandleActiveReorderLifecycle<T>(List<T> list)
        {
            if (_draggingOwner != this || _reorder == null) return;

            var e = Event.current;
            if (GUIUtility.hotControl != _reorder.ControlId)
            {
                if (e.rawType == EventType.MouseUp || e.rawType == EventType.Ignore)
                    EndReorderSession();
                return;
            }

            if (e.rawType == EventType.MouseUp && e.button == 0)
            {
                GUIUtility.hotControl = 0;
                ApplyReorder(list, _reorder.SourceIndex, _reorder.InsertSlot);
                EndReorderSession();
                e.Use();
                return;
            }

            if (e.rawType == EventType.Ignore)
            {
                GUIUtility.hotControl = 0;
                EndReorderSession();
            }
        }

        private void ApplyReorder<T>(List<T> list, int from, int insertSlot)
        {
            var rowCount = list.Count;
            if (from < 0 || from >= rowCount) return;
            var dest = Mathf.Clamp(insertSlot, 0, rowCount - 1);
            if (dest == from) return;

            if (_selectedIndex == from)
                _selectedIndex = dest;
            else if (from < dest && _selectedIndex > from && _selectedIndex <= dest)
                _selectedIndex--;
            else if (from > dest && _selectedIndex >= dest && _selectedIndex < from)
                _selectedIndex++;

            var item = list[from];
            list.RemoveAt(from);
            list.Insert(dest, item);
            GUI.changed = true;
        }

        private void EnsureSessionBuffers(int rowCount)
        {
            if (_reorder == null) return;
            if (_reorder.RowCurrentY != null && _reorder.RowCurrentY.Length == rowCount) return;
            Array.Resize(ref _reorder.RowCurrentY, rowCount);
            Array.Resize(ref _reorder.RowTargetY, rowCount);
            Array.Resize(ref _reorder.RowVelocityY, rowCount);
            _reorder.PositionsInitialized = false;
        }

        private void InitializeDragPositions(float topY, IReadOnlyList<float> rowHeights)
        {
            if (_reorder == null) return;
            var y = topY;
            for (var i = 0; i < rowHeights.Count; i++)
            {
                _reorder.RowCurrentY[i] = y;
                _reorder.RowTargetY[i] = y;
                _reorder.RowVelocityY[i] = 0f;
                y += rowHeights[i];
            }
            var sourceY = _reorder.SourceIndex >= 0 && _reorder.SourceIndex < _reorder.RowCurrentY.Length
                ? _reorder.RowCurrentY[_reorder.SourceIndex] : topY;
            _reorder.GapCurrentY = sourceY;
            _reorder.GapTargetY = sourceY;
            _reorder.GapVelocityY = 0f;
            _reorder.DragRowY = sourceY;
            _reorder.DragRowYTarget = sourceY;
            _reorder.LastBodyTopY = topY;
            _reorder.HasBodyTopY = true;
            _reorder.PositionsInitialized = true;
        }

        private void ApplyBodyScrollDelta(float bodyTopY)
        {
            if (_reorder == null) return;
            if (!_reorder.HasBodyTopY) { _reorder.LastBodyTopY = bodyTopY; _reorder.HasBodyTopY = true; return; }
            var deltaY = bodyTopY - _reorder.LastBodyTopY;
            if (Mathf.Abs(deltaY) < 0.01f) return;
            for (var i = 0; i < _reorder.RowCurrentY.Length; i++)
            {
                if (i == _reorder.SourceIndex) continue;
                _reorder.RowCurrentY[i] += deltaY;
                _reorder.RowTargetY[i] += deltaY;
            }
            _reorder.GapCurrentY += deltaY;
            _reorder.GapTargetY += deltaY;
            _reorder.LastBodyTopY = bodyTopY;
        }

        private void UpdateInsertSlotFromMouse(Rect bodyRect, IReadOnlyList<float> rowHeights, int rowCount)
        {
            if (_reorder == null || rowCount == 0) return;
            var dragRowTop = Mathf.Clamp(
                Event.current.mousePosition.y - _reorder.PickupOffsetY,
                bodyRect.yMin,
                Mathf.Max(bodyRect.yMin, bodyRect.yMax - _reorder.SourceRowHeight));
            var probeY = dragRowTop + _reorder.SourceRowHeight * 0.5f;

            var count = 0;
            var rowTop = bodyRect.yMin;
            for (var i = 0; i < rowHeights.Count; i++)
            {
                if (probeY > rowTop) count++;
                rowTop += rowHeights[i];
            }
            _reorder.InsertSlot = Mathf.Clamp(count - 1, 0, Math.Max(0, rowCount - 1));
            _reorder.DragRowYTarget = dragRowTop;
        }

        private void UpdateTargets(float topY, IReadOnlyList<float> rowHeights)
        {
            if (_reorder == null) return;
            var slotCursor = 0;
            var y = topY;
            for (var i = 0; i < rowHeights.Count; i++)
            {
                if (i == _reorder.SourceIndex) continue;
                if (slotCursor == _reorder.InsertSlot) y += _reorder.SourceRowHeight;
                _reorder.RowTargetY[i] = y;
                y += rowHeights[i];
                slotCursor++;
            }

            var gapY = topY;
            var remaining = 0;
            for (var i = 0; i < rowHeights.Count; i++)
            {
                if (i == _reorder.SourceIndex) continue;
                if (remaining >= _reorder.InsertSlot) break;
                gapY += rowHeights[i];
                remaining++;
            }
            _reorder.GapTargetY = gapY;
        }

        private void StepSessionAnimation(float dt)
        {
            if (_reorder == null) return;
            for (var i = 0; i < _reorder.RowCurrentY.Length; i++)
            {
                if (i == _reorder.SourceIndex) continue;
                _reorder.RowCurrentY[i] = Mathf.SmoothDamp(
                    _reorder.RowCurrentY[i], _reorder.RowTargetY[i],
                    ref _reorder.RowVelocityY[i], RowMoveSmoothTime, Mathf.Infinity, dt);
            }
            _reorder.GapCurrentY = Mathf.SmoothDamp(
                _reorder.GapCurrentY, _reorder.GapTargetY,
                ref _reorder.GapVelocityY, GapMoveSmoothTime, Mathf.Infinity, dt);
            _reorder.DragRowY = Mathf.SmoothDamp(
                _reorder.DragRowY, _reorder.DragRowYTarget,
                ref _reorder.DragRowVelY, DragRowSmoothTime, Mathf.Infinity, dt);
        }

        private static List<VisualRow> BuildVisualRows(
            Rect bodyRect, IReadOnlyList<float> rowHeights,
            IReadOnlyList<int> filteredIndices, bool dragging)
        {
            var rowCount = filteredIndices.Count;
            var rows = new List<VisualRow>(rowCount + (dragging ? 1 : 0));

            if (!dragging || _reorder == null)
            {
                var y = bodyRect.yMin;
                for (var i = 0; i < rowCount; i++)
                {
                    rows.Add(new VisualRow { RowIndex = filteredIndices[i], StripeIndex = i, DrawY = y, Height = rowHeights[i] });
                    y += rowHeights[i];
                }
                return rows;
            }

            rows.Add(new VisualRow
            {
                RowIndex = -1,
                StripeIndex = _reorder.InsertSlot,
                DrawY = _reorder.GapCurrentY,
                Height = _reorder.SourceRowHeight,
                IsGap = true
            });

            var stripe = 0;
            for (var i = 0; i < rowCount; i++)
            {
                if (i == _reorder.SourceIndex) continue;
                rows.Add(new VisualRow
                {
                    RowIndex = filteredIndices[i],
                    StripeIndex = stripe++,
                    DrawY = _reorder.RowCurrentY[i],
                    Height = rowHeights[i]
                });
            }
            return rows;
        }

        private void EndReorderSession()
        {
            _reorder = null;
            _draggingOwner = null;
            UnhookRepaintTick();
        }

        private static void HookRepaintTick()
        {
            if (_dragTickHooked) return;
            EditorApplication.update += RepaintTick;
            _dragTickHooked = true;
        }

        private static void UnhookRepaintTick()
        {
            if (!_dragTickHooked) return;
            EditorApplication.update -= RepaintTick;
            _dragTickHooked = false;
        }

        private static void RepaintTick()
        {
            if (_reorder == null) { UnhookRepaintTick(); return; }
            EditorWindow.mouseOverWindow?.Repaint();
        }

        private static float BeginFrameDelta()
        {
            var now = EditorApplication.timeSinceStartup;
            var dt = (float)(now - _lastUpdateTime);
            _lastUpdateTime = now;
            return Mathf.Clamp(dt, 0.0025f, 0.05f);
        }
    }
}
#endif
