using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // Manager 模板仓（Runtime~/Templates/Managers）——可选模板包入口。
    // manifest.json 声明每个 id 的 displayName / description / recommended；
    // 每个 id 对应一份模板包源，由 ManagerInstallerPage 驱动用户按需安装。
    // Frame 元结构由 DevWindowFrameworkGuard 独立管理，不经此类。
    internal static class ManagerTemplateInstaller
    {
        private const string TemplateSourceRelative =
            "Packages/com.l47coder.dev-workbench/Runtime~/Templates/Managers";
        private const string ManifestFileName = "manifest.json";
        private const string ManagerRootAssetPath = "Assets/Game/Manager";
        private const string ManagerEditorAssetPath = "Assets/Game/Manager/Editor";

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

        // 以"主脚本存在"判定"已安装"——比只看目录更稳（用户清空文件后应视为需重装）。
        public static bool IsPackageInstalled(string packageId)
        {
            if (string.IsNullOrEmpty(packageId)) return false;
            var marker = AssetPathUtil.ToAbsolute($"{ManagerRootAssetPath}/{packageId}/{packageId}Manager.cs");
            return File.Exists(marker);
        }

        // 幂等批量安装。返回新装数量；>0 则写 SessionKeyRerunInitialize，让 reload 后
        // 的 Guard.Ensure 再跑一轮 IPage.OnWorkbenchOpen 把新 Config 补齐 asset/Addressables/Order。
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

                AssetFolderCopier.Import(sourceAbs, $"{ManagerRootAssetPath}/{id}");

                var editorSourceAbs = ResolveSourceAbsolute($"Editor/{id}");
                if (!string.IsNullOrEmpty(editorSourceAbs) && Directory.Exists(editorSourceAbs))
                    AssetFolderCopier.Import(editorSourceAbs, $"{ManagerEditorAssetPath}/{id}");

                installed++;
                Debug.Log($"[ManagerTemplateInstaller] Installed Manager template \"{id}\".");
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
