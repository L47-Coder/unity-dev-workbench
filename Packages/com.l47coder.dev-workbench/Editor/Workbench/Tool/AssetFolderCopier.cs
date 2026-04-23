using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // 把源文件或源目录拷到 Assets 下的目标目录；重名跳过、.meta 忽略、必要时 Refresh。
    //   - sourcePath：绝对路径或能被 Path.GetFullPath 解析的项目相对路径（Packages/..、Runtime~/.. 皆可）
    //   - targetFolderAssetPath：必须是 "Assets" 或 "Assets/..." 形式的文件夹路径
    //   - 返回值：实际新落盘的文件数；>0 时末尾触发一次 AssetDatabase.Refresh
    internal static class AssetFolderCopier
    {
        public static int Import(string sourcePath, string targetFolderAssetPath, bool recursive = true)
        {
            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentException("sourcePath is null or empty.", nameof(sourcePath));
            if (string.IsNullOrEmpty(targetFolderAssetPath))
                throw new ArgumentException("targetFolderAssetPath is null or empty.", nameof(targetFolderAssetPath));

            var normalizedTarget = AssetPathUtil.Normalize(targetFolderAssetPath);
            if (normalizedTarget != "Assets" && !normalizedTarget.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException(
                    $"targetFolderAssetPath must start with \"Assets\": {targetFolderAssetPath}",
                    nameof(targetFolderAssetPath));

            string sourceAbs;
            try { sourceAbs = Path.GetFullPath(sourcePath); }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Cannot resolve sourcePath \"{sourcePath}\": {ex.Message}",
                    nameof(sourcePath), ex);
            }

            var isSourceDir = Directory.Exists(sourceAbs);
            var isSourceFile = File.Exists(sourceAbs);
            if (!isSourceDir && !isSourceFile)
                throw new FileNotFoundException($"Source path does not exist: {sourceAbs}");

            var targetAbs = AssetPathUtil.ToAbsolute(normalizedTarget);
            if (File.Exists(targetAbs))
                throw new IOException(
                    $"targetFolderAssetPath points to an existing file, folder required: {normalizedTarget}");

            AssetPathUtil.EnsureFolder(normalizedTarget);

            var copied = isSourceFile
                ? (CopyOne(sourceAbs, targetAbs) ? 1 : 0)
                : CopyDirectory(sourceAbs, targetAbs, recursive);

            if (copied > 0)
                AssetDatabase.Refresh();

            return copied;
        }

        private static int CopyDirectory(string sourceDirAbs, string targetDirAbs, bool recursive)
        {
            if (!Directory.Exists(targetDirAbs))
                Directory.CreateDirectory(targetDirAbs);

            var count = 0;
            foreach (var file in Directory.GetFiles(sourceDirAbs))
            {
                if (IsMeta(file)) continue;
                if (CopyOne(file, targetDirAbs)) count++;
            }

            if (!recursive) return count;

            foreach (var sub in Directory.GetDirectories(sourceDirAbs))
            {
                var name = Path.GetFileName(sub);
                count += CopyDirectory(sub, Path.Combine(targetDirAbs, name), true);
            }

            return count;
        }

        private static bool CopyOne(string fileAbs, string targetDirAbs)
        {
            if (IsMeta(fileAbs)) return false;

            if (!Directory.Exists(targetDirAbs))
                Directory.CreateDirectory(targetDirAbs);

            var name = Path.GetFileName(fileAbs);
            var destAbs = Path.Combine(targetDirAbs, name);
            if (File.Exists(destAbs)) return false;

            File.Copy(fileAbs, destAbs, overwrite: false);
            return true;
        }

        private static bool IsMeta(string path) =>
            Path.GetExtension(path).Equals(".meta", StringComparison.OrdinalIgnoreCase);
    }
}
