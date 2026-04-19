using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using DevWorkbench;

namespace DevWorkbench.Editor
{

internal sealed class AddressableViewerPage : IPage
{
    public string GroupTitle => "Addressable";
    public string TabTitle => "查看";

    private const float SplitterVisualW = 1f;
    private const float LeftPanelMin = 100f;
    private const float LeftPanelMax = 800f;
    private const float LeftPanelStart = 180f;
    private static readonly Color SplitterColor = new(0.11f, 0.11f, 0.11f);

    private readonly AddressableGroupPanel _leftPanel = new();
    private readonly AddressableEntryPanel _rightPanel = new();
    private float _splitterX = LeftPanelStart;
    private bool _dragging;

    public void OnStart() => _leftPanel.OnStart(_rightPanel.SetGroup, _rightPanel.Invalidate);

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

// ── 左侧：组列表面板 ──────────────────────────────────────────────────────────

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

    public void OnStart(Action<AddressableAssetGroup> onGroupSelected, Action onDropComplete)
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
                "未找到 AddressableAssetSettings。\n请先通过菜单 Window → Asset Management → Addressables → Groups 创建配置。",
                EditorStyles.wordWrappedLabel);
            return;
        }

        // 注意：settings.groups 在磁盘上永远是 GUID 字典序（见
        // AddressableAssetSettings.OnBeforeSerialize），真正的显示顺序来源是
        // AddressableAssetGroupSortSettings.sortOrder。这里必须按 sortOrder 排序，
        // 否则重新序列化/重启后就会"回到之前的顺序"。
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
            .Where(g => g != null
                        && !string.Equals(g.Name, "Built In Data", StringComparison.OrdinalIgnoreCase))
            .OrderBy(g => orderIdx.TryGetValue(g.Guid, out var i) ? i : int.MaxValue)
            .ThenBy(g => g.Name, StringComparer.Ordinal)
            .ToList();
    }

    private void HandleRenameGroup(int index, string oldName, string newName)
    {
        if (index < 0 || index >= _visibleGroups.Count) return;
        var group = _visibleGroups[index];
        if (string.IsNullOrWhiteSpace(newName) || group.Name == newName) return;
        group.Name = newName;
        EditorUtility.SetDirty(group);
        AssetDatabase.SaveAssets();
    }

    private void HandleDeleteGroup(int index, string label)
    {
        if (index < 0 || index >= _visibleGroups.Count) return;
        var group = _visibleGroups[index];
        if (!EditorUtility.DisplayDialog(
                "确认删除",
                $"确认删除组「{group.Name}」？组内的条目将被移除，但资产文件不受影响。",
                "删除", "取消"))
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

    // from / to 使用 _visibleGroups（过滤后）的索引空间；to 为 List.Insert 语义的插入位。
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

        // 同步修改 settings.groups 的内存顺序：本会话内原生 Groups 窗口 Reload 时
        // 会直接读它（Reload → SortGroups 先按 TreeViewState.sortOrder 排一遍，
        // 但 TreeViewState 缓存的 sortOrder 只有在 DeserializeState 时才从磁盘同步；
        // 把内存物理顺序一起改，可避免原生窗口在缓存未同步时显示不一致）。
        // 注意：SaveAssets 时 OnBeforeSerialize 会把 settings.groups 重新按 GUID
        // 字典序排，这里的改动不会持久化——持久化永远走 sortSettings.sortOrder。
        ReorderPhysicalGroups(settings, newVisible);

        // 触发原生 Groups 窗口 Reload+Repaint（GroupMoved 事件它不监听，用
        // BatchModification 强制刷新）。
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
    }

    // 把新的 visible 顺序写入 sortSettings.sortOrder。
    // 保持原 sortOrder 中非 visible 项（例如 Built In Data）的相对位置不变，
    // 只在它们原来占的"visible 槽位"上按 newVisible 顺序重新填。
    private static bool PersistReorderedSortOrder(
        AddressableAssetSettings settings,
        List<AddressableAssetGroup> newVisible)
    {
        var sortSettings = AddressableAssetGroupSortSettings.GetSettings(settings);
        if (sortSettings == null) return false;

        var allGroups = settings.groups.Where(g => g != null).ToList();
        var allGuidSet = new HashSet<string>(allGroups.Select(g => g.Guid), StringComparer.Ordinal);
        var visibleGuidSet = new HashSet<string>(newVisible.Select(g => g.Guid), StringComparer.Ordinal);

        // 构造 baseline：先按旧 sortOrder 顺序放入仍存在的 GUID，再把遗漏的按名字补到末尾。
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

        // 把 baseline 里 visible 槽位按 newVisible 顺序重新填入。
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

        // 关键：同步所有已打开的原生 Addressables Groups 窗口的 TreeViewState.sortOrder。
        // Addressables 原生窗口的 m_TreeState.sortOrder 是一份独立内存缓存，只在
        // 第一次 InitialiseEntryTree→DeserializeState 时才从磁盘读取；之后它 Reload
        // 用的就是这份内存缓存。更致命的是 OnDisable 里 SerializeState 会用这份
        // 缓存覆盖磁盘上的 sortSettings.asset——这就是用户看到的"只改磁盘不够、刷新
        // 一下又回到旧顺序"的根源。必须把新顺序直接写进它的内存缓存。
        SyncNativeGroupTreeSortOrder(newOrder);
        return true;
    }

    // 反射：遍历所有 AddressableAssetsWindow 实例，把新的 sortOrder 灌进
    // m_GroupEditor.m_TreeState.sortOrder，并让它的 TreeView 重绘。
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
            Debug.LogWarning($"[AddressableViewerPage] SyncNativeGroupTreeSortOrder 失败：{ex.Message}");
        }
    }

    private static void ReorderPhysicalGroups(
        AddressableAssetSettings settings,
        List<AddressableAssetGroup> newVisible)
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

// ── entry 行 DTO ──────────────────────────────────────────────────────────────

internal sealed class AddressableEntryRow
{
    [TableColumn(Header = "Address")]                    public string Address;
    [TableColumn(Header = "AssetPath", Editable = false)] public string AssetPath;
    [TableColumn(Header = "Labels")]                     public string Labels;
    [TableColumn(Visible = false)]                       public string Guid;
}

// ── 右侧：条目表格面板 ────────────────────────────────────────────────────────

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

    public AddressableEntryPanel()
    {
        _tableView.OnRowChanged<AddressableEntryRow>(SyncEntryFromRow);
    }

    public void SetGroup(AddressableAssetGroup group) => _currentGroup = group;

    // 外部调用：使缓存失效，下次 OnGUI 时重建行数据
    public void Invalidate() => _cachedEntryCount = -1;

    public void OnGUI(Rect rect)
    {
        var group = _currentGroup;
        if (group == null)
        {
            GUI.Label(rect, "未选中任何组", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        // group 切换或条目数变化时重建行数据
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
                Address  = entry.address,
                AssetPath = entry.AssetPath,
                Labels   = string.Join(", ", entry.labels),
                Guid     = entry.guid,
            });
        }
    }

    // 将 DTO 的编辑结果写回真实的 Addressable entry
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

        // Labels：逗号分隔字符串 → 差量更新
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

    // 在 TableView 处理事件之前检测拖拽发起（MouseDown 记录，MouseDrag 发起 DnD）
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
