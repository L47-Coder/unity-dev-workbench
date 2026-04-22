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
        void OnWorkbenchOpen() { }
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
        private static readonly Color MenuBg = new(0.14f, 0.14f, 0.14f);
        private static readonly Color ContentBg = new(0.18f, 0.18f, 0.18f);
        private static readonly Color DividerCol = new(0.11f, 0.11f, 0.11f);
        private static readonly Color SelectedBg = new(0.22f, 0.22f, 0.22f);
        private static readonly Color HoverBg = new(0.18f, 0.18f, 0.18f);
        private static readonly Color Accent = new(0.35f, 0.65f, 1f);
        private static readonly Color DimText = new(0.70f, 0.70f, 0.70f);
        private readonly static GUIStyle _menuStyle = new(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(16, 0, 0, 0),
            fontSize = 13,
        };
        private readonly static GUIStyle _tabStyle = new(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
        };

        private sealed class PageGroup
        {
            public string Title;
            public readonly List<IPage> Pages = new();
        }

        private readonly List<PageGroup> _groups = new();
        private readonly HashSet<IPage> _initializedPages = new();
        private PageOrder _pageOrder;
        private PageGroup _currentGroup;
        private IPage _currentPage;
        private string _draggingGroupTitle;
        private string _draggingTabTitle;
        [SerializeField] private string _persistedGroupTitle; //记录编译前的GroupTitle
        [SerializeField] private string _persistedTabTitle; //记录编译前的TabTitle

        [MenuItem("Tools/Dev Workbench/Dev")]
        private static void Open()
        {
            FrameworkBootstrapper.RunFullEnsure(); //构建架构所需

            var window = GetWindow<DevWindow>("Dev Workbench", false);
            window.minSize = new Vector2(MenuWidth, MenuWidth);
            window.position = SearchUtils.GetMainWindowCenteredPosition(new Vector2(StartWidth, StartHeight));
        }

        private void OnEnable()
        {
            wantsMouseMove = true;

            _pageOrder = AssetDatabase.LoadAssetAtPath<PageOrder>(FrameAssetInstaller.PageOrderAssetPath);

            if (_pageOrder != null)
                BuildPageTree();
        }

        private void OnDisable() => _currentPage?.OnLeave();

        private void BuildPageTree()
        {
            var pages = new List<IPage>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch { continue; }

                foreach (var t in types)
                {
                    if (!typeof(IPage).IsAssignableFrom(t) || t.IsInterface || t.IsAbstract) continue;
                    try
                    {
                        if (Activator.CreateInstance(t) is IPage page)
                            pages.Add(page);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[DevWindow] Failed to instantiate {t.FullName}: {ex.Message}");
                    }
                }
            }
            if (pages.Count == 0) return;

            // 同步 group/tab 顺序到 PageOrder SO：保留旧顺序、新项追加末尾、失效项删除。
            var groupOrder = SyncOrderMap(pages.Select(p => p.GroupTitle).Distinct(), _pageOrder.GetGroupDict());
            _pageOrder.SetGroupDict(groupOrder);

            var tabOrders = new Dictionary<string, Dictionary<string, int>>();
            foreach (var g in groupOrder.Keys)
            {
                var tabOrder = SyncOrderMap(pages.Where(p => p.GroupTitle == g).Select(p => p.TabTitle), _pageOrder.GetTabDict(g));
                _pageOrder.SetTabDict(g, tabOrder);
                tabOrders[g] = tabOrder;
            }
            EditorUtility.SetDirty(_pageOrder);
            AssetDatabase.SaveAssets();

            // 按存储顺序排列、归组。
            _groups.Clear();
            foreach (var page in pages.OrderBy(p => groupOrder[p.GroupTitle]).ThenBy(p => tabOrders[p.GroupTitle][p.TabTitle]))
            {
                var group = _groups.FirstOrDefault(x => x.Title == page.GroupTitle);

                if (group == null)
                    _groups.Add(group = new PageGroup { Title = page.GroupTitle });
                
                group.Pages.Add(page);
            }

            // 恢复上次选中项，匹配不上回落到第一个。
            _currentGroup = _groups.FirstOrDefault(g => g.Title == _persistedGroupTitle) ?? _groups[0];
            _currentPage = _currentGroup.Pages.FirstOrDefault(p => p.TabTitle == _persistedTabTitle) ?? _currentGroup.Pages[0];
            _persistedGroupTitle = _currentGroup.Title;
            _persistedTabTitle = _currentPage.TabTitle;
            ActivatePage(_currentPage);
        }

        // 保留 stored 中仍有效的顺序，新 key 追加末尾，失效 key 丢弃；返回 key→index 映射。
        private Dictionary<string, int> SyncOrderMap(IEnumerable<string> activeKeys, Dictionary<string, int> stored)
        {
            var active = activeKeys.ToList();
            var activeSet = new HashSet<string>(active);

            var ordered = stored
                .Where(kv => activeSet.Contains(kv.Key))
                .OrderBy(kv => kv.Value)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in active.Where(k => !ordered.Contains(k)))
                ordered.Add(key);

            return ordered.Select((k, i) => (k, i)).ToDictionary(x => x.k, x => x.i);
        }

        private void ActivatePage(IPage page)
        {
            if (_initializedPages.Add(page))
                page.OnFirstEnter();
            
            page.OnEnter();
        }

        private void SelectPage(IPage page)
        {
            if (page == _currentPage) return;
            _currentPage?.OnLeave();
            _currentGroup = _groups.First(g => g.Pages.Contains(page));
            _currentPage = page;
            _persistedGroupTitle = _currentGroup.Title;
            _persistedTabTitle = _currentPage.TabTitle;
            ActivatePage(page);
        }

        // ── Drawing ──────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (_groups.Count == 0) return;

            var contentX = MenuWidth + DividerWidth;
            var contentW = Mathf.Max(0f, position.width - contentX);
            var menuRect = new Rect(0f, 0f, MenuWidth, position.height);
            var headerRect = new Rect(contentX, 0f, contentW, HeaderHeight);
            var bodyRect = new Rect(contentX, HeaderHeight + DividerWidth, contentW, Mathf.Max(0f, position.height - HeaderHeight - DividerWidth));

            EditorGUI.DrawRect(menuRect, MenuBg);
            DrawMenuItems(menuRect);
            EditorGUI.DrawRect(new Rect(MenuWidth, 0f, DividerWidth, position.height), DividerCol);

            EditorGUI.DrawRect(headerRect, MenuBg);
            DrawTabs(headerRect);
            EditorGUI.DrawRect(new Rect(contentX, HeaderHeight, contentW, DividerWidth), DividerCol);
            EditorGUI.DrawRect(bodyRect, ContentBg);
            _currentPage.OnGUI(bodyRect);

            if (Event.current.type is EventType.MouseMove or EventType.MouseEnterWindow or EventType.MouseLeaveWindow)
                Repaint();
        }

        private void DrawMenuItems(Rect rect)
        {
            var evt = Event.current;
            for (var i = 0; i < _groups.Count; i++)
            {
                var group = _groups[i];
                var btnRect = new Rect(rect.x, rect.y + i * MenuButtonHeight, rect.width, MenuButtonHeight);
                var selected = group == _currentGroup;
                var hovered = btnRect.Contains(evt.mousePosition);

                if (selected)
                {
                    EditorGUI.DrawRect(btnRect, SelectedBg);
                    EditorGUI.DrawRect(new Rect(btnRect.x, btnRect.y, 4f, btnRect.height), Accent);
                }
                else if (hovered) EditorGUI.DrawRect(btnRect, HoverBg);

                _menuStyle.normal.textColor = selected ? Color.white : DimText;
                GUI.Label(btnRect, group.Title, _menuStyle);

                switch (evt.type)
                {
                    case EventType.MouseDown when evt.button == 0 && hovered:
                        if (!selected) SelectPage(group.Pages[0]);
                        _draggingGroupTitle = group.Title;
                        evt.Use();
                        break;
                    case EventType.MouseDrag when _draggingGroupTitle != null && hovered && group.Title != _draggingGroupTitle:
                        SwapOrder(
                            _pageOrder.GetGroupDict(), _draggingGroupTitle, group.Title,
                            d => { _pageOrder.SetGroupDict(d); _groups.Sort((x, y) => d[x.Title].CompareTo(d[y.Title])); });
                        evt.Use();
                        return;
                }
            }
            if (evt.rawType == EventType.MouseUp) _draggingGroupTitle = null;
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
                var selected = page == _currentPage;
                var hovered = tabRect.Contains(evt.mousePosition);

                if (selected)
                {
                    EditorGUI.DrawRect(tabRect, SelectedBg);
                    EditorGUI.DrawRect(new Rect(tabRect.x, tabRect.yMax - 2f, tabRect.width, 2f), Accent);
                }
                else if (hovered) EditorGUI.DrawRect(tabRect, HoverBg);

                _tabStyle.normal.textColor = selected ? Color.white : DimText;
                GUI.Label(tabRect, page.TabTitle, _tabStyle);

                switch (evt.type)
                {
                    case EventType.MouseDown when evt.button == 0 && hovered:
                        SelectPage(page);
                        _draggingTabTitle = page.TabTitle;
                        evt.Use();
                        break;
                    case EventType.MouseDrag when _draggingTabTitle != null && hovered && page.TabTitle != _draggingTabTitle:
                        SwapOrder(
                            _pageOrder.GetTabDict(_currentGroup.Title), _draggingTabTitle, page.TabTitle,
                            d => { _pageOrder.SetTabDict(_currentGroup.Title, d); _currentGroup.Pages.Sort((x, y) => d[x.TabTitle].CompareTo(d[y.TabTitle])); });
                        evt.Use();
                        return;
                }
            }
            if (evt.rawType == EventType.MouseUp) _draggingTabTitle = null;
        }

        // 交换字典里两个 key 的顺序值，持久化并触发对 UI 列表的排序回调。
        private void SwapOrder(Dictionary<string, int> dict, string a, string b, Action<Dictionary<string, int>> commit)
        {
            if (!dict.TryGetValue(a, out var ia) || !dict.TryGetValue(b, out var ib)) return;
            (dict[a], dict[b]) = (ib, ia);
            commit(dict);
            EditorUtility.SetDirty(_pageOrder);
            AssetDatabase.SaveAssets();
        }
    }
}
