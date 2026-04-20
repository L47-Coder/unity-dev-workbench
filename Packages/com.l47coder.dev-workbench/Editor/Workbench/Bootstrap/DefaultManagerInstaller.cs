using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // 默认 Manager 模板仓（Runtime~/DefaultManagers）里分两层东西：
    //   1. Game.Managers.asmdef —— "Manager 容器"程序集，与具体某个默认包无关。
    //      Creator 生成的用户 Manager 也靠它接管，因此无论用户是否安装任一默认包，
    //      只要想在 Assets/Game/Manager/ 下写 Manager，就需要它先就位。
    //      => 由 FrameworkBootstrapper 的 Step 0（EnsureContainerInstalled）负责。
    //   2. Asset / Component / Prefab 子目录 —— 每个子目录就是一个"可选默认包"，
    //      manifest.json 描述它们的元信息；由 ManagerInstallerPage 驱动用户按需安装。
    //
    // 注意：Unity 会忽略任何以 `~` 结尾的目录，所以包里 Runtime~ 的内容不会被当作 asset 编译，
    // 这正是"模板仓"该有的行为。
    internal static class DefaultManagerInstaller
    {
        private const string PackageSourceRelative =
            "Packages/com.l47coder.dev-workbench/Runtime~/DefaultManagers";
        private const string ManifestFileName = "manifest.json";
        private const string AsmdefFileName = "Game.Managers.asmdef";

        public const string ManagerRootAssetPath = "Assets/Game/Manager";
        public const string AsmdefAssetPath = ManagerRootAssetPath + "/" + AsmdefFileName;

        // Installer 拷完用户选中的包后，Unity 会重新编译；编译完 domain reload
        // 会触发 FrameworkBootstrapper.TryRerunInitializeAfterReload 再跑一次 InitializeAll，
        // 把新包里的 ManagerConfig 类型挂到 Addressables、同步 Order、跑 Refresher。
        internal const string SessionKeyRerunInitialize = "DevWorkbench.Bootstrapper.RerunInitialize";

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
                Debug.LogWarning($"[DefaultManagerInstaller] manifest.json not found at {PackageSourceRelative}/{ManifestFileName}.");
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
                Debug.LogError($"[DefaultManagerInstaller] Failed to parse manifest.json: {ex.Message}");
                _cachedManifest = new List<PackageInfo>();
            }

            return _cachedManifest;
        }

        public static void InvalidateManifestCache() => _cachedManifest = null;

        // ── 容器 asmdef ───────────────────────────────────────────────────────────

        public static bool IsContainerInstalled() => File.Exists(ToAbsolute(AsmdefAssetPath));

        // 只投放 Game.Managers.asmdef（= Manager 容器程序集）。不拷任何默认包。
        // 供 FrameworkBootstrapper Step 0 使用。
        public static bool EnsureContainerInstalled()
        {
            if (IsContainerInstalled()) return false;

            var sourceAbs = ResolveSourceAbsolute(AsmdefFileName);
            if (string.IsNullOrEmpty(sourceAbs) || !File.Exists(sourceAbs))
            {
                Debug.LogError($"[DefaultManagerInstaller] Container asmdef missing in template: {PackageSourceRelative}/{AsmdefFileName}.");
                return false;
            }

            FrameAssetInstaller.EnsureFolder(ManagerRootAssetPath);

            var targetAbs = ToAbsolute(AsmdefAssetPath);
            var targetDir = Path.GetDirectoryName(targetAbs);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            File.Copy(sourceAbs, targetAbs, overwrite: false);
            AssetDatabase.Refresh();
            Debug.Log("[DefaultManagerInstaller] Game.Managers.asmdef container deployed.");
            return true;
        }

        // ── 可选默认包 ────────────────────────────────────────────────────────────

        // 以"主脚本文件是否存在"作为"这个包是否已安装"的判据。
        // 比只看目录存在更稳：用户如果把目录里的文件清空了，我们也认为要重装。
        public static bool IsPackageInstalled(string packageId)
        {
            if (string.IsNullOrEmpty(packageId)) return false;
            var marker = ToAbsolute($"{ManagerRootAssetPath}/{packageId}/{packageId}Manager.cs");
            return File.Exists(marker);
        }

        // 批量安装指定默认包（幂等：已安装的直接跳过）。
        // 返回值：实际新安装了几个包。若 >0 会设置 SessionKeyRerunInitialize，
        // 让 FrameworkBootstrapper 在编译完成后重跑 InitializeAll 以挂载 Addressables/Order/Refresher。
        public static int InstallPackages(IEnumerable<string> packageIds)
        {
            if (packageIds == null) return 0;

            // 先确保容器 asmdef 存在（Installer 作为入口被单独触发时的兜底，
            // 一般来说到得了 Installer 页 Bootstrap 就已经跑过了，但多一份保险不亏）。
            EnsureContainerInstalled();

            var installed = 0;
            foreach (var id in packageIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (IsPackageInstalled(id)) continue;

                var sourceAbs = ResolveSourceAbsolute(id);
                if (string.IsNullOrEmpty(sourceAbs) || !Directory.Exists(sourceAbs))
                {
                    Debug.LogError($"[DefaultManagerInstaller] Template folder missing: {PackageSourceRelative}/{id}");
                    continue;
                }

                var targetAbs = ToAbsolute($"{ManagerRootAssetPath}/{id}");
                CopyDirectory(sourceAbs, targetAbs);
                installed++;
                Debug.Log($"[DefaultManagerInstaller] Installed default package \"{id}\".");
            }

            if (installed > 0)
            {
                SessionState.SetBool(SessionKeyRerunInitialize, true);
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
                var root = Path.GetFullPath(PackageSourceRelative);
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
