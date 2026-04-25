using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    internal static class ComponentTemplateInstaller
    {
        private const string TemplateSourceRelative = DevWorkbenchPackageInfo.ComponentTemplatesFolder;
        private const string ManifestFileName = "manifest.json";
        private const string ComponentRootAssetPath = GameProjectPaths.ComponentRoot;

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
                Debug.LogWarning($"[ComponentTemplateInstaller] manifest.json not found at {TemplateSourceRelative}/{ManifestFileName}.");
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

        public static bool IsPackageInstalled(string packageId)
        {
            if (string.IsNullOrEmpty(packageId)) return false;
            var marker = AssetPathUtil.ToAbsolute($"{ComponentRootAssetPath}/{packageId}/{packageId}Component.cs");
            return File.Exists(marker);
        }

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
