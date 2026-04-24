using System.IO;
using UnityEditor;

namespace DevWorkbench.Editor
{
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
