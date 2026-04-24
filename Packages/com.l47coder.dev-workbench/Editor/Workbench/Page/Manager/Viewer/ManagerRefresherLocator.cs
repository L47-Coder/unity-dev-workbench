using System.IO;
using UnityEditor;

namespace DevWorkbench.Editor
{
    // 给 ManagerViewerPage 的 "Open Refresher script" 按钮定位源文件。
    // 约定：Refresher .cs 住在 <managerFolder>/Editor/<Name>ManagerRefresher.cs
    //       （由 ManagerCreationService 生成、ManagerTemplateInstaller 铺装，路径固定）。
    //
    // 历史兼容：早期版本把 Refresher 和 Manager 同目录，这里作为回落继续支持。
    internal static class ManagerRefresherLocator
    {
        private const string RefresherSuffix = "ManagerRefresher";

        public static MonoScript FindRefresherScript(string managerName, string assetPath)
        {
            if (string.IsNullOrEmpty(managerName) || string.IsNullOrEmpty(assetPath)) return null;

            var folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder)) return null;

            var fileName = $"{managerName}{RefresherSuffix}.cs";

            var editorPath = $"{folder}/Editor/{fileName}";
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(editorPath);
            if (script != null) return script;

            var legacyPath = $"{folder}/{fileName}";
            return AssetDatabase.LoadAssetAtPath<MonoScript>(legacyPath);
        }
    }
}
