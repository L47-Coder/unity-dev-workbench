using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // 通用 asset 导入工具：把源文件或源文件夹拷贝到 Assets 下的目标文件夹。
    //
    // 契约：
    //   - sourcePath：单个文件或文件夹。支持绝对路径，也支持 "Assets/..."、
    //     "Packages/...(~)" 等能被 Path.GetFullPath 解析为绝对路径的项目相对路径。
    //   - targetFolderAssetPath：必须是 "Assets" 或 "Assets/..." 形式的文件夹路径
    //     （工具内部走 AssetDatabase 才能让 Unity 正确生成 .meta）。
    //       * 不存在时沿路 AssetDatabase.CreateFolder；
    //       * 指向已存在的"文件"直接抛 IOException——契约要求目标必须是文件夹。
    //   - source 为文件夹时递归保留目录结构；recursive=false 则只拷直接子文件。
    //   - 重名文件直接跳过（不覆盖），Debug.Log 提示便于排查。
    //   - .meta 文件一律忽略：Unity 会在 Refresh 时为新文件重新生成 GUID，避免与源
    //     目录 GUID 冲突。未来需要保留 GUID 的场景再加参数扩展。
    //
    // 返回值：实际新写入的文件数。>0 时末尾触发一次 AssetDatabase.Refresh。
    internal static class AssetFolderCopier
    {
        public static int Import(string sourcePath, string targetFolderAssetPath, bool recursive = true)
        {
            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentException("sourcePath is null or empty.", nameof(sourcePath));
            if (string.IsNullOrEmpty(targetFolderAssetPath))
                throw new ArgumentException("targetFolderAssetPath is null or empty.", nameof(targetFolderAssetPath));

            var normalizedTarget = targetFolderAssetPath.Replace('\\', '/').TrimEnd('/');
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

            var targetAbs = ToAbsolute(normalizedTarget);
            if (File.Exists(targetAbs))
                throw new IOException(
                    $"targetFolderAssetPath points to an existing file, folder required: {normalizedTarget}");

            EnsureFolder(normalizedTarget);

            int copied = isSourceFile
                ? (CopyOne(sourceAbs, targetAbs) ? 1 : 0)
                : CopyDirectory(sourceAbs, targetAbs, recursive);

            if (copied > 0)
                AssetDatabase.Refresh();

            return copied;
        }

        // 递归拷贝整棵目录树；返回实际写入文件数。
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

        // 单文件落盘：.meta 忽略、目标目录缺就建、重名跳过。
        private static bool CopyOne(string fileAbs, string targetDirAbs)
        {
            if (IsMeta(fileAbs)) return false;

            if (!Directory.Exists(targetDirAbs))
                Directory.CreateDirectory(targetDirAbs);

            var name = Path.GetFileName(fileAbs);
            var destAbs = Path.Combine(targetDirAbs, name);
            if (File.Exists(destAbs))
            {
                Debug.Log($"[AssetFolderCopier] Skip (exists): {destAbs}");
                return false;
            }

            File.Copy(fileAbs, destAbs, overwrite: false);
            return true;
        }

        private static bool IsMeta(string path) =>
            Path.GetExtension(path).Equals(".meta", StringComparison.OrdinalIgnoreCase);

        // 走 AssetDatabase.CreateFolder 逐级建（同时生成 .meta，Unity 能立刻识别）。
        private static void EnsureFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
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

        private static string ToAbsolute(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, assetPath).Replace('\\', '/');
        }
    }
}
