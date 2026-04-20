using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // Manager / Installer Tab：展示 manifest.json 里登记的可选默认 Manager 包，
    // 让用户勾选要导入哪些；已安装的包显示"Installed"并禁用 Toggle。
    // 点击 Import 后调用 DefaultManagerInstaller.InstallPackages，Unity 重新编译完
    // FrameworkBootstrapper.TryRerunInitializeAfterReload 会把剩下的挂载补齐。
    internal sealed class ManagerInstallerPage : IPage
    {
        public string GroupTitle => "Manager";
        public string TabTitle => "Installer";

        private const float HPad = 16f;
        private const float VPad = 16f;
        private const float SectionSpacing = 10f;
        private const float CardPad = 14f;
        private const float RowHeight = 62f;
        private const float RowSpacing = 8f;
        private const float ImportButtonHeight = 38f;
        private const float ToggleBoxSize = 18f;
        private const float SelectAllRowHeight = 28f;

        private static readonly Color BgColor = new(0.17f, 0.17f, 0.17f);
        private static readonly Color CardBg = new(0.215f, 0.215f, 0.215f);
        private static readonly Color CardBorder = new(0.13f, 0.13f, 0.13f);
        private static readonly Color RowSelectableBg = new(0.23f, 0.23f, 0.23f);
        private static readonly Color RowInstalledBg = new(0.16f, 0.33f, 0.22f);
        private static readonly Color RowSelectableBorder = new(0.36f, 0.36f, 0.36f);
        private static readonly Color RowInstalledBorder = new(0.30f, 0.78f, 0.42f);
        private static readonly Color RowCheckedBg = new(0.20f, 0.35f, 0.50f);
        private static readonly Color RowCheckedBorder = new(0.35f, 0.65f, 1f);
        private static readonly Color AccentBlue = new(0.35f, 0.65f, 1f);
        private static readonly Color HeaderTextColor = new(0.78f, 0.84f, 0.94f);
        private static readonly Color DimTextColor = new(0.65f, 0.65f, 0.65f);
        private static readonly Color OkTextColor = new(0.50f, 0.85f, 0.60f);

        // 专门给"全选条"用的冷蓝灰色系，有别于 package 行的中性灰 / 深蓝。
        private static readonly Color SelectAllBg = new(0.19f, 0.21f, 0.25f);
        private static readonly Color SelectAllBgActive = new(0.22f, 0.30f, 0.42f);
        private static readonly Color SelectAllBorder = new(0.32f, 0.40f, 0.55f);
        private static readonly Color SelectAllBorderActive = new(0.45f, 0.70f, 1f);
        private static readonly Color SelectAllTextColor = new(0.82f, 0.88f, 0.98f);

        private readonly HashSet<string> _selected = new();
        private IReadOnlyList<DefaultManagerInstaller.PackageInfo> _packages;
        private Dictionary<string, bool> _installedState;
        private Vector2 _scroll;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        public void OnEnter() => RefreshState();

        public void OnLeave() => _scroll = Vector2.zero;

        private void RefreshState()
        {
            DefaultManagerInstaller.InvalidateManifestCache();
            _packages = DefaultManagerInstaller.LoadManifest();
            _installedState = _packages.ToDictionary(
                p => p.id,
                p => DefaultManagerInstaller.IsPackageInstalled(p.id));

            _selected.RemoveWhere(id => !_installedState.ContainsKey(id) || _installedState[id]);
        }

        // ── Drawing ───────────────────────────────────────────────────────────────

        public void OnGUI(Rect rect)
        {
            EditorGUI.DrawRect(rect, BgColor);

            if (_packages == null) RefreshState();

            var content = new Rect(rect.x + HPad, rect.y + VPad,
                rect.width - HPad * 2f, rect.height - VPad * 2f);

            GUILayout.BeginArea(content);
            try
            {
                DrawIntroCard();
                GUILayout.Space(SectionSpacing);
                if (_packages.Count > 0)
                {
                    DrawSelectAllRow();
                    GUILayout.Space(RowSpacing);
                }
                DrawPackageList();
                GUILayout.FlexibleSpace();
                GUILayout.Space(SectionSpacing);
                DrawImportButton();
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        // "全选条"刻意做成扁扁的一行 + 冷蓝灰色调，和下面的 package 行在身形和配色上明显区分，
        // 这样即便 ScrollView 出滚动条导致 package 行右边界向内收,两者的"错位"在视觉上也不突兀
        // —— 用户会把它读成独立的控制条，而不是列表中"某一行"。
        private void DrawSelectAllRow()
        {
            var installableCount = CountInstallable();
            var allSelected = installableCount > 0 && _selected.Count >= installableCount;
            var disabled = installableCount == 0;

            var rect = GUILayoutUtility.GetRect(0f, SelectAllRowHeight, GUILayout.ExpandWidth(true));

            var bg = allSelected && !disabled ? SelectAllBgActive : SelectAllBg;
            var border = allSelected && !disabled ? SelectAllBorderActive : SelectAllBorder;

            EditorGUI.DrawRect(rect, bg);
            DrawOutline(rect, border);

            // 跟下方 package 行用同一份 toggle rect（Unity 的 checkbox 是左对齐绘制、
            // 尺寸由 skin 固定，rect 只影响点击区和文字列起点），这样"选框 + 文字"两列完全对齐。
            var toggleRect = new Rect(
                rect.x + 14f,
                rect.y + (rect.height - ToggleBoxSize) * 0.5f,
                ToggleBoxSize, ToggleBoxSize);
            var countWidth = 90f;
            var textLeft = toggleRect.xMax + 12f;
            var labelRect = new Rect(
                textLeft, rect.y,
                rect.width - (textLeft - rect.x) - countWidth - 12f, rect.height);
            var countRect = new Rect(rect.xMax - countWidth - 12f, rect.y, countWidth, rect.height);

            using (new EditorGUI.DisabledScope(disabled))
            {
                var next = EditorGUI.Toggle(toggleRect, allSelected);
                if (!disabled && next != allSelected)
                    ToggleSelectAll(allSelected);
            }

            var labelText = disabled
                ? "All packages installed — nothing to import"
                : allSelected
                    ? "All installable packages selected (click to clear)"
                    : "Select all installable packages";
            EditorGUI.LabelField(labelRect, labelText, SelectAllLabelStyle);

            var countText = disabled ? "-" : $"{_selected.Count} selected";
            EditorGUI.LabelField(countRect, countText, SelectAllCountStyle);

            if (!disabled
                && Event.current.type == EventType.MouseDown
                && rect.Contains(Event.current.mousePosition)
                && !toggleRect.Contains(Event.current.mousePosition))
            {
                ToggleSelectAll(allSelected);
                GUI.FocusControl(null);
                Event.current.Use();
            }
        }

        private void ToggleSelectAll(bool currentlyAllSelected)
        {
            if (currentlyAllSelected) _selected.Clear();
            else SelectAllInstallable();
        }

        private void SelectAllInstallable()
        {
            foreach (var pkg in _packages)
            {
                if (IsInstalled(pkg.id)) continue;
                _selected.Add(pkg.id);
            }
        }

        private int CountInstallable()
        {
            var n = 0;
            foreach (var pkg in _packages)
                if (!IsInstalled(pkg.id)) n++;
            return n;
        }

        private bool IsInstalled(string id)
            => _installedState != null && _installedState.TryGetValue(id, out var v) && v;

        private void DrawIntroCard()
        {
            BeginCard();
            DrawHeader("Default Manager packages");
            GUILayout.Label(
                "Pick the default Managers you want and click Import. These packages are optional; only the Game.Managers.asmdef container is created by the framework itself.",
                IntroStyle);
            if (_packages.Count == 0)
            {
                GUILayout.Space(6f);
                EditorGUILayout.HelpBox(
                    "manifest.json is missing or empty. Please verify the package contents.",
                    MessageType.Warning);
            }
            EndCard();
        }

        private void DrawPackageList()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            try
            {
                foreach (var pkg in _packages)
                {
                    DrawPackageRow(pkg);
                    GUILayout.Space(RowSpacing);
                }
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawPackageRow(DefaultManagerInstaller.PackageInfo pkg)
        {
            var isInstalled = _installedState.TryGetValue(pkg.id, out var flag) && flag;
            // 已安装的行视觉上也显示为"勾选"——表达"这个包已经在项目里了"。
            // 只不过这个勾是只读的：DisabledScope 会把它锁住，用户动不了。
            var isChecked = isInstalled || _selected.Contains(pkg.id);

            var rect = GUILayoutUtility.GetRect(0f, RowHeight, GUILayout.ExpandWidth(true));

            Color bg, border;
            if (isInstalled) { bg = RowInstalledBg; border = RowInstalledBorder; }
            else if (isChecked) { bg = RowCheckedBg; border = RowCheckedBorder; }
            else { bg = RowSelectableBg; border = RowSelectableBorder; }

            EditorGUI.DrawRect(rect, bg);
            DrawOutline(rect, border);

            var toggleRect = new Rect(
                rect.x + 14f,
                rect.y + (rect.height - ToggleBoxSize) * 0.5f,
                ToggleBoxSize, ToggleBoxSize);
            var textLeft = toggleRect.xMax + 12f;
            var statusWidth = 100f;
            var titleRect = new Rect(textLeft, rect.y + 8f, rect.width - (textLeft - rect.x) - statusWidth - 14f, 20f);
            var descRect = new Rect(textLeft, rect.y + 28f, rect.width - (textLeft - rect.x) - statusWidth - 14f, rect.height - 32f);
            var statusRect = new Rect(rect.xMax - statusWidth - 12f, rect.y, statusWidth, rect.height);

            using (new EditorGUI.DisabledScope(isInstalled))
            {
                var next = EditorGUI.Toggle(toggleRect, isChecked);
                if (!isInstalled && next != isChecked)
                {
                    if (next) _selected.Add(pkg.id);
                    else _selected.Remove(pkg.id);
                }
            }

            EditorGUI.LabelField(titleRect, pkg.displayName ?? pkg.id, TitleStyle);
            EditorGUI.LabelField(descRect, pkg.description ?? string.Empty, DescStyle);

            var statusText = isInstalled
                ? "Installed"
                : pkg.recommended ? "Recommended" : "Optional";
            var statusStyle = isInstalled ? InstalledStatusStyle
                : pkg.recommended ? RecommendedStatusStyle : OptionalStatusStyle;
            EditorGUI.LabelField(statusRect, statusText, statusStyle);

            // 点击整行时切换勾选（已安装的行是只读态，不响应）。
            if (!isInstalled
                && Event.current.type == EventType.MouseDown
                && rect.Contains(Event.current.mousePosition)
                && !toggleRect.Contains(Event.current.mousePosition))
            {
                if (_selected.Contains(pkg.id)) _selected.Remove(pkg.id);
                else _selected.Add(pkg.id);
                GUI.FocusControl(null);
                Event.current.Use();
            }
        }

        private void DrawImportButton()
        {
            var hasSelection = _selected.Count > 0;
            var prevBg = GUI.backgroundColor;
            if (hasSelection) GUI.backgroundColor = AccentBlue;

            using (new EditorGUI.DisabledScope(!hasSelection))
            {
                var label = hasSelection
                    ? $"Import {_selected.Count} package{(_selected.Count > 1 ? "s" : "")}"
                    : "Import Selected";
                if (GUILayout.Button(label, GUILayout.Height(ImportButtonHeight)))
                    PerformImport();
            }

            GUI.backgroundColor = prevBg;
        }

        private void PerformImport()
        {
            var ids = _selected.ToList();
            if (ids.Count == 0) return;

            var installed = DefaultManagerInstaller.InstallPackages(ids);
            _selected.Clear();
            RefreshState();

            if (installed > 0)
                Debug.Log($"[ManagerInstallerPage] Imported {installed} default Manager package(s). Unity will recompile and the remaining configuration will be applied automatically.");
        }

        // ── Chrome ────────────────────────────────────────────────────────────────

        private static void BeginCard()
        {
            var rect = EditorGUILayout.BeginVertical();
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, CardBg);
                DrawOutline(rect, CardBorder);
            }
            GUILayout.Space(CardPad);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(CardPad);
            EditorGUILayout.BeginVertical();
        }

        private static void EndCard()
        {
            EditorGUILayout.EndVertical();
            GUILayout.Space(CardPad);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(CardPad);
            EditorGUILayout.EndVertical();
        }

        private static void DrawHeader(string title)
        {
            GUILayout.Label(title, HeaderStyle);
            GUILayout.Space(3f);
            var line = GUILayoutUtility.GetRect(0f, 2f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(line, new Color(0.30f, 0.30f, 0.30f));
            GUILayout.Space(10f);
        }

        private static void DrawOutline(Rect r, Color c)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }

        // ── Styles ────────────────────────────────────────────────────────────────

        private static GUIStyle _headerStyle;
        private static GUIStyle HeaderStyle => _headerStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            normal = { textColor = HeaderTextColor },
        };

        private static GUIStyle _introStyle;
        private static GUIStyle IntroStyle => _introStyle ??= new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            wordWrap = true,
            normal = { textColor = DimTextColor },
        };

        private static GUIStyle _titleStyle;
        private static GUIStyle TitleStyle => _titleStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.92f, 0.92f, 0.92f) },
        };

        private static GUIStyle _descStyle;
        private static GUIStyle DescStyle => _descStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 11,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = DimTextColor },
        };

        private static GUIStyle _installedStatusStyle;
        private static GUIStyle InstalledStatusStyle => _installedStatusStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = OkTextColor },
        };

        private static GUIStyle _recommendedStatusStyle;
        private static GUIStyle RecommendedStatusStyle => _recommendedStatusStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = AccentBlue },
        };

        private static GUIStyle _optionalStatusStyle;
        private static GUIStyle OptionalStatusStyle => _optionalStatusStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = DimTextColor },
        };

        private static GUIStyle _selectAllLabelStyle;
        private static GUIStyle SelectAllLabelStyle => _selectAllLabelStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = SelectAllTextColor },
        };

        private static GUIStyle _selectAllCountStyle;
        private static GUIStyle SelectAllCountStyle => _selectAllCountStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = DimTextColor },
        };
    }
}
