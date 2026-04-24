using UnityEditor;

namespace DevWorkbench.Editor
{
    /// <summary>
    /// Manager-subsystem workbench contribution. Ensures every
    /// <c>BaseManagerConfig</c> asset is registered in Addressables and keeps
    /// <see cref="ManagerOrderConfig"/> in sync with the current set of
    /// concrete managers. Runs once per Editor session via the framework guard.
    /// </summary>
    internal sealed class ManagerWorkbenchContribution : IWorkbenchContribution
    {
        public void Contribute()
        {
            ManagerConfigInstaller.EnsureAllRegistered();

            var order = AssetDatabase.LoadAssetAtPath<ManagerOrderConfig>(GameFramePaths.ManagerOrder);
            if (order != null) ManagerOrderSync.Sync(order);
        }
    }
}
