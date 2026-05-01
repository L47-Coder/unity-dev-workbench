using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace DevWorkbench.Editor
{
    internal sealed class AddressableViewerPage : IPage
    {
        public string GroupTitle => "Addressable";
        public string TabTitle => "Viewer";

        private const float SplitterVisualW = 1f;
        private const float LeftPanelMin = 100f;
        private const float LeftPanelMax = 800f;
        private const float LeftPanelStart = 180f;
        private static readonly Color SplitterColor = new(0.11f, 0.11f, 0.11f);

        private readonly AddressableGroupPanel _leftPanel = new();
        private readonly AddressableEntryPanel _rightPanel = new();
        private float _splitterX = LeftPanelStart;
        private bool _dragging;

        public void OnFirstEnter() => _leftPanel.OnFirstEnter(_rightPanel.SetGroup, _rightPanel.Invalidate);

        public void OnGUI(Rect rect)
        {
            var visualRect = new Rect(rect.x + _splitterX, rect.y, SplitterVisualW, rect.height);
            var hitRect = new Rect(rect.x + _splitterX - 2f, rect.y, SplitterVisualW + 4f, rect.height);

            EditorGUIUtility.AddCursorRect(hitRect, MouseCursor.ResizeHorizontal);

            var evt = Event.current;
            switch (evt.type)
            {
                case EventType.MouseDown when hitRect.Contains(evt.mousePosition):
                    _dragging = true;
                    evt.Use();
                    break;
                case EventType.MouseDrag when _dragging:
                    var maxX = Mathf.Min(LeftPanelMax, rect.width - LeftPanelMin - SplitterVisualW);
                    _splitterX = Mathf.Clamp(evt.mousePosition.x - rect.x, LeftPanelMin, maxX);
                    evt.Use();
                    break;
                case EventType.MouseUp when _dragging:
                    _dragging = false;
                    evt.Use();
                    break;
            }

            var leftRect = new Rect(rect.x, rect.y, _splitterX, rect.height);
            var rightRect = new Rect(visualRect.xMax, rect.y, rect.width - _splitterX - SplitterVisualW, rect.height);

            _leftPanel.OnGUI(leftRect);
            EditorGUI.DrawRect(visualRect, SplitterColor);
            _rightPanel.OnGUI(rightRect);
        }
    }

    internal sealed class AddressableGroupPanel
    {
        private readonly ListView _listView = new()
        {
            CanReceiveDrop = true,
            CanReorder = true,
            IgnoredNames = { "Frame" },
        };
        private List<AddressableAssetGroup> _visibleGroups = new();
        private Action<AddressableAssetGroup> _onGroupSelected;
        private Action _onDropComplete;

        public void OnFirstEnter(Action<AddressableAssetGroup> onGroupSelected, Action onDropComplete)
        {
            _onGroupSelected = onGroupSelected;
            _onDropComplete = onDropComplete;
            _listView.OnRowSelected((idx, _) =>
                _onGroupSelected?.Invoke(idx >= 0 && idx < _visibleGroups.Count ? _visibleGroups[idx] : null));
            _listView.OnDropOnRow(HandleDropOnGroup);
            _listView.OnRowRenamed(HandleRenameGroup);
            _listView.OnRowDeleted(HandleDeleteGroup);
            _listView.OnRowReordered(HandleReorderGroup);
            _listView.OnAddClicked(() =>
            {
                var s = AddressableAssetSettingsDefaultObject.Settings;
                if (s == null) return;
                var name = "New Group";
                var idx = 0;
                while (s.groups.Any(g => g.Name == name))
                    name = $"New Group {++idx}";
                s.CreateGroup(name, false, false, false, null);
                EditorUtility.SetDirty(s);
                AssetDatabase.SaveAssets();
            });
        }

        public void OnGUI(Rect rect)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                var inner = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f);
                GUI.Label(inner,
                    "AddressableAssetSettings not found.\nUse Window → Asset Management → Addressables → Groups to create the configuration first.",
                    EditorStyles.wordWrappedLabel);
                return;
            }

            _visibleGroups = GetSortedVisibleGroups(settings);
            _listView.Draw(rect, _visibleGroups.Select(g => g.Name).ToList());
        }

        private static List<AddressableAssetGroup> GetSortedVisibleGroups(AddressableAssetSettings settings)
        {
            var sortSettings = AddressableAssetGroupSortSettings.GetSettings(settings);
            var order = sortSettings?.sortOrder ?? Array.Empty<string>();

            var orderIdx = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < order.Length; i++)
                if (!string.IsNullOrEmpty(order[i]))
                    orderIdx[order[i]] = i;

            return settings.groups
                .Where(g => g != null && !string.Equals(g.Name, "Built In Data", StringComparison.OrdinalIgnoreCase))
                .OrderBy(g => orderIdx.TryGetValue(g.Guid, out var i) ? i : int.MaxValue)
                .ThenBy(g => g.Name, StringComparer.Ordinal)
                .ToList();
        }

        private void HandleRenameGroup(int index, string oldName, string newName)
        {
            if (index < 0 || index >= _visibleGroups.Count) return;
            var group = _visibleGroups[index];
            if (string.IsNullOrWhiteSpace(newName) || group.Name == newName) return;
            try
            {
                AssetDatabase.StartAssetEditing();
                group.Name = newName;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            EditorUtility.SetDirty(group);
            AssetDatabase.SaveAssets();
        }

        private void HandleDeleteGroup(int index, string label)
        {
            if (index < 0 || index >= _visibleGroups.Count) return;
            var group = _visibleGroups[index];
            if (!EditorUtility.DisplayDialog("Confirm deletion", $"Delete group \"{group.Name}\"? Its entries will be removed, but the underlying asset files will not be touched.", "Delete", "Cancel"))
                return;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;
            settings.RemoveGroup(group);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private void HandleDropOnGroup(int targetIndex)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;
            if (targetIndex < 0 || targetIndex >= _visibleGroups.Count) return;

            var guid = DragAndDrop.GetGenericData("AddressableEntryGuid") as string;
            if (string.IsNullOrEmpty(guid)) return;

            var entry = settings.FindAssetEntry(guid);
            var targetGroup = _visibleGroups[targetIndex];
            if (entry == null || entry.parentGroup == targetGroup) return;

            settings.MoveEntry(entry, targetGroup);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            _onDropComplete?.Invoke();
        }

        private void HandleReorderGroup(int from, int to)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;
            if (from < 0 || from >= _visibleGroups.Count) return;
            if (to < 0 || to > _visibleGroups.Count) return;

            var newVisible = new List<AddressableAssetGroup>(_visibleGroups);
            var src = newVisible[from];
            newVisible.RemoveAt(from);
            var insertAt = to > from ? to - 1 : to;
            insertAt = Mathf.Clamp(insertAt, 0, newVisible.Count);
            newVisible.Insert(insertAt, src);

            if (!PersistReorderedSortOrder(settings, newVisible))
                return;

            ReorderPhysicalGroups(settings, newVisible);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        }

        private static bool PersistReorderedSortOrder(AddressableAssetSettings settings, List<AddressableAssetGroup> newVisible)
        {
            var sortSettings = AddressableAssetGroupSortSettings.GetSettings(settings);
            if (sortSettings == null) return false;

            var allGroups = settings.groups.Where(g => g != null).ToList();
            var allGuidSet = new HashSet<string>(allGroups.Select(g => g.Guid), StringComparer.Ordinal);
            var visibleGuidSet = new HashSet<string>(newVisible.Select(g => g.Guid), StringComparer.Ordinal);

            var baseline = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var guid in sortSettings.sortOrder ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(guid)) continue;
                if (allGuidSet.Contains(guid) && seen.Add(guid))
                    baseline.Add(guid);
            }
            foreach (var g in allGroups.OrderBy(g => g.Name, StringComparer.Ordinal))
            {
                if (seen.Add(g.Guid))
                    baseline.Add(g.Guid);
            }

            var slotIndices = new List<int>(newVisible.Count);
            for (var i = 0; i < baseline.Count; i++)
                if (visibleGuidSet.Contains(baseline[i]))
                    slotIndices.Add(i);

            var newVisibleGuids = newVisible.Select(g => g.Guid).ToList();
            for (var k = 0; k < slotIndices.Count && k < newVisibleGuids.Count; k++)
                baseline[slotIndices[k]] = newVisibleGuids[k];

            var newOrder = baseline.ToArray();
            sortSettings.sortOrder = newOrder;
            EditorUtility.SetDirty(sortSettings);
            AssetDatabase.SaveAssetIfDirty(sortSettings);

            SyncNativeGroupTreeSortOrder(newOrder);
            return true;
        }

        private static void SyncNativeGroupTreeSortOrder(string[] newOrder)
        {
            try
            {
                var asm = typeof(AddressableAssetSettings).Assembly;
                var windowType = asm.GetType("UnityEditor.AddressableAssets.GUI.AddressableAssetsWindow");
                if (windowType == null) return;

                const BindingFlags bf = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                var groupEditorField = windowType.GetField("m_GroupEditor", bf);
                if (groupEditorField == null) return;

                var windows = Resources.FindObjectsOfTypeAll(windowType);
                foreach (var win in windows)
                {
                    var groupEditor = groupEditorField.GetValue(win);
                    if (groupEditor == null) continue;

                    var editorType = groupEditor.GetType();
                    var treeStateField = editorType.GetField("m_TreeState", bf);
                    var treeState = treeStateField?.GetValue(groupEditor);
                    if (treeState != null)
                    {
                        var sortOrderField = treeState.GetType()
                            .GetField("sortOrder", BindingFlags.Instance | BindingFlags.Public);
                        sortOrderField?.SetValue(treeState, (string[])newOrder.Clone());
                    }

                    var entryTreeField = editorType.GetField("m_EntryTree", bf);
                    var entryTree = entryTreeField?.GetValue(groupEditor);
                    if (entryTree != null)
                    {
                        var reload = entryTree.GetType().GetMethod("Reload",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        reload?.Invoke(entryTree, null);
                    }

                    if (win is EditorWindow ew) ew.Repaint();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AddressableViewerPage] SyncNativeGroupTreeSortOrder failed: {ex.Message}");
            }
        }

        private static void ReorderPhysicalGroups(AddressableAssetSettings settings, List<AddressableAssetGroup> newVisible)
        {
            var visibleGuidSet = new HashSet<string>(newVisible.Select(g => g.Guid), StringComparer.Ordinal);
            var visibleQueue = new Queue<AddressableAssetGroup>(newVisible);

            var rebuilt = new List<AddressableAssetGroup>(settings.groups.Count);
            foreach (var g in settings.groups)
            {
                if (g == null) { rebuilt.Add(g); continue; }
                rebuilt.Add(visibleGuidSet.Contains(g.Guid) ? visibleQueue.Dequeue() : g);
            }

            settings.groups.Clear();
            foreach (var g in rebuilt)
                settings.groups.Add(g);
        }
    }

    internal sealed class AddressableEntryRow
    {
        [TableColumn(Header = "Address")] public string Address;
        [TableColumn(Header = "AssetPath", Editable = false)] public string AssetPath;
        [TableColumn(Header = "Labels")] public string Labels;
        [TableColumn(Visible = false)] public string Guid;
    }

    internal sealed class AddressableEntryPanel
    {
        private readonly TableView _tableView = new()
        {
            CanAdd = false,
            CanRemove = false,
            CanDrag = false,
            ShowToolbarButtons = false,
            SearchField = "Address",
        };

        private AddressableAssetGroup _currentGroup;
        private AddressableAssetGroup _cachedGroup;
        private int _cachedEntryCount = -1;
        private List<AddressableEntryRow> _rows = new();
        private List<AddressableAssetEntry> _entries = new();
        private string _pressedGuid;

        public AddressableEntryPanel() => _tableView.OnRowChanged<AddressableEntryRow>(SyncEntryFromRow);

        public void SetGroup(AddressableAssetGroup group) => _currentGroup = group;

        public void Invalidate() => _cachedEntryCount = -1;

        public void OnGUI(Rect rect)
        {
            var group = _currentGroup;
            if (group == null)
            {
                GUI.Label(rect, "No group selected", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            if (_cachedGroup != group || _cachedEntryCount != group.entries.Count)
            {
                _cachedGroup = group;
                _cachedEntryCount = group.entries.Count;
                RebuildRows(group);
            }

            HandleEntryDragStart(rect);
            _tableView.Draw(rect, _rows);
        }

        private void RebuildRows(AddressableAssetGroup group)
        {
            _rows.Clear();
            _entries.Clear();
            foreach (var entry in group.entries)
            {
                _entries.Add(entry);
                _rows.Add(new AddressableEntryRow
                {
                    Address = entry.address,
                    AssetPath = entry.AssetPath,
                    Labels = string.Join(", ", entry.labels),
                    Guid = entry.guid,
                });
            }
        }

        private void SyncEntryFromRow(int index, AddressableEntryRow row)
        {
            if (index < 0 || index >= _entries.Count) return;

            var entry = _entries[index];
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;

            var dirty = false;

            if (row.Address != entry.address)
            {
                entry.address = row.Address;
                dirty = true;
            }

            var newLabels = row.Labels
                .Split(',')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var l in entry.labels.Where(l => !newLabels.Contains(l)).ToList())
            {
                entry.SetLabel(l, false);
                dirty = true;
            }
            foreach (var l in newLabels.Where(l => !entry.labels.Contains(l)))
            {
                settings.AddLabel(l);
                entry.SetLabel(l, true);
                dirty = true;
            }

            if (dirty)
            {
                EditorUtility.SetDirty(entry.parentGroup);
                AssetDatabase.SaveAssets();
            }
        }

        private void HandleEntryDragStart(Rect rect)
        {
            var e = Event.current;

            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                var boxOffset = BoxDrawer.Padding + BoxDrawer.BorderWidth;
                var toolbarH = 20f;
                var rowH = EditorGUIUtility.singleLineHeight + 8f;
                var localY = e.mousePosition.y - rect.y - boxOffset - toolbarH - rowH;
                var rowIdx = Mathf.FloorToInt(localY / rowH);

                _pressedGuid = (rowIdx >= 0 && rowIdx < _rows.Count) ? _rows[rowIdx].Guid : null;
            }

            if (e.type == EventType.MouseDrag && e.button == 0 && !string.IsNullOrEmpty(_pressedGuid))
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData("AddressableEntryGuid", _pressedGuid);
                DragAndDrop.objectReferences = Array.Empty<UnityEngine.Object>();
                var row = _rows.FirstOrDefault(r => r.Guid == _pressedGuid);
                DragAndDrop.StartDrag(row?.Address ?? _pressedGuid);
                _pressedGuid = null;
                e.Use();
            }

            if (e.type == EventType.MouseUp)
                _pressedGuid = null;
        }
    }
}
