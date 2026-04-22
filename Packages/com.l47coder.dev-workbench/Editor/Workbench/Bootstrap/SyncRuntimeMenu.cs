using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // Tools/Dev Workbench/Sync Runtime —— 运行前手动"把所有东西对齐"的入口。
    //
    // 相对于 DevWindow.OnEnable 自动跑的 RunFullEnsure，这里多一步 RunAllRefreshers：
    //   - RunAllRefreshers 会覆盖每个 ManagerConfig 的 _configs 列表内容，属于"可能破坏用户
    //     在 Inspector 里手填数据"的集体同步。所以不放进自动流程，只由这个菜单显式触发。
    //
    // 不依赖 DevWindow 是否打开——可以开也可以不开，独立工作。
    internal static class SyncRuntimeMenu
    {
        [MenuItem("Tools/Dev Workbench/Sync Runtime")]
        private static void SyncRuntime()
        {
            FrameworkBootstrapper.RunFullEnsure();

            var refreshed = ManagerConfigInstaller.RunAllRefreshers();
            Debug.Log($"[DevWorkbench] Runtime synced. Refreshers executed: {refreshed}.");
        }
    }
}
