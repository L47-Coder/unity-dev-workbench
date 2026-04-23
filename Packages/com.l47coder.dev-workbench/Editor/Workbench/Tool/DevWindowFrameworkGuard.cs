using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // DevWorkbench 架构完整性兜底 + domain-reload rerun 触发器。
    //
    // 三条触发路径统一收敛到 Ensure()：DevWindow.Open 手动开窗、Framework/Sync 页的
    // Sync Runtime 按钮（FrameworkSyncPage）、以及写入 SessionKeyRerunInitialize 后
    // 的 domain reload（静态构造 delayCall 兜底）。
    //
    // Ensure() 两段时序：
    //   首次 bootstrap（skeleton 拷贝数 > 0）：拷模板 → 写 flag → return；reload 后再跑。
    //   已初始化（拷贝数 == 0）：Addressables settings → 三份 Order SO → 反射跑
    //     IPage.OnWorkbenchOpen → Invoke EnsureCompleted 通知 DevWindow 重建 PageTree。
    //
    // 首次 bootstrap 的短路 return 刻意不 Invoke EnsureCompleted——那时 PageOrder 尚未建好。
    [InitializeOnLoad]
    internal static class DevWindowFrameworkGuard
    {
        // TemplateInstaller / 首次模板拷贝完写此 flag；domain reload 后静态构造重跑 Ensure。
        public const string SessionKeyRerunInitialize = "DevWorkbench.FrameworkGuard.RerunInitialize";

        // 架构 + 业务动态发现全套完成后触发。DevWindow 订阅此事件以应对首次 bootstrap
        // 场景（Open 时 PageOrder 尚未建 → OnEnable 扑空 → 空白窗体）。
        public static event Action EnsureCompleted;

        private const string GameRootAssetPath = "Assets/Game";
        private const string GameBootAssetPath = FrameAssetPaths.Root + "/GameBoot.cs";

        // 老版本把 GameBoot.cs 投在 Game.Managers 下；升级走 MoveAsset 保住 GUID。
        private const string LegacyGameBootAssetPath = GameRootAssetPath + "/Manager/GameBoot.cs";

        private const string GameSkeletonSourceRelative =
            "Packages/com.l47coder.dev-workbench/Runtime~/Templates/Game";

        private const string FrameGroupName = "Frame";

        private static readonly string ManagerOrderAddress =
            $"{FrameGroupName}/{Path.GetFileNameWithoutExtension(FrameAssetPaths.ManagerOrder)}";
        private static readonly string ComponentOrderAddress =
            $"{FrameGroupName}/{Path.GetFileNameWithoutExtension(FrameAssetPaths.ComponentOrder)}";

        static DevWindowFrameworkGuard()
        {
            EditorApplication.delayCall += TryRerunAfterReload;
        }

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
                // 放在 StartAssetEditing 之外：AssetFolderCopier.Import 内部 Refresh 必须
                // 立刻生效，否则后续 CreateFolder 看不到新建目录，会出 "Frame 1" 带数字副本。
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
                    EnsureOrderAsset<ManagerOrderConfig>(FrameAssetPaths.ManagerOrder, ManagerOrderAddress);
                    EnsureOrderAsset<ComponentOrderConfig>(FrameAssetPaths.ComponentOrder, ComponentOrderAddress);
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
                EditorUtility.DisplayDialog(
                    "Dev Workbench",
                    $"Framework auto-initialise failed:\n{ex.Message}\n\nSee Console for details.",
                    "OK");
            }

            // Page.OnWorkbenchOpen 可能自行 SaveAssets，不宜被 Frame 批量 editing 包住；
            // 且 Frame 段异常不该阻断 Page 层贡献——故放 try/catch 外。
            RunAllPageContributions();

            try { EnsureCompleted?.Invoke(); }
            catch (Exception ex)
            {
                Debug.LogError($"[DevWindowFrameworkGuard] EnsureCompleted subscriber threw: {ex}");
            }
        }

        // 把 Runtime~/Templates/Game 整树镜像到 Assets/Game，返回新落盘文件数。
        // Import 之前做一次 legacy GameBoot.cs 迁移（MoveAsset 保 GUID）。
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

            AssetPathUtil.EnsureFolder(FrameAssetPaths.Root);

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

        // PageOrder 仅供 DevWindow 菜单/标签排序，editor-only，不挂 Addressables。
        private static void EnsurePageOrderAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<PageOrder>(FrameAssetPaths.PageOrder) != null) return;

            AssetPathUtil.EnsureFolder(FrameAssetPaths.Root);
            var asset = ScriptableObject.CreateInstance<PageOrder>();
            AssetDatabase.CreateAsset(asset, FrameAssetPaths.PageOrder);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureOrderAsset<T>(string assetPath, string address) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                AssetPathUtil.EnsureFolder(FrameAssetPaths.Root);
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

        // 反射扫所有 IPage 实现，new 一次性实例触发 OnWorkbenchOpen。
        // 契约：OnWorkbenchOpen 只读/写项目资产，不依赖 UI 字段——临时实例用完即丢安全。
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
