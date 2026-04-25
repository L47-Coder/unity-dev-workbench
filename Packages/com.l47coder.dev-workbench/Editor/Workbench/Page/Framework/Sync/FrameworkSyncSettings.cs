using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    internal enum FrameworkSyncTrigger
    {
        Manual = 0,
        OnWorkbenchClose = 1,
        BeforePlayMode = 2,
    }

    [InitializeOnLoad]
    internal static class FrameworkSyncSettings
    {
        private const string TriggerKey = "DevWorkbench.FrameworkSync.Trigger";

        public static FrameworkSyncTrigger Trigger
        {
            get => (FrameworkSyncTrigger)EditorPrefs.GetInt(TriggerKey, (int)FrameworkSyncTrigger.Manual);
            set => EditorPrefs.SetInt(TriggerKey, (int)value);
        }

        static FrameworkSyncSettings() => EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode) return;
            if (Trigger != FrameworkSyncTrigger.BeforePlayMode) return;
            RunSync("before play mode");
        }

        public static void OnDevWindowClosed()
        {
            if (Trigger != FrameworkSyncTrigger.OnWorkbenchClose) return;
            RunSync("on workbench close");
        }

        public static int RunSync(string reason)
        {
            var executed = EditorSyncRunner.RunAll();
            Debug.Log($"[DevWorkbench] Runtime synced ({reason}). EditorSync methods executed: {executed}.");
            return executed;
        }
    }
}
