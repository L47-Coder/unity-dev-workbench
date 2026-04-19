using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using DevWorkbench;

namespace DevWorkbench.Editor
{

internal sealed class ComponentViewerPage : IPage
{
    public string GroupTitle => "Component";
    public string TabTitle => "查看";

    private const float SplitterVisualW = 1f;
    private const float LeftPanelMin = 100f;
    private const float LeftPanelMax = 800f;
    private const float LeftPanelStart = 220f;
    private static readonly Color SplitterColor = new(0.11f, 0.11f, 0.11f);

    private readonly ComponentLeftPanel _leftPanel = new();
    private readonly ComponentRightPanel _rightPanel = new();
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

internal sealed class ComponentLeftPanel
{
    private readonly TreeView _treeView = new();

    public void OnStart(Action<string> onSelected)
    {
        _treeView.IgnoredNames = new() { "**/Generated" };
        _treeView.OnNodeSelected(onSelected);
        _treeView.OnAddClicked(() => _treeView.CreateFolderAtSelected());
    }

    public void OnGUI(Rect rect) => _treeView.Draw(rect, "Assets/Game/Component");
}

internal sealed class ComponentRightPanel
{
    private readonly TextView _csTextView = new();
    private string _currentPath;

    private string _cachedCsPath;
    private string _cachedCsText;

    // .asset 文件缓存
    private string _cachedAssetPath;
    private BaseComponentConfig _cachedAsset;
    private object _cachedList;
    private MethodInfo _cachedDrawMethod;
    private TableView _tableView;

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
            _cachedAsset = AssetDatabase.LoadAssetAtPath<BaseComponentConfig>(path);
            _cachedList = null;
            _cachedDrawMethod = null;
            _tableView = null;

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
}
}
