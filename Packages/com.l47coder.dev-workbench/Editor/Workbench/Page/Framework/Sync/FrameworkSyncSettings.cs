using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    internal enum FrameworkSyncTrigger
    {
        // 仅由 FrameworkSyncPage 上的按钮触发；自动路径全关。
        Manual = 0,

        // Dev Workbench 窗口关闭时自动跑一次。
        // 刻意挂在 OnDestroy 而不是 OnDisable：OnDisable 在 domain reload 也会走，
        // 会在"编辑代码"期间被误触发。
        OnWorkbenchClose = 1,

        // 进入 Play Mode 前自动跑一次（ExitingEditMode 阶段，同步执行）。
        BeforePlayMode = 2,
    }

    // Framework/Sync 的配置 + 自动触发挂钩。设置存 EditorPrefs（开发者个人本地偏好，
    // 不入工程资产也不进 git）；UI 只负责读写属性、真正的"在某某时机跑一次"逻辑在这里。
    [InitializeOnLoad]
    internal static class FrameworkSyncSettings
    {
        // key 里带包前缀避免和其他工具冲突；值是 FrameworkSyncTrigger 的 int。
        private const string TriggerKey = "DevWorkbench.FrameworkSync.Trigger";

        public static FrameworkSyncTrigger Trigger
        {
            get => (FrameworkSyncTrigger)EditorPrefs.GetInt(TriggerKey, (int)FrameworkSyncTrigger.Manual);
            set => EditorPrefs.SetInt(TriggerKey, (int)value);
        }

        static FrameworkSyncSettings()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode) return;
            if (Trigger != FrameworkSyncTrigger.BeforePlayMode) return;
            RunSync("before play mode");
        }

        // 由 DevWindow.OnDestroy 显式回调——避免在 FrameworkSyncPage 里订阅"我所在的
        // window 关了没"这种反向依赖。
        public static void OnDevWindowClosed()
        {
            if (Trigger != FrameworkSyncTrigger.OnWorkbenchClose) return;
            RunSync("on workbench close");
        }

        public static int RunSync(string reason)
        {
            DevWindowFrameworkGuard.Ensure();
            var refreshed = ManagerConfigInstaller.RunAllRefreshers();
            Debug.Log($"[DevWorkbench] Runtime synced ({reason}). Refreshers executed: {refreshed}.");
            return refreshed;
        }
    }
}
