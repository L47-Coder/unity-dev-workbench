using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // Component 模板仓（Runtime~/Templates/Components）——与 ManagerTemplateInstaller
    // 对称。仅负责可选模板包；Frame 元结构由 DevWindowFrameworkGuard 独立管理。
    internal static class ComponentTemplateInstaller
    {
        private const string TemplateSourceRelative =
            "Packages/com.l47coder.dev-workbench/Runtime~/Templates/Components";
        private const string ManifestFileName = "manifest.json";
        private const string ComponentRootAssetPath = "Assets/Game/Component";

        [Serializable]
        public sealed class PackageInfo
        {
            public string id;
            public string displayName;
            public string description;
            public bool recommended;
        }

        [Serializable]
        private sealed class Manifest
        {
            public List<PackageInfo> packages;
        }

        private static List<PackageInfo> _cachedManifest;

        public static IReadOnlyList<PackageInfo> LoadManifest()
        {
            if (_cachedManifest != null) return _cachedManifest;

            var manifestAbs = ResolveSourceAbsolute(ManifestFileName);
            if (string.IsNullOrEmpty(manifestAbs) || !File.Exists(manifestAbs))
            {
                // manifest 文件缺失 → 视为暂时没有内置 Component 模板。
                _cachedManifest = new List<PackageInfo>();
                return _cachedManifest;
            }

            try
            {
                var json = File.ReadAllText(manifestAbs);
                var parsed = JsonUtility.FromJson<Manifest>(json);
                _cachedManifest = parsed?.packages ?? new List<PackageInfo>();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ComponentTemplateInstaller] Failed to parse manifest.json: {ex.Message}");
                _cachedManifest = new List<PackageInfo>();
            }

            return _cachedManifest;
        }

        public static void InvalidateManifestCache() => _cachedManifest = null;

        // 判据：{packageId}Component.cs 存在。与 Creator 的 {Name}Component.cs 命名对齐。
        public static bool IsPackageInstalled(string packageId)
        {
            if (string.IsNullOrEmpty(packageId)) return false;
            var marker = AssetPathUtil.ToAbsolute($"{ComponentRootAssetPath}/{packageId}/{packageId}Component.cs");
            return File.Exists(marker);
        }

        // 幂等批量安装。返回新装数量；>0 则写 SessionKeyRerunInitialize，让 reload 后的
        // Guard.Ensure 再跑一轮 IPage.OnWorkbenchOpen 把新 Config 补齐 asset/Addressables/Order。
        public static int InstallPackages(IEnumerable<string> packageIds)
        {
            if (packageIds == null) return 0;

            var installed = 0;
            foreach (var id in packageIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (IsPackageInstalled(id)) continue;

                var sourceAbs = ResolveSourceAbsolute(id);
                if (string.IsNullOrEmpty(sourceAbs) || !Directory.Exists(sourceAbs))
                {
                    Debug.LogError($"[ComponentTemplateInstaller] Template folder missing: {TemplateSourceRelative}/{id}");
                    continue;
                }

                AssetFolderCopier.Import(sourceAbs, $"{ComponentRootAssetPath}/{id}");
                installed++;
                Debug.Log($"[ComponentTemplateInstaller] Installed Component template \"{id}\".");
            }

            if (installed > 0)
                SessionState.SetBool(DevWindowFrameworkGuard.SessionKeyRerunInitialize, true);

            return installed;
        }

        private static string ResolveSourceAbsolute(string relativeInsideTemplate = null)
        {
            try
            {
                var root = Path.GetFullPath(TemplateSourceRelative);
                return string.IsNullOrEmpty(relativeInsideTemplate)
                    ? root
                    : Path.Combine(root, relativeInsideTemplate);
            }
            catch { return null; }
        }
    }
}
