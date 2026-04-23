using System;
using System.Linq;
using Unity.CodeEditor;
using UnityEditor;

namespace DevWorkbench.Editor
{
    // 资源导入完成后自动触发一次 IDE 项目文件（.sln / .csproj）重写，
    // 避免新建脚本 / asmdef 后 Cursor 等外部 IDE 读到旧的 csproj 报
    // "未能找到类型或命名空间名" 的假阳性错误。
    internal sealed class ProjectFilesAutoSync : AssetPostprocessor
    {
        private static bool _pending;

        private static void OnPostprocessAllAssets(
            string[] imported,
            string[] deleted,
            string[] movedTo,
            string[] movedFrom)
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
            catch (Exception e) { UnityEngine.Debug.LogWarning($"[DevWorkbench] SyncAll failed: {e.Message}"); }
        }

        private static bool AffectsCompilation(string[] paths) => paths != null && paths.Any(IsCodeAsset);

        private static bool IsCodeAsset(string path) =>
            path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".asmref", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".rsp", StringComparison.OrdinalIgnoreCase);
    }
}
