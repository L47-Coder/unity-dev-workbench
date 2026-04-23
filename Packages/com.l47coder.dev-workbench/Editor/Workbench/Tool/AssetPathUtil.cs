using System.IO;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // Asset 路径小工具：斜杠归一、Assets → 项目绝对路径、逐级 AssetDatabase.CreateFolder。
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

        // 走 AssetDatabase.CreateFolder 逐级建（同时生成 .meta，Unity 立刻识别）。
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
