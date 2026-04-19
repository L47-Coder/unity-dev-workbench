using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using DevWorkbench;

namespace DevWorkbench.Editor
{

internal sealed class ManagerViewerPage : IPage
{
    public string GroupTitle => "Manager";
    public string TabTitle => "查看";

    private const float SplitterVisualW = 1f;
    private const float LeftPanelMin = 100f;
    private const float LeftPanelMax = 800f;
    private const float LeftPanelStart = 220f;
    private static readonly Color SplitterColor = new(0.11f, 0.11f, 0.11f);

    private readonly ManagerLeftPanel _leftPanel = new();
    private readonly ManagerRightPanel _rightPanel = new();
    private float _splitterX = LeftPanelStart;
    private bool _dragging;

    public void OnStart() => _leftPanel.OnStart(_rightPanel.SetPath);

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

    public void OnStart(Action<string> onSelected)
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

    // .asset 文件缓存（管理器配置为 BaseManagerConfig，与 BaseComponentConfig 无继承关系）
    private string _cachedAssetPath;
    private BaseManagerConfig _cachedAsset;
    private object _cachedList;
    private FieldInfo _cachedListField;
    private MethodInfo _cachedDrawMethod;
    private TableView _tableView;

    private MonoScript _cachedRefresherScript;
    private IManagerRefresher _cachedRefresher;

    public void SetPath(string path) => _currentPath = path;

    public void OnGUI(Rect rect)
    {
        if (string.IsNullOrEmpty(_currentPath))
        {
            GUI.Label(rect, "未选中任何项", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        if (Directory.Exists(_currentPath))
        {
            DrawFolder(rect, _currentPath);
            return;
        }

        switch (Path.GetExtension(_currentPath).ToLowerInvariant())
        {
            case ".cs":    DrawCsFile(rect, _currentPath);    break;
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
            _cachedCsText = File.Exists(path) ? File.ReadAllText(path) : "(文件读取失败)";
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
            _cachedListField = null;
            _cachedDrawMethod = null;
            _tableView = null;
            _cachedRefresherScript = null;
            _cachedRefresher = null;

            if (_cachedAsset != null)
            {
                var field = _cachedAsset.GetType().GetField("_configs",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && field.FieldType.IsGenericType)
                {
                    _cachedListField = field;
                    _cachedList = field.GetValue(_cachedAsset);
                    var elemType = field.FieldType.GetGenericArguments()[0];
                    _tableView = new TableView();
                    _cachedDrawMethod = typeof(TableView)
                        .GetMethod(nameof(TableView.Draw))
                        .MakeGenericMethod(elemType);

                    ResolveRefresher(path);

                    _tableView.OnRefreshClicked(ExecuteRefresh);
                    _tableView.OnViewRefresherClicked(OpenRefresherScript);
                }
            }
        }

        if (_cachedAsset == null || _cachedList == null || _cachedDrawMethod == null)
        {
            GUI.Label(rect, "无法读取配置列表", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        _cachedDrawMethod.Invoke(_tableView, new object[] { rect, _cachedList });

        if (GUI.changed)
            EditorUtility.SetDirty(_cachedAsset);
    }

    private void ResolveRefresher(string assetPath)
    {
        var configTypeName = _cachedAsset.GetType().Name;
        if (!configTypeName.EndsWith("ManagerConfig")) return;

        var managerName = configTypeName[..^"ManagerConfig".Length];

        _cachedRefresherScript = ManagerRefresherLocator.FindRefresherScript(managerName, assetPath);
        var refresherType = ManagerRefresherLocator.FindRefresherType(managerName, assetPath);
        if (refresherType != null)
            _cachedRefresher = (IManagerRefresher)System.Activator.CreateInstance(refresherType);
    }

    private void ExecuteRefresh()
    {
        if (_cachedRefresher == null || _cachedAsset == null)
        {
            Debug.LogWarning("[ManagerViewerPage] 未找到刷新脚本或配置资产。");
            return;
        }

        _cachedRefresher.Refresh(_cachedAsset);

        if (_cachedListField != null)
            _cachedList = _cachedListField.GetValue(_cachedAsset);
    }

    private void OpenRefresherScript()
    {
        if (_cachedRefresherScript != null)
            AssetDatabase.OpenAsset(_cachedRefresherScript);
        else
            Debug.LogWarning("[ManagerViewerPage] 未找到刷新脚本。");
    }
}
}
