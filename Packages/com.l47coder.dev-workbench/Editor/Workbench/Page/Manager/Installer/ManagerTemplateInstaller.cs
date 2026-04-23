using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // Manager 模板仓（Runtime~/Templates/Managers）——只管"可选模板包"这一件事：
    //   - manifest.json 声明每个内置 Manager 模板的 id / displayName / description / recommended
    //   - 每个 id 对应一个子目录，就是一份模板包源，由 ManagerInstallerPage 驱动用户按需安装
    //
    // Frame 元结构（Game.Managers.asmdef 容器、GameBoot.cs 等）已由 DevWindowFrameworkGuard
    // 在开窗时从 Runtime~/Templates/Game 镜像拷贝，**不再**走这里。因此本类砍掉了：
    //   - IsContainerInstalled / EnsureContainerInstalled（Guard 接管）
    //   - CopyDirectory（改走 AssetFolderCopier.Import）
    //
    // Unity 忽略任何以 `~` 结尾的目录，所以 Runtime~ 内容不会被当作 asset 编译，
    // 模板仓该有的行为。
    internal static class ManagerTemplateInstaller
    {
        private const string TemplateSourceRelative =
            "Packages/com.l47coder.dev-workbench/Runtime~/Templates/Managers";
        private const string ManifestFileName = "manifest.json";
        private const string ManagerRootAssetPath = "Assets/Game/Manager";

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
                Debug.LogWarning($"[ManagerTemplateInstaller] manifest.json not found at {TemplateSourceRelative}/{ManifestFileName}.");
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
                Debug.LogError($"[ManagerTemplateInstaller] Failed to parse manifest.json: {ex.Message}");
                _cachedManifest = new List<PackageInfo>();
            }

            return _cachedManifest;
        }

        public static void InvalidateManifestCache() => _cachedManifest = null;

        // ── 可选模板包 ────────────────────────────────────────────────────────────

        // 以"主脚本文件是否存在"作为"这个模板是否已安装"的判据。
        // 比只看目录存在更稳：用户如果把目录里的文件清空了，我们也认为要重装。
        public static bool IsPackageInstalled(string packageId)
        {
            if (string.IsNullOrEmpty(packageId)) return false;
            var marker = ToAbsolute($"{ManagerRootAssetPath}/{packageId}/{packageId}Manager.cs");
            return File.Exists(marker);
        }

        // 批量安装指定模板包（幂等：已安装的直接跳过）。
        // 返回值：实际新安装了几个包。若 >0 会设置 SessionKeyRerunInitialize，让
        // DevWindowFrameworkGuard 在编译完成后的 domain reload 里重跑 Ensure()——
        // 触发 ManagerViewerPage.OnWorkbenchOpen 把新编译出的 <Name>ManagerConfig
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
                    Debug.LogError($"[ManagerTemplateInstaller] Template folder missing: {TemplateSourceRelative}/{id}");
                    continue;
                }

                // 走通用导入工具：递归保留结构、重名跳过、.meta 忽略、AssetDatabase.Refresh 由它管。
                AssetFolderCopier.Import(sourceAbs, $"{ManagerRootAssetPath}/{id}");
                installed++;
                Debug.Log($"[ManagerTemplateInstaller] Installed Manager template \"{id}\".");
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
