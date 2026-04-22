using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // Component 模板仓（Runtime~/Templates/Components）与 Manager 模板仓结构对称：
    //   1. Game.Components.asmdef —— "Component 容器"程序集，与具体某个模板包无关。
    //      Creator 生成的用户 Component 都落在 Assets/Game/Component/ 下，需要这个
    //      asmdef 先就位，以免 Component 被吸进 Assembly-CSharp 和业务代码一起重编译。
    //      => 由 FrameworkBootstrapper 的 Step 0（EnsureContainerInstalled）负责。
    //   2. manifest.json 记录的内置 Component 模板 —— 每个 id 对应一个可选模板包；
    //      由（未来的）ComponentInstallerPage 驱动用户按需安装。
    //
    // API 形状刻意与 ManagerTemplateInstaller 对齐（LoadManifest / IsPackageInstalled /
    // InstallPackages / EnsureContainerInstalled），待第三种模板出现时再考虑抽共用基座。
    internal static class ComponentTemplateInstaller
    {
        private const string TemplateSourceRelative =
            "Packages/com.l47coder.dev-workbench/Runtime~/Templates/Components";
        private const string ManifestFileName = "manifest.json";
        private const string AsmdefFileName = "Game.Components.asmdef";

        public const string ComponentRootAssetPath = "Assets/Game/Component";
        public const string AsmdefAssetPath = ComponentRootAssetPath + "/" + AsmdefFileName;

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

        // ── 容器 asmdef ───────────────────────────────────────────────────────────

        public static bool IsContainerInstalled() => File.Exists(ToAbsolute(AsmdefAssetPath));

        // 只投放 Game.Components.asmdef（= Component 容器程序集）。不拷任何模板包。
        // 供 FrameworkBootstrapper Step 0 使用。
        public static bool EnsureContainerInstalled()
        {
            if (IsContainerInstalled()) return false;

            var sourceAbs = ResolveSourceAbsolute(AsmdefFileName);
            if (string.IsNullOrEmpty(sourceAbs) || !File.Exists(sourceAbs))
            {
                Debug.LogError($"[ComponentTemplateInstaller] Container asmdef missing in template: {TemplateSourceRelative}/{AsmdefFileName}.");
                return false;
            }

            FrameAssetInstaller.EnsureFolder(ComponentRootAssetPath);

            var targetAbs = ToAbsolute(AsmdefAssetPath);
            var targetDir = Path.GetDirectoryName(targetAbs);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            File.Copy(sourceAbs, targetAbs, overwrite: false);
            AssetDatabase.Refresh();
            Debug.Log("[ComponentTemplateInstaller] Game.Components.asmdef container deployed.");
            return true;
        }

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
        public static int InstallPackages(IEnumerable<string> packageIds)
        {
            if (packageIds == null) return 0;

            EnsureContainerInstalled();

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

                var targetAbs = ToAbsolute($"{ComponentRootAssetPath}/{id}");
                CopyDirectory(sourceAbs, targetAbs);
                installed++;
                Debug.Log($"[ComponentTemplateInstaller] Installed Component template \"{id}\".");
            }

            if (installed > 0)
            {
                // 让 FrameworkBootstrapper 在 domain reload 之后重跑 RunFullEnsure——
                // Component 侧的 ComponentViewerPage.OnWorkbenchOpen 负责把新编译出的
                // <Name>ComponentConfig 类型创建成 asset、挂 Addressables、同步 Order。
                SessionState.SetBool(FrameworkBootstrapper.SessionKeyRerunInitialize, true);
                AssetDatabase.Refresh();
            }

            return installed;
        }

        // ── 工具 ──────────────────────────────────────────────────────────────────

        private static void CopyDirectory(string sourceAbs, string targetAbs)
        {
            if (!Directory.Exists(targetAbs))
                Directory.CreateDirectory(targetAbs);

            foreach (var file in Directory.GetFiles(sourceAbs))
            {
                var name = Path.GetFileName(file);
                var dest = Path.Combine(targetAbs, name);
                File.Copy(file, dest, overwrite: false);
            }

            foreach (var sub in Directory.GetDirectories(sourceAbs))
            {
                var name = Path.GetFileName(sub);
                CopyDirectory(sub, Path.Combine(targetAbs, name));
            }
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

        private static string ToAbsolute(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, assetPath).Replace('\\', '/');
        }
    }
}
