using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace DevWorkbench.Editor
{
    [InitializeOnLoad]
    internal static class DevWindowFrameworkGuard
    {
        public const string SessionKeyRerunInitialize = "DevWorkbench.FrameworkGuard.RerunInitialize";
        private const string GameRootAssetPath = "Assets/Game";
        private const string GameBootAssetPath = GameFramePaths.Root + "/GameBoot.cs";
        private const string LegacyGameBootAssetPath = GameRootAssetPath + "/Manager/GameBoot.cs";
        private const string GameSkeletonSourceRelative = "Packages/com.l47coder.dev-workbench/Runtime~/Templates/Game";
        private const string FrameGroupName = "Frame";
        private static readonly string ManagerOrderAddress = $"{FrameGroupName}/{Path.GetFileNameWithoutExtension(GameFramePaths.ManagerOrder)}";
        private static readonly string ComponentOrderAddress = $"{FrameGroupName}/{Path.GetFileNameWithoutExtension(GameFramePaths.ComponentOrder)}";
        public static event Action EnsureCompleted;

        static DevWindowFrameworkGuard() => EditorApplication.delayCall += TryRerunAfterReload;

        private static void TryRerunAfterReload()
        {
            if (!SessionState.GetBool(SessionKeyRerunInitialize, false)) return;
            SessionState.EraseBool(SessionKeyRerunInitialize);
            Ensure();
        }

        public static void Ensure()
        {
            try
            {
                var skeletonCopied = EnsureGameSkeleton();
                if (skeletonCopied > 0)
                {
                    SessionState.SetBool(SessionKeyRerunInitialize, true);
                    return;
                }

                AssetDatabase.StartAssetEditing();
                try
                {
                    EnsureAddressablesInitialized();
                    EnsureOrderAsset<ManagerOrderConfig>(GameFramePaths.ManagerOrder, ManagerOrderAddress);
                    EnsureOrderAsset<ComponentOrderConfig>(GameFramePaths.ComponentOrder, ComponentOrderAddress);
                    EnsurePageOrderAsset();
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                    AssetDatabase.SaveAssets();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DevWindowFrameworkGuard] Ensure failed: {ex}");
                EditorUtility.DisplayDialog("Dev Workbench", $"Framework auto-initialise failed:\n{ex.Message}\n\nSee Console for details.", "OK");
            }

            RunAllPageContributions();

            try { EnsureCompleted?.Invoke(); }
            catch (Exception ex)
            {
                Debug.LogError($"[DevWindowFrameworkGuard] EnsureCompleted subscriber threw: {ex}");
            }
        }

        private static int EnsureGameSkeleton()
        {
            var migrated = TryMigrateLegacyGameBoot() ? 1 : 0;

            int copied;
            try
            {
                copied = AssetFolderCopier.Import(GameSkeletonSourceRelative, GameRootAssetPath);
            }
            catch (FileNotFoundException)
            {
                Debug.LogError($"[DevWindowFrameworkGuard] Game skeleton template missing: {GameSkeletonSourceRelative}.");
                copied = 0;
            }

            return migrated + copied;
        }

        private static bool TryMigrateLegacyGameBoot()
        {
            if (File.Exists(AssetPathUtil.ToAbsolute(GameBootAssetPath))) return false;
            if (!File.Exists(AssetPathUtil.ToAbsolute(LegacyGameBootAssetPath))) return false;

            AssetPathUtil.EnsureFolder(GameFramePaths.Root);

            var error = AssetDatabase.MoveAsset(LegacyGameBootAssetPath, GameBootAssetPath);
            if (string.IsNullOrEmpty(error))
            {
                Debug.Log($"[DevWindowFrameworkGuard] Migrated legacy GameBoot.cs from {LegacyGameBootAssetPath} to {GameBootAssetPath}.");
                return true;
            }

            Debug.LogWarning($"[DevWindowFrameworkGuard] MoveAsset failed ({error}); falling back to template import. Please remove {LegacyGameBootAssetPath} manually if it still exists.");
            return false;
        }

        private static void EnsureAddressablesInitialized()
        {
            if (AddressableAssetSettingsDefaultObject.Settings != null) return;

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                Debug.LogError("[DevWindowFrameworkGuard] Failed to create AddressableAssetSettings.");
                return;
            }

            AssetDatabase.SaveAssets();
        }

        private static void EnsurePageOrderAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<PageOrder>(GameFramePaths.PageOrder) != null) return;

            AssetPathUtil.EnsureFolder(GameFramePaths.Root);
            var asset = ScriptableObject.CreateInstance<PageOrder>();
            AssetDatabase.CreateAsset(asset, GameFramePaths.PageOrder);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureOrderAsset<T>(string assetPath, string address) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                AssetPathUtil.EnsureFolder(GameFramePaths.Root);
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.SaveAssets();
            }

            EnsureAddressableEntry(assetPath, address);
        }

        private static void EnsureAddressableEntry(string assetPath, string address)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning($"[DevWindowFrameworkGuard] AddressableAssetSettings not found; skipping registration of {assetPath}.");
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid)) return;

            var group = settings.groups.FirstOrDefault(g => g != null && g.Name == FrameGroupName)
                        ?? settings.CreateGroup(FrameGroupName, false, false, true, null);

            var existing = settings.FindAssetEntry(guid);
            if (existing != null && existing.parentGroup == group && existing.address == address)
                return;

            var entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = address;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
            AssetDatabase.SaveAssets();
        }

        private static void RunAllPageContributions()
        {
            foreach (var type in TypeCache.GetTypesDerivedFrom<IPage>())
            {
                if (type.IsAbstract || type.IsInterface) continue;

                IPage page;
                try { page = Activator.CreateInstance(type) as IPage; }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DevWindowFrameworkGuard] Failed to instantiate {type.FullName}: {ex.Message}");
                    continue;
                }

                if (page == null) continue;

                try { page.OnWorkbenchOpen(); }
                catch (Exception ex)
                {
                    Debug.LogError($"[DevWindowFrameworkGuard] {type.Name}.OnWorkbenchOpen threw: {ex}");
                }
            }
        }
    }
}
