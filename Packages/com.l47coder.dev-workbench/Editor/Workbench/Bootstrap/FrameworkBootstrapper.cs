using System;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // DevWorkbench 的启动编排器。职责拆成两段：
    //
    //   1. EnsureFrame() —— 框架"元结构"的幂等兜底：
    //        - 三个容器 asmdef（Manager/Component/Frame，影响编译归属，必须最早）
    //        - GameBoot.cs（项目全局唯一的启动入口，携 GUID，不能靠任何业务模块）
    //        - Addressables settings
    //        - Frame 下三个 SO（ManagerOrder / ComponentOrder / PageOrder），
    //          其中前两个挂 Addressables "Frame" 组并对齐约定地址
    //      这些都不归属任何业务模块——没有 "FrameViewPage" 来承载，所以留在这里静态入口。
    //
    //   2. 业务模块（Manager / Component）自己的前置条件不在这里——下放到各模块的
    //      ViewPage 在 IPage.OnWorkbenchOpen() 里贡献；批量调度由 WorkbenchPageRunner 负责。
    //
    // 三处触发入口共享 RunFullEnsure：
    //   - DevWindow.OnEnable 一会话一次（SessionState 压）
    //   - Installer 装完模板后 domain reload 的 [InitializeOnLoad] rerun
    //   - Tools/Dev Workbench/Sync Runtime 菜单（见 SyncRuntimeMenu）
    //
    // 失败兜底：捕获异常 → Debug.LogError + DisplayDialog，不阻断后续 Page 流程。
    [InitializeOnLoad]
    internal static class FrameworkBootstrapper
    {
        // Installer（ManagerTemplateInstaller / ComponentTemplateInstaller）拷完模板设置此 flag，
        // 让我们在编译完成的 domain reload 里重跑一次完整 ensure，把新包里的 Config 挂到 Addressables。
        internal const string SessionKeyRerunInitialize = "DevWorkbench.Bootstrapper.RerunInitialize";

        static FrameworkBootstrapper()
        {
            EditorApplication.delayCall += TryRerunAfterReload;
        }

        private static void TryRerunAfterReload()
        {
            if (!SessionState.GetBool(SessionKeyRerunInitialize, false)) return;
            SessionState.EraseBool(SessionKeyRerunInitialize);

            RunFullEnsure();
        }

        // Frame 层 + 所有 Page 的 OnWorkbenchOpen 一次性跑完。
        // 幂等：资产齐全时几乎零写盘；有缺口则自动补齐。
        public static void RunFullEnsure()
        {
            try
            {
                EnsureFrame();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FrameworkBootstrapper] EnsureFrame failed: {ex}");
                EditorUtility.DisplayDialog(
                    "Dev Workbench",
                    $"Framework auto-initialise failed:\n{ex.Message}\n\nSee Console for details.",
                    "OK");
            }

            // Page 层单独 try：EnsureFrame 抛异常也不应该阻断本能跑的模块贡献。
            WorkbenchPageRunner.RunOnWorkbenchOpenForAll();
        }

        // Frame 层的幂等 ensure。调用者应当独占一次（DevWindow.OnEnable 首次 / rerun / 菜单）。
        public static void EnsureFrame()
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                // Step 0：三个容器 asmdef。必须最早——它们定义 .cs 的编译归属。
                ManagerTemplateInstaller.EnsureContainerInstalled();
                ComponentTemplateInstaller.EnsureContainerInstalled();
                FrameTemplateInstaller.EnsureContainerInstalled();

                // Step 1：启动入口脚本。项目全局唯一，携 MonoBehaviour GUID，场景引用依赖它。
                FrameTemplateInstaller.EnsureGameBootInstalled();

                // Step 2：Addressables settings 本身。
                FrameAssetInstaller.EnsureAddressablesInitialized();

                // Step 3：Frame 下三份 SO。ManagerOrder/ComponentOrder 同时挂 Addressables
                // "Frame" 组并对齐约定地址；PageOrder 是 editor-only 偏好，不挂 Addressables。
                FrameAssetInstaller.EnsureManagerOrderAsset();
                FrameAssetInstaller.EnsureComponentOrderAsset();
                FrameAssetInstaller.EnsurePageOrderAsset();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }
        }
    }
}
