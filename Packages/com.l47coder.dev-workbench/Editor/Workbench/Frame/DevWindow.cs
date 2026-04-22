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

        // 窗体打开（以及 domain reload 后 Installer rerun、Tools/Sync Runtime 菜单）时被统一调用，
        // 用于各模块自己贡献"项目结构性前置条件"。默认空实现；只有真正有前置条件要兜底的 Page
        // （当前是 ManagerViewerPage / ComponentViewerPage）才 override。
        //
        // 实现上必须：幂等、无覆盖用户合法数据、不依赖 UI 字段（会被独立反射实例化后调一次就丢弃）。
        void OnWorkbenchOpen() { }

        // 该 Page 第一次被激活（点开进入）时调用，负责 UI 一次性初始化——绑回调、建 TableView 之类。
        // 和 OnWorkbenchOpen 严格分开，不承担前置条件兜底职责。
        void OnFirstEnter() { }

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

        // 一次 Unity 编辑器会话里只跑一次"完整 ensure"（Frame 层 + 所有 Page.OnWorkbenchOpen）。
        // 用 SessionState 跨 domain reload 保留标记；重启 Unity 时清空，下次开窗会重新跑。
        private const string SessionKeyWorkbenchBooted = "DevWorkbench.Workbench.Booted";

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

        [MenuItem("Tools/Dev Workbench/Dev")]
        private static void Open()
        {
            // 用户主动从菜单打开窗口且当前没有存活实例时，清掉 SessionState 里的 booted flag，
            // 使得接下来的 OnEnable 会重新跑一次 RunFullEnsure。其余路径（Unity 启动恢复 docked
            // 窗口、Creator/删资产触发的 domain reload 重建）都不会走到这里。
            if (!HasOpenInstances<DevWindow>())
                SessionState.EraseBool(SessionKeyWorkbenchBooted);

            var window = GetWindow<DevWindow>("Dev Workbench", false);
            window.minSize = new Vector2(MenuWidth, MenuWidth);
            window.position = SearchUtils.GetMainWindowCenteredPosition(new Vector2(StartWidth, StartHeight));
        }

        private void OnEnable()
        {
            wantsMouseMove = true;

            // 一会话一次地跑完整 ensure：Frame 层 + 所有 Page 的 OnWorkbenchOpen。
            // 幂等无惊喜：东西都齐全时几乎不写盘；缺什么自动补。
            // domain reload 之后回来的 OnEnable 看到 flag=true 会跳过，避免每次编译都重扫。
            if (!SessionState.GetBool(SessionKeyWorkbenchBooted, false))
            {
                SessionState.SetBool(SessionKeyWorkbenchBooted, true);
                FrameworkBootstrapper.RunFullEnsure();
            }

            _pageOrder = TryLoadPageOrder();

            // 正常情况下 RunFullEnsure 里的 EnsureFrame 已经把 PageOrder.asset 创建好了；
            // 这里兜底：真的没加载到就短路，OnGUI 画空白——理论上只会出现在 EnsureFrame 异常路径。
            if (_pageOrder != null)
                BuildPageTree();
        }

        private void BuildPageTree()
        {
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

        // 只加载，不创建。PageOrder 的创建权在 FrameworkBootstrapper.EnsureFrame，
        // 避免"打开窗口即产生未经用户授意的资产写入"。
        private static PageOrder TryLoadPageOrder()
        {
            return AssetDatabase.LoadAssetAtPath<PageOrder>(PageOrderAssetPath);
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
                page.OnFirstEnter();
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
    }
}
