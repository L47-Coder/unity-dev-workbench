using System.IO;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    internal static class AssetPathUtil
    {
        public static string Normalize(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return assetPath;
            return assetPath.Replace('\\', '/').TrimEnd('/');
        }

        public static string ToAbsolute(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, assetPath).Replace('\\', '/');
        }

        public static void EnsureFolder(string assetPath)
        {
            assetPath = Normalize(assetPath);
            if (string.IsNullOrEmpty(assetPath)) return;
            if (assetPath == "Assets" || AssetDatabase.IsValidFolder(assetPath)) return;

            var parts = assetPath.Split('/');
            for (var i = 1; i < parts.Length; i++)
            {
                var parent = string.Join("/", parts, 0, i);
                var child = $"{parent}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(child))
                    AssetDatabase.CreateFolder(parent, parts[i]);
            }
        }
    }
}
