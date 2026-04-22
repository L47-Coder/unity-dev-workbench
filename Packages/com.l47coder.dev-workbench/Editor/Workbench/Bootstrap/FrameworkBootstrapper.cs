using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // 架构初始化单一入口。检测项全部以项目自身状态为准（不依赖任何持久化 flag），因此可幂等重入。
    //
    // 检测时机说明：DevWindow 在一次 Unity 编辑器会话里只做一次 CheckStatus（首次打开时），
    // 靠 SessionState 跨 domain reload 保留标记；不订阅 EditorApplication.projectChanged，
    // 也不在每次 OnEnable 都跑。这样能规避两种瞬态噪音——删资产瞬间的"脚本已空但 asset 未清"、
    // 以及 Creator 生成后 domain reload 的"脚本已编译但 post-compile asset 未建"。
    // 结构真的坏了，靠用户重启 Unity 后下一次首开自检、或主动点 Initialise 修复。
    //
    // "Manager / Component 程序集容器"这两项以各自 .asmdef 的存在为准——
    // 无论用户是否安装了任一模板，只要想在 Assets/Game/Manager/ 或 Assets/Game/Component/
    // 下写代码，就需要对应 asmdef 先就位，否则会被吸进 Assembly-CSharp 和业务代码一起重编译，
    // 所以它们作为"框架基础设施"的一部分由本类负责投放。
    //
    // 具体"哪些模板被安装了"不再是 Bootstrap 的关注点，改由 ManagerInstallerPage /
    // ComponentInstallerPage 驱动用户按需选择安装。
    [InitializeOnLoad]
    internal static class FrameworkBootstrapper
    {
        // Installer 拷完用户选中的模板后，Unity 会重新编译；编译完 domain reload
        // 会走到这里，我们再跑一次 InitializeAll 把新包里的 ManagerConfig / ComponentConfig
        // 的 asset / Addressable / Order 补齐。Manager 侧和 Component 侧的 Installer 共用这把 key。
        internal const string SessionKeyRerunInitialize = "DevWorkbench.Bootstrapper.RerunInitialize";

        static FrameworkBootstrapper()
        {
            EditorApplication.delayCall += TryRerunInitializeAfterReload;
        }

        private static void TryRerunInitializeAfterReload()
        {
            if (!SessionState.GetBool(SessionKeyRerunInitialize, false)) return;
            SessionState.EraseBool(SessionKeyRerunInitialize);

            try { InitializeAll(); }
            catch (System.Exception ex) { Debug.LogError($"[FrameworkBootstrapper] Rerun after template install failed: {ex}"); }
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

        // 蒙版上只展示 3 条聚合项，保持用户的注意力集中：
        //   1. Project structure ready   —— 程序集容器 + Frame 下 asset 的"文件存在 + 关键挂载"
        //   2. Addressables initialised  —— AddressableAssetSettings 本身
        //   3. All configs registered    —— 所有 *ManagerConfig / *ComponentConfig 是否挂了 Addressable
        // 任何一条失败，Detail 里列具体缺了什么；Initialise 按钮负责一次性把所有问题修好。
        public static Status CheckStatus()
        {
            var status = new Status();
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            status.Checks.Add(CheckProjectStructure(settings));

            status.Checks.Add(new Check
            {
                Label = "Addressables initialised",
                Passed = settings != null,
                Detail = settings != null
                    ? null
                    : "AddressableAssetSettings not found. Initialise will create it automatically.",
            });

            status.Checks.Add(CheckAllConfigsRegistered(settings));

            return status;
        }

        // "项目结构完整"聚合了 5 项事实：
        //   - Game.Managers.asmdef 存在（Manager 程序集容器）
        //   - Game.Components.asmdef 存在（Component 程序集容器）
        //   - ManagerOrder.asset 存在 + 已挂 Addressable + address 对齐
        //   - ComponentOrder.asset 存在 + 已挂 Addressable + address 对齐
        //   - PageOrder.asset 存在（editor-only 偏好，故意不检查 Addressable）
        //
        // Addressables 未初始化时，Order 的 Addressable 部分暂缓判断——那是 Check 2 的职责，
        // 这里只报文件是否存在，避免"Addressables 未初始化"的错误在多条 Check 上重复出现。
        private static Check CheckProjectStructure(AddressableAssetSettings settings)
        {
            const string label = "Project structure ready";
            var issues = new List<string>();

            if (!ManagerTemplateInstaller.IsContainerInstalled())
                issues.Add($"{ManagerTemplateInstaller.AsmdefAssetPath} is missing.");

            if (!ComponentTemplateInstaller.IsContainerInstalled())
                issues.Add($"{ComponentTemplateInstaller.AsmdefAssetPath} is missing.");

            if (!FrameTemplateInstaller.IsContainerInstalled())
                issues.Add($"{FrameTemplateInstaller.AsmdefAssetPath} is missing.");

            if (!FrameTemplateInstaller.IsGameBootInstalled())
                issues.Add($"{FrameTemplateInstaller.GameBootAssetPath} is missing.");

            AppendOrderAssetIssues(settings, issues,
                FrameAssetInstaller.ManagerOrderAssetPath,
                FrameAssetInstaller.ManagerOrderAddress);

            AppendOrderAssetIssues(settings, issues,
                FrameAssetInstaller.ComponentOrderAssetPath,
                FrameAssetInstaller.ComponentOrderAddress);

            if (!AssetFileExists(FrameAssetInstaller.PageOrderAssetPath))
                issues.Add($"{FrameAssetInstaller.PageOrderAssetPath} does not exist.");

            if (issues.Count == 0)
                return new Check { Label = label, Passed = true };

            return new Check
            {
                Label = label,
                Passed = false,
                Detail = string.Join("\n", issues),
            };
        }

        private static void AppendOrderAssetIssues(
            AddressableAssetSettings settings, List<string> issues, string assetPath, string address)
        {
            if (!AssetFileExists(assetPath))
            {
                issues.Add($"{assetPath} does not exist.");
                return;
            }

            // Addressables 未初始化时，这里先放过——Check 2 会专门报这个错。
            if (settings == null) return;

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var entry = settings.FindAssetEntry(guid);
            if (entry == null)
            {
                issues.Add($"{assetPath} is not registered as an Addressable entry.");
                return;
            }

            if (entry.address != address)
                issues.Add($"{assetPath}: address should be \"{address}\", but is currently \"{entry.address}\".");
        }

        // asset 文件是否落盘——用 File.Exists 而非 AssetDatabase.LoadAssetAtPath，
        // 避开"脚本改名后 SO 反序列化失败但文件还在"这类瞬态。
        private static bool AssetFileExists(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            var abs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            return File.Exists(abs);
        }

        // Config 的 address 是"生成代码里硬编码 + 框架按约定反推"的——entry 缺失、address 漂移、
        // asset 不存在，三者都会让运行时 FrameworkLoader.LoadAsync 失败。因此全部计入 pending，
        // 统一交给 Initialise 按钮一把修（EnsureAllRegistered 本来就能把漂移纠回约定地址）。
        private static Check CheckAllConfigsRegistered(AddressableAssetSettings settings)
        {
            const string label = "All configs registered";

            if (settings == null)
                return new Check { Label = label, Passed = false, Detail = "Addressables are not initialised." };

            var managerInfos = ManagerConfigInstaller.Collect();
            var componentInfos = ComponentConfigInstaller.Collect();

            var pending = new List<string>();
            foreach (var info in managerInfos)
                if (!info.AssetExists || !info.HasAddressableEntry || !info.AddressMatches)
                    pending.Add($"{info.ManagerName} (Manager)");
            foreach (var info in componentInfos)
                if (!info.AssetExists || !info.HasAddressableEntry || !info.AddressMatches)
                    pending.Add($"{info.ComponentName} (Component)");

            var total = managerInfos.Count + componentInfos.Count;

            if (pending.Count == 0)
            {
                var detail = total == 0
                    ? "No manager or component configs found."
                    : $"{total} item(s) total.";
                return new Check { Label = label, Passed = true, Detail = detail };
            }

            return new Check
            {
                Label = label,
                Passed = false,
                Detail = $"{pending.Count} pending: {string.Join(", ", pending)}",
            };
        }

        // ── 一键初始化 ────────────────────────────────────────────────────────────

        public static void InitializeAll()
        {
            // Step 0：投放三个容器 asmdef + 默认 GameBoot。
            //   - Game.Managers.asmdef   → Assets/Game/Manager/
            //   - Game.Components.asmdef → Assets/Game/Component/
            //   - Game.Frame.asmdef      → Assets/Game/Frame/  （GameBoot 归属的程序集）
            //   - GameBoot.cs            → Assets/Game/Frame/GameBoot.cs
            // 只写文件（不走 AssetDatabase.CreateAsset），不依赖脚本编译，所以无需让后续步骤等 domain reload。
            // 可选 Manager/Component 模板不在这里强推，改由对应的 InstallerPage 让用户按需安装。
            ManagerTemplateInstaller.EnsureContainerInstalled();
            ComponentTemplateInstaller.EnsureContainerInstalled();
            FrameTemplateInstaller.EnsureContainerInstalled();

            // GameBoot 是"启动入口"，每个项目都必需且全局唯一。若历史版本曾把它投放在
            // Game.Managers 程序集目录，FrameTemplateInstaller 会用 AssetDatabase.MoveAsset
            // 保留 GUID 迁到 Game.Frame，场景里已挂的 GameBoot MonoBehaviour 引用不会丢。
            FrameTemplateInstaller.EnsureGameBootInstalled();

            AssetDatabase.StartAssetEditing();
            try
            {
                // Step 1：确保 Addressables 已创建。
                FrameAssetInstaller.EnsureAddressablesInitialized();

                // Step 2：Frame 资产存在 + 标记为 Addressable（分组不存在时按需创建）。
                // PageOrder 也在这里统一创建——它是 editor-only 偏好，不走 Addressables；
                // 创建权集中在 Initialise 流程里，避免 DevWindow 打开即产生未经授意的资产写入。
                var managerOrder = FrameAssetInstaller.EnsureManagerOrderAsset();
                var componentOrder = FrameAssetInstaller.EnsureComponentOrderAsset();
                FrameAssetInstaller.EnsurePageOrderAsset();

                // Step 3：批量挂载所有 *ManagerConfig.asset 和 *ComponentConfig.asset 到 Addressable
                // （同上，按需建组）。Component 侧没有 Refresher 机制，只需要 EnsureAllRegistered。
                ManagerConfigInstaller.EnsureAllRegistered();
                ComponentConfigInstaller.EnsureAllRegistered();

                // Step 4：同步 Order 列表（扫描 BaseManager / BaseComponent 子类）。
                FrameAssetInstaller.SyncManagerOrderEntries(managerOrder);
                FrameAssetInstaller.SyncComponentOrderEntries(componentOrder);

                // Step 5：批量执行 Refresher（把 Addressable 里的条目回写进各 ManagerConfig）。
                // Component 侧不参与：IComponentRefresher 不存在，对称性由业务语义决定。
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
