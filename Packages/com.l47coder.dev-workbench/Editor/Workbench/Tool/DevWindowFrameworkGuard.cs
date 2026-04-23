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
    // 三条触发路径全部收敛到 Ensure()：
    //   1. DevWindow.Open 手动开窗——直接调 Ensure()
    //   2. TemplateInstaller 装完模板后 domain reload——[InitializeOnLoad] 静态构造挂
    //      delayCall 检测 SessionKeyRerunInitialize flag，命中则调 Ensure()
    //   3. Tools/Dev Workbench/Sync Runtime 菜单（SyncRuntimeMenu）——直接调 Ensure()
    //
    // Ensure() 的幂等步骤：
    //   Step 0+1：Frame 元结构 —— 三个容器 asmdef + 默认 GameBoot.cs
    //             由 AssetFolderCopier 从 Runtime~/Templates/Game 整树镜像到 Assets/Game
    //   Step 2：  Addressables settings 本身
    //   Step 3：  Frame 下三份 Order SO（Manager / Component / Page），前两者挂 Addressables
    //   Step 4：  所有 IPage.OnWorkbenchOpen 的模块贡献（反射扫 TypeCache）
    //
    // 失败兜底：Frame 段 try/catch → LogError + Dialog，不向上抛；Page 段独立执行
    // ——Frame 失败不应阻断 Page 层本能跑的模块贡献。
    [InitializeOnLoad]
    internal static class DevWindowFrameworkGuard
    {
        // TemplateInstaller 装完模板会写入此 flag；编译完成触发 domain reload，
        // 本类的静态构造挂 delayCall 读 flag → 重跑 Ensure()，把新包的 Config
        // 扫进 Addressables、同步 Order。对外 const，TemplateInstaller 要引用。
        public const string SessionKeyRerunInitialize = "DevWorkbench.FrameworkGuard.RerunInitialize";

        // Assets 侧目标根。AssetFolderCopier 会把模板镜像到这里。
        private const string GameRootAssetPath = "Assets/Game";
        private const string GameBootAssetPath = FrameAssetPaths.Root + "/GameBoot.cs";

        // 历史落点：老版本在 Game.Managers 程序集目录下生成 GameBoot.cs，升级后需要迁移
        // 以保留 .meta 的 GUID（场景里已挂的 GameBoot MonoBehaviour 引用不会变 Missing）。
        private const string LegacyGameBootAssetPath = GameRootAssetPath + "/Manager/GameBoot.cs";

        // Frame 元结构模板根。目录树与 Assets/Game 一一对应，Import 一次即可覆盖所有容器
        // asmdef 与默认 GameBoot.cs。修改默认模板直接编辑 Runtime~ 下真实文件即可，不用动 C#。
        private const string GameSkeletonSourceRelative =
            "Packages/com.l47coder.dev-workbench/Runtime~/Templates/Game";

        private const string FrameGroupName = "Frame";

        private static readonly string ManagerOrderAddress =
            $"{FrameGroupName}/{Path.GetFileNameWithoutExtension(FrameAssetPaths.ManagerOrder)}";
        private static readonly string ComponentOrderAddress =
            $"{FrameGroupName}/{Path.GetFileNameWithoutExtension(FrameAssetPaths.ComponentOrder)}";

        // ── domain-reload rerun 触发器 ────────────────────────────────────────────

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

        // ── 对外入口 ──────────────────────────────────────────────────────────────

        // 幂等完整性兜底。资产齐全时几乎零写盘；有缺口自动补齐。
        // Frame 段异常捕获 → LogError + DisplayDialog，不向上抛；
        // Page 贡献段独立 try——Frame 失败也不应阻断 Page 层本能跑的模块贡献。
        public static void Ensure()
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                try
                {
                    EnsureGameSkeleton();

                    EnsureAddressablesInitialized();

                    EnsureManagerOrderAsset();
                    EnsureComponentOrderAsset();
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

            // 刻意放在 Frame 段的 StartAssetEditing 块与 try/catch 之外：
            //   - Page.OnWorkbenchOpen 可能自行触发 SaveAssets，不适合被 Frame 的批量 editing 包住；
            //   - Frame 段抛异常也不应阻断 Page 层的贡献。
            RunAllPageContributions();
        }

        // ── Step 0 + 1：Frame 元结构（容器 asmdef × 3 + 默认 GameBoot.cs） ───────

        // 把 Runtime~/Templates/Game 整棵模板目录镜像拷到 Assets/Game：
        //   Templates/Game/Manager/Game.Managers.asmdef    → Assets/Game/Manager/
        //   Templates/Game/Component/Game.Components.asmdef → Assets/Game/Component/
        //   Templates/Game/Frame/Game.Frame.asmdef         → Assets/Game/Frame/
        //   Templates/Game/Frame/GameBoot.cs               → Assets/Game/Frame/
        // AssetFolderCopier 内部对重名文件跳过、AssetDatabase.Refresh 由它管。
        //
        // 必须在 Import 之前做一次"历史 GameBoot.cs 迁移"——老版本把 GameBoot.cs 投放在
        // Assets/Game/Manager 下，升级时要用 AssetDatabase.MoveAsset 搬到 Assets/Game/Frame 以
        // 连同 .meta 保住 GUID（场景里挂的 GameBoot MonoBehaviour 不会变 Missing）。迁移完
        // 目标位置已有 GameBoot.cs，后续 Import 会自然跳过。
        private static void EnsureGameSkeleton()
        {
            TryMigrateLegacyGameBoot();

            try
            {
                AssetFolderCopier.Import(GameSkeletonSourceRelative, GameRootAssetPath);
            }
            catch (FileNotFoundException)
            {
                Debug.LogError($"[DevWindowFrameworkGuard] Game skeleton template missing: {GameSkeletonSourceRelative}.");
            }
        }

        private static void TryMigrateLegacyGameBoot()
        {
            if (File.Exists(ToAbsolute(GameBootAssetPath))) return;
            if (!File.Exists(ToAbsolute(LegacyGameBootAssetPath))) return;

            EnsureFolder(FrameAssetPaths.Root);

            var error = AssetDatabase.MoveAsset(LegacyGameBootAssetPath, GameBootAssetPath);
            if (string.IsNullOrEmpty(error))
            {
                Debug.Log($"[DevWindowFrameworkGuard] Migrated legacy GameBoot.cs from {LegacyGameBootAssetPath} to {GameBootAssetPath}.");
                return;
            }

            Debug.LogWarning($"[DevWindowFrameworkGuard] MoveAsset failed ({error}); falling back to template import. Please remove {LegacyGameBootAssetPath} manually if it still exists.");
        }

        // ── Step 2：Addressables settings ────────────────────────────────────────

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

        // ── Step 3：Frame 下三个 Order SO ────────────────────────────────────────

        private static void EnsureManagerOrderAsset()
        {
            EnsureOrderAsset<ManagerOrderConfig>(FrameAssetPaths.ManagerOrder, ManagerOrderAddress);
        }

        private static void EnsureComponentOrderAsset()
        {
            EnsureOrderAsset<ComponentOrderConfig>(FrameAssetPaths.ComponentOrder, ComponentOrderAddress);
        }

        // PageOrder 仅供 DevWindow 菜单/标签排序，editor-only 偏好，不挂 Addressables。
        private static void EnsurePageOrderAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<PageOrder>(FrameAssetPaths.PageOrder);
            if (asset != null) return;

            EnsureFolder(FrameAssetPaths.Root);
            asset = ScriptableObject.CreateInstance<PageOrder>();
            AssetDatabase.CreateAsset(asset, FrameAssetPaths.PageOrder);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureOrderAsset<T>(string assetPath, string address) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                EnsureFolder(FrameAssetPaths.Root);
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.SaveAssets();
            }

            EnsureAddressableEntry(assetPath, FrameGroupName, address);
        }

        // ── Addressables 小工具 ──────────────────────────────────────────────────

        private static AddressableAssetGroup EnsureAddressableGroup(string groupName)
        {
            if (string.IsNullOrEmpty(groupName)) return null;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return null;

            var group = settings.groups.FirstOrDefault(g => g != null && g.Name == groupName);
            if (group != null) return group;

            group = settings.CreateGroup(groupName, false, false, true, null);
            EditorUtility.SetDirty(settings);
            return group;
        }

        private static void EnsureAddressableEntry(string assetPath, string groupName, string address)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning($"[DevWindowFrameworkGuard] AddressableAssetSettings not found; skipping registration of {assetPath}.");
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid)) return;

            var group = EnsureAddressableGroup(groupName);
            if (group == null) return;

            var existing = settings.FindAssetEntry(guid);
            if (existing != null && existing.parentGroup == group && existing.address == address)
                return;

            var entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = address;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
            AssetDatabase.SaveAssets();
        }

        // ── 文件系统小工具 ───────────────────────────────────────────────────────

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

        // ── Step 4：Page 层批量贡献 ──────────────────────────────────────────────

        // 反射扫所有 IPage 实现，为每个类型 new 出一次性实例并触发 OnWorkbenchOpen。
        // 契约：OnWorkbenchOpen 必须是"纯静态动作（只读/写项目资产，不依赖 UI 字段）"，
        // 因此这里用完即丢的临时实例是安全的，无需复用 DevWindow 里维护的那批。
        private static void RunAllPageContributions()
        {
            foreach (var type in TypeCache.GetTypesDerivedFrom<IPage>())
            {
                if (type.IsAbstract || type.IsInterface) continue;

                IPage page;
                try
                {
                    page = Activator.CreateInstance(type) as IPage;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DevWindowFrameworkGuard] Failed to instantiate {type.FullName}: {ex.Message}");
                    continue;
                }

                if (page == null) continue;

                try
                {
                    page.OnWorkbenchOpen();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DevWindowFrameworkGuard] {type.Name}.OnWorkbenchOpen threw: {ex}");
                }
            }
        }
    }
}
