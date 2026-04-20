using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // 架构初始化单一入口。检测项全部以项目自身状态为准（不依赖任何持久化 flag），
    // 因此可幂等重入、支持自愈（用户手动删了 Group / 资产后会自动再弹遮罩）。
    //
    // "Manager 程序集容器"这一项以 Assets/Game/Manager/Game.Managers.asmdef 的存在为准——
    // 无论用户是否安装了任一默认 Manager 包，只要想在 Assets/Game/Manager/ 下写 Manager，
    // 就需要这个 asmdef 先就位，所以它作为"框架基础设施"的一部分由本类负责投放。
    //
    // 具体"哪些默认 Manager 包被安装了"不再是 Bootstrap 的关注点，
    // 改由 ManagerInstallerPage 驱动用户按需选择安装。
    [InitializeOnLoad]
    internal static class FrameworkBootstrapper
    {
        // 用户通过 Installer 安装任一默认包后，Unity 会重新编译；编译完 domain reload
        // 会走到这里，我们再跑一次 InitializeAll 把新包里的 ManagerConfig 的 asset / Addressable 补齐。
        static FrameworkBootstrapper()
        {
            EditorApplication.delayCall += TryRerunInitializeAfterReload;
        }

        private static void TryRerunInitializeAfterReload()
        {
            if (!SessionState.GetBool(DefaultManagerInstaller.SessionKeyRerunInitialize, false)) return;
            SessionState.EraseBool(DefaultManagerInstaller.SessionKeyRerunInitialize);

            try { InitializeAll(); }
            catch (System.Exception ex) { Debug.LogError($"[FrameworkBootstrapper] Rerun after default package install failed: {ex}"); }
        }

        public sealed class Check
        {
            public string Label;
            public bool Passed;
            public string Detail;
        }

        public sealed class Status
        {
            public List<Check> Checks = new();
            public bool IsReady => Checks.All(c => c.Passed);
        }

        // ── 状态检测（只读） ──────────────────────────────────────────────────────

        public static Status CheckStatus()
        {
            var status = new Status();

            var containerReady = DefaultManagerInstaller.IsContainerInstalled();
            status.Checks.Add(new Check
            {
                Label = "Manager assembly ready",
                Passed = containerReady,
                Detail = containerReady
                    ? null
                    : $"{DefaultManagerInstaller.AsmdefAssetPath} is missing. Initialise will create it so that Assets/Game/Manager/ becomes a compile-ready assembly.",
            });

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            status.Checks.Add(new Check
            {
                Label = "Addressables initialised",
                Passed = settings != null,
                Detail = settings != null ? null : "AddressableAssetSettings not found. Initialise will create it automatically.",
            });

            status.Checks.Add(CheckAssetRegistered(
                settings,
                "ManagerOrder asset registered",
                FrameAssetInstaller.ManagerOrderAssetPath,
                FrameAssetInstaller.ManagerOrderAddress));

            status.Checks.Add(CheckAssetRegistered(
                settings,
                "ComponentOrder asset registered",
                FrameAssetInstaller.ComponentOrderAssetPath,
                FrameAssetInstaller.ComponentOrderAddress));

            status.Checks.Add(CheckManagerConfigs(settings));

            return status;
        }

        // 只检查"资产被正确挂上 Addressable 且 address 对齐"；分组是否存在不是独立维度，
        // 注册资产时若分组不存在会按需创建，因此无需单独检测。
        private static Check CheckAssetRegistered(
            AddressableAssetSettings settings, string label, string assetPath, string address)
        {
            if (settings == null)
                return new Check { Label = label, Passed = false, Detail = "Addressables are not initialised." };

            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath) == null)
                return new Check { Label = label, Passed = false, Detail = $"{assetPath} does not exist." };

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var entry = settings.FindAssetEntry(guid);
            if (entry == null)
                return new Check { Label = label, Passed = false, Detail = $"{assetPath} is not registered as an Addressable entry." };

            if (entry.address != address)
                return new Check { Label = label, Passed = false, Detail = $"Address should be \"{address}\", but is currently \"{entry.address}\"." };

            return new Check { Label = label, Passed = true };
        }

        private static Check CheckManagerConfigs(AddressableAssetSettings settings)
        {
            const string label = "All ManagerConfigs registered";

            if (settings == null)
                return new Check { Label = label, Passed = false, Detail = "Addressables are not initialised." };

            var infos = ManagerConfigInstaller.Collect();
            var pending = infos.Where(i => !i.AddressMatches).ToList();

            if (pending.Count == 0)
                return new Check
                {
                    Label = label,
                    Passed = true,
                    Detail = infos.Count == 0 ? "No ManagerConfig was found." : $"{infos.Count} item(s) total.",
                };

            var names = string.Join(", ", pending.Select(p => p.ManagerName));
            return new Check
            {
                Label = label,
                Passed = false,
                Detail = $"{pending.Count} pending: {names}",
            };
        }

        // ── 一键初始化 ────────────────────────────────────────────────────────────

        public static void InitializeAll()
        {
            // Step 0：投放 Game.Managers.asmdef 容器（= Manager 程序集）。
            // 只写一个文件（不走 AssetDatabase.CreateAsset），不依赖脚本编译，所以无需让后续步骤等 domain reload。
            // 具体的默认 Manager 包此处不再强推，改由 ManagerInstallerPage 让用户按需安装。
            DefaultManagerInstaller.EnsureContainerInstalled();

            AssetDatabase.StartAssetEditing();
            try
            {
                // Step 1：确保 Addressables 已创建。
                FrameAssetInstaller.EnsureAddressablesInitialized();

                // Step 2：Frame 资产存在 + 标记为 Addressable（分组不存在时按需创建）。
                var managerOrder = FrameAssetInstaller.EnsureManagerOrderAsset();
                var componentOrder = FrameAssetInstaller.EnsureComponentOrderAsset();

                // Step 3：批量挂载所有 *ManagerConfig.asset 到 Addressable（同上，按需建组）。
                ManagerConfigInstaller.EnsureAllRegistered();

                // Step 4：同步 Order 列表（扫描 BaseManager / BaseComponent 子类）。
                FrameAssetInstaller.SyncManagerOrderEntries(managerOrder);
                FrameAssetInstaller.SyncComponentOrderEntries(componentOrder);

                // Step 5：批量执行 Refresher（把 Addressable 里的条目回写进各 ManagerConfig）。
                ManagerConfigInstaller.RunAllRefreshers();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log("[FrameworkBootstrapper] Framework initialisation complete.");
        }
    }
}
