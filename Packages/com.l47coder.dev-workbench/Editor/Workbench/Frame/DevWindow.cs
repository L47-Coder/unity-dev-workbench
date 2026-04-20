using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;

namespace DevWorkbench.Editor
{
    internal interface IPage
    {
        string GroupTitle { get; }
        string TabTitle { get; }
        void OnStart() { }
        void OnEnter() { }
        void OnGUI(Rect rect) { }
        void OnLeave() { }
    }

    internal sealed class DevWindow : EditorWindow
    {
        private const float StartWidth = 1200f;
        private const float StartHeight = 720f;
        private const float MenuWidth = 130f;
        private const float HeaderHeight = 25f;
        private const float DividerWidth = 1f;
        private const float MenuButtonHeight = 40f;
        private const string PageOrderAssetPath = FrameAssetInstaller.PageOrderAssetPath;

        // 一次 Unity 编辑器进程里只做一次完整性检测。用 SessionState 跨 domain reload 保留标记。
        // 重启 Unity 时 SessionState 清空，下次打开 Workbench 会重新检查。
        private const string SessionKeyBootstrapChecked = "DevWorkbench.DevWindow.BootstrapChecked";

        private static readonly Color MenuBgColor = new(0.14f, 0.14f, 0.14f);
        private static readonly Color ContentBgColor = new(0.18f, 0.18f, 0.18f);
        private static readonly Color DividerColor = new(0.11f, 0.11f, 0.11f);
        private static readonly Color SelectedBgColor = new(0.22f, 0.22f, 0.22f);
        private static readonly Color HoverBgColor = new(0.18f, 0.18f, 0.18f);
        private static readonly Color AccentColor = new(0.35f, 0.65f, 1f);
        private static readonly Color DimTextColor = new(0.70f, 0.70f, 0.70f);

        private static GUIStyle _menuItemStyle;
        private static GUIStyle MenuItemStyle => _menuItemStyle ??= new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(16, 0, 0, 0),
            fontSize = 13,
        };

