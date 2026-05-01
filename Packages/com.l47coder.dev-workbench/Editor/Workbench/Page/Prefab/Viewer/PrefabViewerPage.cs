using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace DevWorkbench.Editor
{
    internal sealed class PrefabViewerPage : IPage
    {
        public string GroupTitle => "Prefab";
        public string TabTitle   => "Viewer";

        private const float SplitterVisualW = 1f;
        private const float LeftPanelMin    = 100f;
        private const float LeftPanelMax    = 800f;
        private const float LeftPanelStart  = 220f;
        private static readonly Color SplitterColor = new(0.11f, 0.11f, 0.11f);

        private readonly PrefabViewerLeftPanel  _leftPanel  = new();
        private readonly PrefabViewerRightPanel _rightPanel = new();
        private float _splitterX = LeftPanelStart;
        private bool  _dragging;

        public void OnFirstEnter() => _leftPanel.OnFirstEnter(_rightPanel.SetPath);

        public void OnGUI(Rect rect)
        {
            var visualRect = new Rect(rect.x + _splitterX, rect.y, SplitterVisualW, rect.height);
            var hitRect    = new Rect(rect.x + _splitterX - 2f, rect.y, SplitterVisualW + 4f, rect.height);

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

            var leftRect  = new Rect(rect.x, rect.y, _splitterX, rect.height);
            var rightRect = new Rect(visualRect.xMax, rect.y,
                                     rect.width - _splitterX - SplitterVisualW, rect.height);

            _leftPanel.OnGUI(leftRect);
            EditorGUI.DrawRect(visualRect, SplitterColor);
            _rightPanel.OnGUI(rightRect);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Left Panel — file-system tree rooted at Assets/Game/Prefab
    // ─────────────────────────────────────────────────────────────────────────

    internal sealed class PrefabViewerLeftPanel
    {
        private readonly TreeView _treeView = new();

        public void OnFirstEnter(Action<string> onSelected)
        {
            _treeView.StripDisplayExtensions = new() { ".prefab" };
            _treeView.OnNodeSelected(onSelected);
            _treeView.OnAddClicked(() => _treeView.CreateFolderAtSelected());
        }

        public void OnGUI(Rect rect) =>
            _treeView.Draw(rect, GameProjectPaths.PrefabRoot);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Right Panel — prefab overview (hierarchy + placeholder for EntityConfig)
    // ─────────────────────────────────────────────────────────────────────────

    internal sealed class PrefabViewerRightPanel
    {
        // Visual constants
        private const float Padding       = 8f;
        private const float RowH          = 18f;
        private const float SectionLabelH = 22f;
        private const float PreviewSize   = 56f;

        private static readonly Color HeaderBg  = new(0.13f, 0.13f, 0.13f);
        private static readonly Color SectionBg = new(0.14f, 0.14f, 0.15f);
        private static readonly Color RowAlt    = new(0.16f, 0.16f, 0.17f);
        private static readonly Color CompColor = new(0.55f, 0.78f, 1.00f);
        private static readonly Color GoColor   = new(0.90f, 0.90f, 0.90f);

        private string     _currentPath;
        private string     _cachedPath;
        private GameObject _cachedPrefab;
        private Texture2D  _cachedPreview;
        private Vector2    _scrollPos;

        // Addressable info (refreshed with cache)
        private bool   _isAddressable;
        private string _addrAddress;
        private string _addrGroup;
        private string _addrLabels;
        private AddressableAssetGroup _addrGroupAsset;

        // Pre-built flat row list to avoid per-frame allocations
        private readonly List<HierarchyRow> _rows = new();

        private readonly struct HierarchyRow
        {
            public readonly string Name;
            public readonly int    Depth;
            public readonly bool   IsComponent;

            public HierarchyRow(string name, int depth, bool isComponent)
            {
                Name = name; Depth = depth; IsComponent = isComponent;
            }
        }

        // ── Public API ───────────────────────────────────────────────────────

        public void SetPath(string path)
        {
            if (_currentPath == path) return;
            _currentPath = path;
            _cachedPath  = null; // invalidate cache
        }

        // ── Draw entry ───────────────────────────────────────────────────────

        public void OnGUI(Rect rect)
        {
            if (string.IsNullOrEmpty(_currentPath))
            {
                GUI.Label(rect, "Nothing selected", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            if (Directory.Exists(_currentPath))
            {
                var inner = new Rect(rect.x + Padding, rect.y + Padding,
                                     rect.width - Padding * 2, rect.height - Padding * 2);
                GUI.Label(inner, _currentPath, EditorStyles.wordWrappedLabel);
                return;
            }

            if (!_currentPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                GUI.Label(rect, "Select a prefab file.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            RefreshCacheIfNeeded();

            if (_cachedPrefab == null)
            {
                GUI.Label(rect, "Failed to load prefab.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            DrawPanel(rect);
        }

        // ── Cache ─────────────────────────────────────────────────────────────

        private void RefreshCacheIfNeeded()
        {
            if (_cachedPath == _currentPath) return;

            _cachedPath    = _currentPath;
            _cachedPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(_currentPath);
            _cachedPreview = null;

            _rows.Clear();
            if (_cachedPrefab != null)
                CollectRows(_cachedPrefab.transform, 0);

            RefreshAddressableInfo();
        }

        private void RefreshAddressableInfo()
        {
            _isAddressable  = false;
            _addrAddress    = string.Empty;
            _addrGroup      = string.Empty;
            _addrLabels     = string.Empty;
            _addrGroupAsset = null;

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;

            var guid  = AssetDatabase.AssetPathToGUID(_currentPath);
            var entry = settings.FindAssetEntry(guid);
            if (entry == null) return;

            _isAddressable  = true;
            _addrAddress    = entry.address;
            _addrGroup      = entry.parentGroup?.Name ?? string.Empty;
            _addrLabels     = string.Join(", ", entry.labels);
            _addrGroupAsset = entry.parentGroup;
        }

        private void CollectRows(Transform t, int depth)
        {
            _rows.Add(new HierarchyRow(t.name, depth, false));
            foreach (var comp in t.GetComponents<Component>())
            {
                if (comp == null) continue;
                _rows.Add(new HierarchyRow(comp.GetType().Name, depth, true));
            }
            for (var i = 0; i < t.childCount; i++)
                CollectRows(t.GetChild(i), depth + 1);
        }

        // ── Panel layout ──────────────────────────────────────────────────────

        private void DrawPanel(Rect rect)
        {
            // Addressable header height varies by state
            var addrRowCount  = _isAddressable
                ? 2 + (string.IsNullOrEmpty(_addrLabels) ? 0 : 1) // address + group [+ labels]
                : 1;                                                // "未注册" hint
            var addrHeaderH   = Padding + addrRowCount * RowH + Padding + 22f + Padding;
            var hierarchyH    = SectionLabelH + _rows.Count * RowH;
            var placeholderH  = SectionLabelH + 36f;
            var contentH      = addrHeaderH + Padding
                                + hierarchyH + Padding
                                + placeholderH + Padding;

            var contentRect = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(contentH, rect.height));
            _scrollPos = GUI.BeginScrollView(rect, _scrollPos, contentRect);

            var y = 0f;
            var w = contentRect.width;

            DrawAddressableHeader(ref y, w, addrHeaderH);

            y += Padding;
            DrawSection(ref y, w, "GameObject 层级");
            DrawHierarchyRows(ref y, w);

            y += Padding;
            DrawSection(ref y, w, "EntityConfig 配置（待设计）");
            DrawPlaceholder(ref y, w);

            GUI.EndScrollView();
        }

        // ── Addressable header box ────────────────────────────────────────────
        // Left: prefab thumbnail | Right: addressable info + action button

        private void DrawAddressableHeader(ref float y, float w, float h)
        {
            EditorGUI.DrawRect(new Rect(0f, y, w, h), HeaderBg);

            // ── Thumbnail (left column) ──────────────────────────────────────
            if (_cachedPreview == null)
                _cachedPreview = AssetPreview.GetAssetPreview(_cachedPrefab);

            var thumbX = Padding;
            var thumbY = y + (h - PreviewSize) * 0.5f;
            var thumbRect = new Rect(thumbX, thumbY, PreviewSize, PreviewSize);
            if (_cachedPreview != null)
                GUI.DrawTexture(thumbRect, _cachedPreview, ScaleMode.ScaleToFit);
            else
                EditorGUI.DrawRect(thumbRect, new Color(0.22f, 0.22f, 0.22f));

            // ── Right column ─────────────────────────────────────────────────
            var rightX = thumbX + PreviewSize + Padding;
            var rightW = w - rightX - Padding;
            var ry     = y + Padding;

            if (!_isAddressable)
            {
                var hintStyle = new GUIStyle(EditorStyles.label)
                    { normal = { textColor = new Color(0.68f, 0.68f, 0.68f) } };
                EditorGUI.LabelField(new Rect(rightX, ry, rightW, RowH),
                    "此预制体尚未标记为 Addressable", hintStyle);
                ry += RowH + Padding;

                if (GUI.Button(new Rect(rightX, ry, 140f, 22f), "标记为 Addressable", EditorStyles.miniButton))
                    MarkAsAddressable();
            }
            else
            {
                DrawKeyValueAt(ref ry, rightX, rightW, "Address", _addrAddress);
                DrawKeyValueAt(ref ry, rightX, rightW, "Group",   _addrGroup);
                if (!string.IsNullOrEmpty(_addrLabels))
                    DrawKeyValueAt(ref ry, rightX, rightW, "Labels", _addrLabels);
                ry += Padding;

                if (GUI.Button(new Rect(rightX, ry, 140f, 22f), "在 Addressables 中查看", EditorStyles.miniButton))
                    OpenAddressablesWindow();
            }

            y += h;
        }

        // ── Section label bar ─────────────────────────────────────────────────

        private static void DrawSection(ref float y, float w, string title)
        {
            EditorGUI.DrawRect(new Rect(0f, y, w, SectionLabelH), SectionBg);
            EditorGUI.LabelField(new Rect(Padding, y + 2f, w - Padding * 2, SectionLabelH - 2f),
                title, EditorStyles.boldLabel);
            y += SectionLabelH;
        }

        // ── Hierarchy rows ────────────────────────────────────────────────────

        private void DrawHierarchyRows(ref float y, float w)
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                var row     = _rows[i];
                var rowRect = new Rect(0f, y, w, RowH);
                if (i % 2 == 1) EditorGUI.DrawRect(rowRect, RowAlt);

                var indentX = Padding + row.Depth * 14f + (row.IsComponent ? 14f : 0f);
                var lblRect = new Rect(indentX, y + 1f, w - indentX - Padding, RowH - 2f);

                if (row.IsComponent)
                {
                    var style = new GUIStyle(EditorStyles.miniLabel)
                        { normal = { textColor = CompColor } };
                    EditorGUI.LabelField(lblRect, $"  {row.Name}", style);
                }
                else
                {
                    var style = new GUIStyle(EditorStyles.label)
                        { normal = { textColor = GoColor }, fontStyle = FontStyle.Bold };
                    EditorGUI.LabelField(lblRect, row.Name, style);
                }

                y += RowH;
            }
        }

        // ── Key-value row ─────────────────────────────────────────────────────

        private static void DrawKeyValueAt(ref float y, float x, float w, string key, string value)
        {
            const float keyW = 52f;
            var keyStyle = new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = new Color(0.58f, 0.58f, 0.58f) } };
            EditorGUI.LabelField(new Rect(x, y, keyW, RowH), key, keyStyle);
            EditorGUI.LabelField(new Rect(x + keyW + 4f, y, w - keyW - 4f, RowH),
                value, EditorStyles.miniLabel);
            y += RowH;
        }

        // ── Addressable actions ───────────────────────────────────────────────

        private void MarkAsAddressable()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[PrefabViewer] 找不到 AddressableAssetSettings，请先在 Addressables 创建配置。");
                return;
            }

            var guid  = AssetDatabase.AssetPathToGUID(_cachedPath);
            var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
            entry.address = Path.GetFileNameWithoutExtension(_cachedPath);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            RefreshAddressableInfo();
        }

        private static void OpenAddressablesWindow() =>
            EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");

        // ── Placeholder for future EntityConfig panel ─────────────────────────

        private static void DrawPlaceholder(ref float y, float w)
        {
            EditorGUI.HelpBox(new Rect(Padding, y, w - Padding * 2, 36f),
                "预制体组件配置将在此展示，设计完成后替换此区域。",
                MessageType.None);
            y += 36f;
        }
    }
}
