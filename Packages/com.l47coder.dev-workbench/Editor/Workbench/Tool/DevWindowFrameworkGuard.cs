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
    //   2. TemplateInstaller / 首次拷 Game 模板后 domain reload——[InitializeOnLoad]
    //      静态构造挂 delayCall 检测 SessionKeyRerunInitialize flag，命中则 Ensure()
    //   3. Tools/Dev Workbench/Sync Runtime 菜单（SyncRuntimeMenu）——直接调 Ensure()
    //
    // Ensure() 的两段时序（关键！）：
    //   ——首次 bootstrap 分支（skeleton 拷贝数 > 0）——
    //     Step 0：EnsureGameSkeleton 从 Runtime~/Templates/Game 镜像到 Assets/Game，
    //             写入三个容器 asmdef + 默认 GameBoot.cs；AssetFolderCopier 内部会
    //             Refresh → Unity 触发编译 → 即将 domain reload。
    //     Step 1：写 SessionKeyRerunInitialize flag → 直接 return。**不做**后续动作，
    //             因为此时新脚本还没编译，反射扫不到；且若继续在同一次 AssetEditing
    //             批里建 Order SO 会与 Import 新建的物理目录撞车（出 "Frame 1"）。
    //     Step 2：domain reload 完成 → Guard 静态构造读 flag → 自动再跑 Ensure()，
    //             走下面"已初始化"分支，把剩下全部做完。
    //   ——已初始化分支（skeleton 拷贝数 == 0）——
    //     Step 2：Addressables settings 本身
    //     Step 3：Frame 下三份 Order SO（Manager / Component / Page），前两者挂
    //             Addressables "Frame" 组
    //     Step 4：所有 IPage.OnWorkbenchOpen 的业务模块动态发现（反射扫 TypeCache）
    //
    // 这条两段式保证了"架构完整性在 OnWorkbenchOpen 之前已完成 + 每次开窗只执行一次"。
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

        // 架构完整性 + 业务模块动态发现全部跑完后触发。DevWindow 订阅此事件来处理
        // "首次 bootstrap：Open 时 PageOrder 尚未建好 → OnEnable 扑空 → 窗体空白"
        // 的情况——等 domain reload 后 rerun 的 Ensure 完工时用事件触发 BuildPageTree。
        //
        // 首次 bootstrap 分支的短路 return 刻意不 Invoke——那时没建过 PageOrder，
        // 通知订阅方也没用。只有"已初始化分支"的全链路跑完才 Invoke，订阅方拿到
        // 通知就一定能 Load 到 PageOrder、扫到新 IPage 类型。
        public static event Action EnsureCompleted;

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

        // 幂等完整性兜底。资产齐全时几乎零写盘；有缺口自动补齐。详细时序见类顶注。
        // Frame 段异常捕获 → LogError + DisplayDialog，不向上抛；
        // Page 贡献段独立 try——Frame 失败也不应阻断 Page 层本能跑的模块贡献。
        public static void Ensure()
        {
            try
            {
                // 刻意放在 StartAssetEditing 之外——AssetFolderCopier.Import 内部
                // 需要 AssetDatabase.Refresh 立刻生效，否则后续 CreateFolder 看不到
                // Import 新建的物理目录，会出现 "Frame 1" 这种带数字后缀的副本。
                var skeletonCopied = EnsureGameSkeleton();

                // 首次 bootstrap：写 rerun flag 后退出，等 domain reload 后再跑完全套。
                // 见类顶注"首次 bootstrap 分支"。
                if (skeletonCopied > 0)
                {
                    SessionState.SetBool(SessionKeyRerunInitialize, true);
                    return;
                }

                AssetDatabase.StartAssetEditing();
                try
                {
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
                // 兜底后仍然走 Page 贡献——Frame 段抛异常不应阻断 Page 层本能跑的模块贡献。
            }

            // 业务模块动态发现（OnWorkbenchOpen）——架构完整性已由上面步骤完成，
            // 这里只扫 domain reload 后新编译出的用户类型并就地补 asset / 同步 Order。
            // 刻意放在 StartAssetEditing 块之外：Page 可能自行 SaveAssets，不适合被批量 editing 包住。
            RunAllPageContributions();

            // 至此"架构 + 业务动态发现"全套完成。通知订阅方（DevWindow）。
            // 注意：首次 bootstrap 分支在上面已 return，不到这里——domain reload
            // 后 rerun 再跑一次 Ensure 时会从这里走出来。
            try
            {
                EnsureCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DevWindowFrameworkGuard] EnsureCompleted subscriber threw: {ex}");
            }
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
        //
        // 返回值：实际新落盘的文件数（Import 拷贝的 + legacy 迁移走的）。>0 表示本次触发了
        // 脚本 / 程序集归属变化，编译器会重跑 → domain reload——调用方据此写
        // SessionKeyRerunInitialize flag 并提前 return，交给 reload 后的 rerun 接手。
        private static int EnsureGameSkeleton()
        {
            // Legacy 搬迁改的是 GameBoot.cs 所属的 asmdef，虽没改代码内容但也会触发编译，
            // 所以算一次"新落盘"。
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
            if (File.Exists(ToAbsolute(GameBootAssetPath))) return false;
            if (!File.Exists(ToAbsolute(LegacyGameBootAssetPath))) return false;

            EnsureFolder(FrameAssetPaths.Root);

            var error = AssetDatabase.MoveAsset(LegacyGameBootAssetPath, GameBootAssetPath);
            if (string.IsNullOrEmpty(error))
            {
                Debug.Log($"[DevWindowFrameworkGuard] Migrated legacy GameBoot.cs from {LegacyGameBootAssetPath} to {GameBootAssetPath}.");
                return true;
            }

            Debug.LogWarning($"[DevWindowFrameworkGuard] MoveAsset failed ({error}); falling back to template import. Please remove {LegacyGameBootAssetPath} manually if it still exists.");
            return false;
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
