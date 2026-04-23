using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // Component 模板仓（Runtime~/Templates/Components）——形态与 ManagerTemplateInstaller
    // 对称，只管"可选模板包"这一件事：manifest.json + 各 id 子目录。
    //
    // Frame 元结构（Game.Components.asmdef 容器等）已由 DevWindowFrameworkGuard
    // 在开窗时从 Runtime~/Templates/Game 镜像拷贝，**不再**走这里。
    //
    // API 形状刻意与 ManagerTemplateInstaller 对齐（LoadManifest / IsPackageInstalled /
    // InstallPackages），待第三种模板出现时再考虑抽共用基座。
    internal static class ComponentTemplateInstaller
    {
        private const string TemplateSourceRelative =
            "Packages/com.l47coder.dev-workbench/Runtime~/Templates/Components";
        private const string ManifestFileName = "manifest.json";
        private const string ComponentRootAssetPath = "Assets/Game/Component";

        // ── Manifest ──────────────────────────────────────────────────────────────

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
                // 允许 manifest 文件缺失——视为"暂时没有内置 Component 模板"。
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

        // ── 可选模板包 ────────────────────────────────────────────────────────────

        // 判据：{packageId}Component.cs 是否存在——和 Creator 生成的命名规则
        // ({Name}Component.cs) 对齐，便于未来内置模板判断"是否已被用户侧安装过"。
        public static bool IsPackageInstalled(string packageId)
        {
            if (string.IsNullOrEmpty(packageId)) return false;
            var marker = ToAbsolute($"{ComponentRootAssetPath}/{packageId}/{packageId}Component.cs");
            return File.Exists(marker);
        }

        // 批量安装指定模板包（幂等：已安装的直接跳过）。
        // 返回值：实际新安装了几个包。若 >0 会设置 SessionKeyRerunInitialize，让
        // DevWindowFrameworkGuard 在编译完成后的 domain reload 里重跑 Ensure()——
        // 触发 ComponentViewerPage.OnWorkbenchOpen 把新编译出的 <Name>ComponentConfig
        // 创建成 asset、挂 Addressables、同步 Order。
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

                // 走通用导入工具：递归保留结构、重名跳过、.meta 忽略、AssetDatabase.Refresh 由它管。
                AssetFolderCopier.Import(sourceAbs, $"{ComponentRootAssetPath}/{id}");
                installed++;
                Debug.Log($"[ComponentTemplateInstaller] Installed Component template \"{id}\".");
            }

            if (installed > 0)
                SessionState.SetBool(DevWindowFrameworkGuard.SessionKeyRerunInitialize, true);

            return installed;
        }

        // ── 工具 ──────────────────────────────────────────────────────────────────

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

        private static string ToAbsolute(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, assetPath).Replace('\\', '/');
        }
    }
}
