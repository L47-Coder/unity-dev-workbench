using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    internal sealed class ManagerViewerPage : IPage
    {
        public string GroupTitle => "Manager";
        public string TabTitle => "Viewer";

        private const float SplitterVisualW = 1f;
        private const float LeftPanelMin = 100f;
        private const float LeftPanelMax = 800f;
        private const float LeftPanelStart = 220f;
        private static readonly Color SplitterColor = new(0.11f, 0.11f, 0.11f);

        private readonly ManagerLeftPanel _leftPanel = new();
        private readonly ManagerRightPanel _rightPanel = new();
        private float _splitterX = LeftPanelStart;
        private bool _dragging;

        // Manager 模块对"项目结构完整性"的贡献——由 FrameworkBootstrapper.RunFullEnsure
        // 通过 WorkbenchPageRunner 在开窗时统一调度。Frame 层已经保证了容器 asmdef、
        // Addressables settings 和 Order 资产，所以本方法只做 Frame 层管不到的两件事：
        //   - EnsureAllRegistered: 扫新编译出的 <Name>ManagerConfig 子类，创建 .asset、
        //     挂到 Addressables "ManagerConfig" 组、对齐地址；首次装完模板的 domain reload
        //     回来时靠这一步真正把 Config asset 造出来。
        //   - SyncManagerOrderEntries: 按 BaseManager 子类增量补行、移除陈旧项。
        // 两个都幂等，资产齐全时零写盘。
        public void OnWorkbenchOpen()
        {
            ManagerConfigInstaller.EnsureAllRegistered();

            var order = AssetDatabase.LoadAssetAtPath<ManagerOrderConfig>(FrameAssetInstaller.ManagerOrderAssetPath);
            if (order != null) FrameAssetInstaller.SyncManagerOrderEntries(order);
        }

        public void OnFirstEnter() => _leftPanel.OnFirstEnter(_rightPanel.SetPath);

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

    internal sealed class ManagerLeftPanel
    {
        private readonly TreeView _treeView = new();

        public void OnFirstEnter(Action<string> onSelected)
        {
            _treeView.IgnoredNames = new() { "**/Generated", "**/*Refresher.cs", "**/*.asmdef" };
            _treeView.OnNodeSelected(onSelected);
            _treeView.OnAddClicked(() => _treeView.CreateFolderAtSelected());
        }

        public void OnGUI(Rect rect) => _treeView.Draw(rect, "Assets/Game/Manager");
    }

    internal sealed class ManagerRightPanel
    {
        private readonly TextView _csTextView = new();
        private string _currentPath;

        private string _cachedCsPath;
        private string _cachedCsText;

        // .asset 文件缓存（管理器配置为 BaseManagerConfig，与 BaseComponentConfig 无继承关系）。
        // Addressable 地址合法性由 ManagerViewerPage.OnWorkbenchOpen 在开窗时的 EnsureAllRegistered
        // 幂等兜底；这里只负责显示/编辑已挂好的 asset。
        private string _cachedAssetPath;
        private BaseManagerConfig _cachedAsset;
        private object _cachedList;
        private MethodInfo _cachedDrawMethod;
        private TableView _tableView;

        // per-manager 的 Refresh 操作已取消——Refresher 现在走 Tools/Dev Workbench/Sync Runtime
        // 菜单统一执行，避免"一个个点容易落下"。保留"打开 Refresher 脚本"入口方便跳转去编辑。
        private MonoScript _cachedRefresherScript;

        public void SetPath(string path) => _currentPath = path;

        public void OnGUI(Rect rect)
        {
            if (string.IsNullOrEmpty(_currentPath))
            {
                GUI.Label(rect, "Nothing selected", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            if (Directory.Exists(_currentPath))
            {
                DrawFolder(rect, _currentPath);
                return;
            }

            switch (Path.GetExtension(_currentPath).ToLowerInvariant())
            {
                case ".cs": DrawCsFile(rect, _currentPath); break;
                case ".asset": DrawAssetFile(rect, _currentPath); break;
            }
        }

        private static void DrawFolder(Rect rect, string path)
        {
            var inner = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f);
            GUI.Label(inner, path, EditorStyles.wordWrappedLabel);
        }

        private void DrawCsFile(Rect rect, string path)
        {
            if (_cachedCsPath != path)
            {
                _cachedCsPath = path;
                _cachedCsText = File.Exists(path) ? File.ReadAllText(path) : "(failed to read file)";
            }
            _csTextView.Draw(rect, _cachedCsText);
        }

        private void DrawAssetFile(Rect rect, string path)
        {
            if (_cachedAssetPath != path)
            {
                _cachedAssetPath = path;
                _cachedAsset = AssetDatabase.LoadAssetAtPath<BaseManagerConfig>(path);
                _cachedList = null;
                _cachedDrawMethod = null;
                _tableView = null;
                _cachedRefresherScript = null;

                if (_cachedAsset != null)
                {
                    var field = _cachedAsset.GetType().GetField("_configs",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null && field.FieldType.IsGenericType)
                    {
                        _cachedList = field.GetValue(_cachedAsset);
                        var elemType = field.FieldType.GetGenericArguments()[0];
                        _tableView = new TableView();
                        _cachedDrawMethod = typeof(TableView)
                            .GetMethod(nameof(TableView.Draw))
                            .MakeGenericMethod(elemType);

                        ResolveRefresherScript(path);
                        _tableView.OnViewRefresherClicked(OpenRefresherScript);
                    }
                }
            }

            if (_cachedAsset == null || _cachedList == null || _cachedDrawMethod == null)
            {
                GUI.Label(rect, "Failed to read the config list.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _cachedDrawMethod.Invoke(_tableView, new object[] { rect, _cachedList });

            if (GUI.changed)
                EditorUtility.SetDirty(_cachedAsset);
        }

        private void ResolveRefresherScript(string assetPath)
        {
            var managerName = ManagerAddressConvention.ManagerNameOf(_cachedAsset.GetType());
            if (string.IsNullOrEmpty(managerName)) return;
            _cachedRefresherScript = ManagerRefresherLocator.FindRefresherScript(managerName, assetPath);
        }

        private void OpenRefresherScript()
        {
            if (_cachedRefresherScript != null)
                AssetDatabase.OpenAsset(_cachedRefresherScript);
            else
                Debug.LogWarning("[ManagerViewerPage] Refresher script was not found.");
        }
    }
}
