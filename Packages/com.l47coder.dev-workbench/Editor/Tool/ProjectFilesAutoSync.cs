using System;
using System.Linq;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    internal sealed class ProjectFilesAutoSync : AssetPostprocessor
    {
        private static bool _pending;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] movedTo, string[] movedFrom)
        {
            if (!AffectsCompilation(imported) &&
                !AffectsCompilation(deleted) &&
                !AffectsCompilation(movedTo) &&
                !AffectsCompilation(movedFrom))
                return;

            if (_pending) return;
            _pending = true;

            EditorApplication.delayCall += Sync;
        }

        private static void Sync()
        {
            _pending = false;
            try { CodeEditor.CurrentEditor.SyncAll(); }
            catch (Exception e) { Debug.LogWarning($"[DevWorkbench] SyncAll failed: {e.Message}"); }
        }

        private static bool AffectsCompilation(string[] paths) => paths != null && paths.Any(IsCodeAsset);

        private static bool IsCodeAsset(string path) =>
            path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".asmref", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".rsp", StringComparison.OrdinalIgnoreCase);
    }
}