        private static GUIStyle _tabItemStyle;
        private static GUIStyle TabItemStyle => _tabItemStyle ??= new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
        };

        private sealed class PageGroup
        {
            public string Title;
            public List<IPage> Pages = new();
        }

        private PageOrder _pageOrder;
        private List<PageGroup> _groups = new();
        private PageGroup _currentGroup;
        private IPage _currentPage;
        private readonly HashSet<IPage> _initializedPages = new();
        private string _draggingGroupTitle;
        private string _draggingTabTitle;

        // IPage 是接口，Unity 序列化扛不住 domain reload；但它们的 GroupTitle/TabTitle 是字符串，
        // Unity 会把带 [SerializeField] 的字段跨 domain reload 保留下来，OnEnable 里据此恢复选中项。
        [SerializeField] private string _persistedGroupTitle;
        [SerializeField] private string _persistedTabTitle;

        private FrameworkBootstrapper.Status _bootstrapStatus;
        private Vector2 _overlayScroll;

        [MenuItem("Tools/Dev Workbench")]
        private static void Open()
        {
            // 用户主动打开窗口且当前没有存活实例时，清掉 SessionState 里的 checked flag，
            // 使得接下来的 OnEnable 会跑一次完整性检测。其余路径（Unity 启动恢复 docked 窗口、
            // Creator/删资产触发的 domain reload 重建）都不会走到这里。
            if (!HasOpenInstances<DevWindow>())
                SessionState.EraseBool(SessionKeyBootstrapChecked);

            var window = GetWindow<DevWindow>("Dev Workbench", false);
            window.minSize = new Vector2(MenuWidth, MenuWidth);
            window.position = SearchUtils.GetMainWindowCenteredPosition(new Vector2(StartWidth, StartHeight));
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            _pageOrder = LoadOrCreatePageOrder();

            // 完整性检测策略：
            //   - 每次用户主动从菜单打开窗口时检测一次（Open() 里清 flag 来驱动）。
            //   - Unity 启动时自动恢复的 docked 窗口，SessionState 是空的，也会触发一次检测。
            //   - 所有由 domain reload 引起的 OnEnable 自动重建都会看到 flag=true 而跳过，
            //     彻底避开"脚本已改但 post-compile asset 还没铺好"的瞬态噪音。
            //   - 不订阅 EditorApplication.projectChanged，避免删资产的瞬间被瞬态判定为"不完整"。
            // 结构真的损坏了，靠重启 Unity 或用户主动关窗重开兜住；其余全交给 Initialise 按钮。
            if (!SessionState.GetBool(SessionKeyBootstrapChecked, false))
            {
                SessionState.SetBool(SessionKeyBootstrapChecked, true);
                RefreshBootstrapStatus();
            }

            var pages = CollectPages();
            if (pages.Count == 0)
                return;

            pages = SyncAndSortPages(pages);
            _groups = BuildGroups(pages);

            // 尝试恢复上次选中的 group/tab；匹配不上时回落到第一个 group 的第一个 tab。
            _currentGroup = (!string.IsNullOrEmpty(_persistedGroupTitle)
                    ? _groups.FirstOrDefault(g => g.Title == _persistedGroupTitle)
                    : null)
                ?? _groups.FirstOrDefault();

            _currentPage = (_currentGroup != null && !string.IsNullOrEmpty(_persistedTabTitle)
                    ? _currentGroup.Pages.FirstOrDefault(p => p.TabTitle == _persistedTabTitle)
                    : null)
                ?? _currentGroup?.Pages.FirstOrDefault();

            PersistCurrentSelection();
            if (_currentPage != null) ActivatePage(_currentPage);
        }

        private void OnDisable()
        {
            _currentPage?.OnLeave();
        }

        private void OnGUI()
        {
            // _bootstrapStatus 为 null = 本会话已跳过检测（见 OnEnable 注释），视为就绪走主 UI。
            // 只有真的检测过且不通过，才弹蒙版。
            if (_bootstrapStatus != null && !_bootstrapStatus.IsReady)
            {
                DrawBootstrapOverlay(new Rect(0f, 0f, position.width, position.height));
                return;
            }

            if (_groups.Count == 0 || _currentGroup == null || _currentPage == null)
                return;

            var menuRect = new Rect(0f, 0f, MenuWidth, position.height);
            var dividerRect = new Rect(MenuWidth, 0f, DividerWidth, position.height);
            var contentRect = new Rect(MenuWidth + DividerWidth, 0f, Mathf.Max(0f, position.width - MenuWidth - DividerWidth), position.height);

            EditorGUI.DrawRect(menuRect, MenuBgColor);
            DrawMenuItems(menuRect);
            EditorGUI.DrawRect(dividerRect, DividerColor);
            EditorGUI.DrawRect(contentRect, ContentBgColor);
            DrawContent(contentRect);

            if (Event.current.type is EventType.MouseMove or EventType.MouseEnterWindow or EventType.MouseLeaveWindow)
                Repaint();
        }

        // ── Setup ────────────────────────────────────────────────────────────────

        private static PageOrder LoadOrCreatePageOrder()
        {
            var asset = AssetDatabase.LoadAssetAtPath<PageOrder>(PageOrderAssetPath);
            if (asset != null)
                return asset;

            // PageOrder 记录用户对 DevWindow 菜单/标签的排序，属于"用户工程数据"，
            // 必须落在 Assets/ 下而不能放进包里（包在 PackageCache 下是只读的）。
            // 新工程可能还没有这个父目录，按需创建。
            var folder = System.IO.Path.GetDirectoryName(PageOrderAssetPath)?.Replace('\\', '/');
            FrameAssetInstaller.EnsureFolder(folder);

            asset = CreateInstance<PageOrder>();
            AssetDatabase.CreateAsset(asset, PageOrderAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return asset;
        }

        private static List<IPage> CollectPages()
        {
            var pages = new List<IPage>();
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .Where(t => typeof(IPage).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in types)
            {
                try
                {
                    if (Activator.CreateInstance(type) is IPage page)
                        pages.Add(page);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DevWindow] Failed to instantiate page {type.FullName}: {ex.Message}");
                }
            }

            return pages;
        }

        // 同步 SO 与实际页面（补新增、删多余），按存储顺序排列后返回
        private List<IPage> SyncAndSortPages(List<IPage> pages)
        {
            var groupOrder = SyncOrderMap(pages.Select(p => p.GroupTitle).Distinct(), _pageOrder.GetGroupDict());
            _pageOrder.SetGroupDict(groupOrder);

            var tabOrders = new Dictionary<string, Dictionary<string, int>>();
            foreach (var groupTitle in groupOrder.Keys)
            {
                var tabOrder = SyncOrderMap(
                    pages.Where(p => p.GroupTitle == groupTitle).Select(p => p.TabTitle),
                    _pageOrder.GetTabDict(groupTitle));
                _pageOrder.SetTabDict(groupTitle, tabOrder);
                tabOrders[groupTitle] = tabOrder;
            }

            EditorUtility.SetDirty(_pageOrder);
            AssetDatabase.SaveAssets();

            return pages
                .OrderBy(p => groupOrder[p.GroupTitle])
                .ThenBy(p => tabOrders[p.GroupTitle][p.TabTitle])
                .ToList();
        }

        // 保留 stored 中有效的顺序，新 key 追加末尾，失效 key 丢弃
        private static Dictionary<string, int> SyncOrderMap(IEnumerable<string> activeKeys, Dictionary<string, int> stored)
        {
            var active = activeKeys.ToList();
            var activeSet = new HashSet<string>(active);

            var result = stored
                .Where(kv => activeSet.Contains(kv.Key))
                .OrderBy(kv => kv.Value)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in active.Where(k => !result.Contains(k)))
                result.Add(key);

            return result.Select((k, i) => (k, i)).ToDictionary(x => x.k, x => x.i);
        }

        private static List<PageGroup> BuildGroups(List<IPage> pages)
        {
            var groups = new List<PageGroup>();
            foreach (var page in pages)
            {
                var group = groups.FirstOrDefault(g => g.Title == page.GroupTitle);
                if (group == null)
                {
                    group = new PageGroup { Title = page.GroupTitle };
                    groups.Add(group);
                }
                group.Pages.Add(page);
            }
            return groups;
        }

        // ── Interaction ──────────────────────────────────────────────────────────

        private void ActivatePage(IPage page)
        {
            if (_initializedPages.Add(page))
                page.OnStart();
            page.OnEnter();
        }

        private void SelectGroup(PageGroup group)
        {
            if (group == null || group == _currentGroup) return;
            var next = group.Pages.FirstOrDefault();
            if (next == null) return;
            _currentPage?.OnLeave();
            _currentGroup = group;
            _currentPage = next;
            PersistCurrentSelection();
            ActivatePage(_currentPage);
        }

        private void SelectPage(IPage page)
        {
            if (page == null || page == _currentPage) return;
            _currentPage?.OnLeave();
            _currentGroup = _groups.FirstOrDefault(g => g.Title == page.GroupTitle);
            _currentPage = page;
            PersistCurrentSelection();
            ActivatePage(_currentPage);
        }

        private void PersistCurrentSelection()
        {
            _persistedGroupTitle = _currentGroup?.Title;
            _persistedTabTitle = _currentPage?.TabTitle;
        }

        private void SwapGroupOrder(string a, string b)
        {
            var dict = _pageOrder.GetGroupDict();
            if (!dict.TryGetValue(a, out var oa) || !dict.TryGetValue(b, out var ob)) return;
            (dict[a], dict[b]) = (ob, oa);
            _pageOrder.SetGroupDict(dict);
            EditorUtility.SetDirty(_pageOrder);
            AssetDatabase.SaveAssets();
            _groups.Sort((x, y) => dict[x.Title].CompareTo(dict[y.Title]));
        }

        private void SwapTabOrder(string a, string b)
        {
            var dict = _pageOrder.GetTabDict(_currentGroup.Title);
            if (!dict.TryGetValue(a, out var oa) || !dict.TryGetValue(b, out var ob)) return;
            (dict[a], dict[b]) = (ob, oa);
            _pageOrder.SetTabDict(_currentGroup.Title, dict);
            EditorUtility.SetDirty(_pageOrder);
            AssetDatabase.SaveAssets();
            _currentGroup.Pages.Sort((x, y) => dict[x.TabTitle].CompareTo(dict[y.TabTitle]));
        }

        // ── Rendering ────────────────────────────────────────────────────────────

        private void DrawMenuItems(Rect rect)
        {
            var evt = Event.current;
            var currentY = rect.y;

            foreach (var group in _groups)
            {
                var buttonRect = new Rect(rect.x, currentY, rect.width, MenuButtonHeight);
                currentY += MenuButtonHeight;

                var isSelected = group == _currentGroup;
                var isHovered = buttonRect.Contains(evt.mousePosition);

                if (isSelected)
                {
                    EditorGUI.DrawRect(buttonRect, SelectedBgColor);
                    EditorGUI.DrawRect(new Rect(buttonRect.x, buttonRect.y, 4f, buttonRect.height), AccentColor);
                }
                else if (isHovered)
                    EditorGUI.DrawRect(buttonRect, HoverBgColor);

                MenuItemStyle.normal.textColor = isSelected ? Color.white : DimTextColor;
                GUI.Label(buttonRect, group.Title, MenuItemStyle);

                switch (evt.type)
                {
                    case EventType.MouseDown when evt.button == 0 && isHovered:
                        SelectGroup(group);
                        _draggingGroupTitle = group.Title;
                        evt.Use();
                        break;
                    case EventType.MouseDrag when _draggingGroupTitle != null && isHovered && group.Title != _draggingGroupTitle:
                        SwapGroupOrder(_draggingGroupTitle, group.Title);
                        evt.Use();
                        return;
                }
            }

            if (evt.rawType == EventType.MouseUp)
                _draggingGroupTitle = null;
        }

        private void DrawContent(Rect rect)
        {
            var headerRect = new Rect(rect.x, rect.y, rect.width, HeaderHeight);
            var dividerRect = new Rect(rect.x, rect.y + HeaderHeight, rect.width, DividerWidth);
            var bodyRect = new Rect(rect.x, rect.y + HeaderHeight + DividerWidth, rect.width, Mathf.Max(0f, rect.height - HeaderHeight - DividerWidth));

            EditorGUI.DrawRect(headerRect, MenuBgColor);
            DrawTabs(headerRect);
            EditorGUI.DrawRect(dividerRect, DividerColor);
            _currentPage.OnGUI(bodyRect);
        }

        private void DrawTabs(Rect rect)
        {
            var evt = Event.current;
            var tabs = _currentGroup.Pages;
            var tabWidth = Mathf.Min(96f, rect.width / Mathf.Max(1, tabs.Count));

            for (var i = 0; i < tabs.Count; i++)
            {
                var page = tabs[i];
                var tabRect = new Rect(rect.x + i * tabWidth, rect.y, tabWidth, rect.height);
                var isSelected = page == _currentPage;
                var isHovered = tabRect.Contains(evt.mousePosition);

                if (isSelected)
                {
                    EditorGUI.DrawRect(tabRect, SelectedBgColor);
                    EditorGUI.DrawRect(new Rect(tabRect.x, tabRect.yMax - 2f, tabRect.width, 2f), AccentColor);
                }
                else if (isHovered)
                    EditorGUI.DrawRect(tabRect, HoverBgColor);

                TabItemStyle.normal.textColor = isSelected ? Color.white : DimTextColor;
                GUI.Label(tabRect, page.TabTitle, TabItemStyle);

                switch (evt.type)
                {
                    case EventType.MouseDown when evt.button == 0 && isHovered:
                        SelectPage(page);
                        _draggingTabTitle = page.TabTitle;
                        evt.Use();
                        break;
                    case EventType.MouseDrag when _draggingTabTitle != null && isHovered && page.TabTitle != _draggingTabTitle:
                        SwapTabOrder(_draggingTabTitle, page.TabTitle);
                        evt.Use();
                        return;
                }
            }

            if (evt.rawType == EventType.MouseUp)
                _draggingTabTitle = null;
        }

        // ── Bootstrap Overlay ────────────────────────────────────────────────────

        private static readonly Color OverlayBgColor = new(0.06f, 0.06f, 0.06f, 0.92f);
        private static readonly Color OverlayCardColor = new(0.18f, 0.18f, 0.18f);
        private static readonly Color OverlayCardBorderColor = new(0.28f, 0.28f, 0.28f);
        private static readonly Color OverlayDividerColor = new(0.24f, 0.24f, 0.24f);
        private static readonly Color OverlayOkColor = new(0.42f, 0.82f, 0.54f);
        private static readonly Color OverlayBadColor = new(0.96f, 0.50f, 0.50f);
        private static readonly Color OverlayAccentColor = new(0.30f, 0.55f, 0.95f);
        private static readonly Color OverlayTitleColor = new(0.95f, 0.95f, 0.95f);
        private static readonly Color OverlayBodyColor = new(0.84f, 0.84f, 0.84f);
        private static readonly Color OverlayDimColor = new(0.62f, 0.62f, 0.62f);

        private GUIStyle _overlayTitleStyle;
        private GUIStyle _overlaySubtitleStyle;
        private GUIStyle _overlayItemLabelStyle;
        private GUIStyle _overlayItemDetailStyle;
        private GUIStyle _overlayIconStyle;
        private GUIStyle _overlayButtonStyle;

        private void EnsureOverlayStyles()
        {
            if (_overlayTitleStyle != null) return;

            _overlayTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = OverlayTitleColor },
            };
            _overlaySubtitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = OverlayDimColor },
            };
            _overlayItemLabelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                normal = { textColor = OverlayBodyColor },
            };
            _overlayItemDetailStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = OverlayDimColor },
            };
            _overlayIconStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
            };
            _overlayButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(24, 24, 10, 10),
                alignment = TextAnchor.MiddleCenter,
            };
        }

        private void RefreshBootstrapStatus()
        {
            _bootstrapStatus = FrameworkBootstrapper.CheckStatus();
            Repaint();
        }

        private void DrawBootstrapOverlay(Rect rect)
        {
            // 进入这里意味着 _bootstrapStatus != null 且 !IsReady（见 OnGUI 的守卫），
            // 所以不需要再做 null 兜底 refresh——那会绕过"一次会话只检测一次"的约定。
            EnsureOverlayStyles();
            EditorGUI.DrawRect(rect, OverlayBgColor);

            const float maxCardW = 560f;
            const float minCardW = 320f;
            const float cardPadding = 28f;
            const float cardMarginY = 36f;

            var cardW = Mathf.Clamp(rect.width - 80f, minCardW, maxCardW);
            var cardH = Mathf.Min(rect.height - cardMarginY * 2f, 500f);
            var cardRect = new Rect(
                rect.x + (rect.width - cardW) * 0.5f,
                rect.y + (rect.height - cardH) * 0.5f,
                cardW, cardH);

            var borderRect = new Rect(cardRect.x - 1f, cardRect.y - 1f, cardRect.width + 2f, cardRect.height + 2f);
            EditorGUI.DrawRect(borderRect, OverlayCardBorderColor);
            EditorGUI.DrawRect(cardRect, OverlayCardColor);

            var inner = new Rect(
                cardRect.x + cardPadding,
                cardRect.y + cardPadding,
                cardRect.width - cardPadding * 2f,
                cardRect.height - cardPadding * 2f);

            GUILayout.BeginArea(inner);

            GUILayout.Label("Framework not initialised", _overlayTitleStyle);
            GUILayout.Space(6f);

            var total = _bootstrapStatus.Checks.Count;
            var passed = 0;
            for (var i = 0; i < total; i++)
                if (_bootstrapStatus.Checks[i].Passed) passed++;

            GUILayout.Label(
                $"{passed} / {total} checks passed. Click the button below to fix the remaining issues.",
                _overlaySubtitleStyle);

            GUILayout.Space(14f);
            DrawOverlayDivider();
            GUILayout.Space(10f);

            _overlayScroll = EditorGUILayout.BeginScrollView(_overlayScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            foreach (var check in _bootstrapStatus.Checks)
                DrawBootstrapCheck(check);
            EditorGUILayout.EndScrollView();

            GUILayout.Space(10f);
            DrawOverlayDivider();
            GUILayout.Space(14f);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = OverlayAccentColor;
            if (GUILayout.Button("Initialise", _overlayButtonStyle, GUILayout.MinWidth(220f)))
                RunInitialization();
            GUI.backgroundColor = prevBg;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private static void DrawOverlayDivider()
        {
            var r = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, OverlayDividerColor);
        }

        private void DrawBootstrapCheck(FrameworkBootstrapper.Check check)
        {
            GUILayout.BeginHorizontal();
            var prevColor = GUI.contentColor;
            GUI.contentColor = check.Passed ? OverlayOkColor : OverlayBadColor;
            GUILayout.Label(check.Passed ? "✓" : "✕", _overlayIconStyle, GUILayout.Width(20f));
            GUI.contentColor = prevColor;

            GUILayout.Space(6f);
            GUILayout.BeginVertical();
            GUILayout.Label(check.Label, _overlayItemLabelStyle);
            if (!string.IsNullOrEmpty(check.Detail))
                GUILayout.Label(check.Detail, _overlayItemDetailStyle);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        private void RunInitialization()
        {
            try
            {
                FrameworkBootstrapper.InitializeAll();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DevWindow] Initialisation failed: {ex}");
                EditorUtility.DisplayDialog("Dev Workbench", $"Initialisation threw an exception:\n{ex.Message}", "OK");
            }

            _bootstrapStatus = FrameworkBootstrapper.CheckStatus();

            // 初始化完成后，已激活的 Page 可能在未就绪状态下跑过 OnStart 且拿不到资产，
            // 清掉标记让它们在下次激活时重跑一遍。
            if (_bootstrapStatus.IsReady)
            {
                _initializedPages.Clear();
                if (_currentPage != null) ActivatePage(_currentPage);
            }

            Repaint();
        }
    }
}
