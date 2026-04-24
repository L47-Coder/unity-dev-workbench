using UnityEditor;

namespace DevWorkbench.Editor
{
    /// <summary>
    /// Component-subsystem workbench contribution. Ensures every
    /// <c>BaseComponentConfig</c> asset is registered in Addressables and keeps
    /// <see cref="ComponentOrderConfig"/> in sync with the current set of
    /// concrete components. Runs once per Editor session via the framework
    /// guard.
    /// </summary>
    internal sealed class ComponentWorkbenchContribution : IWorkbenchContribution
    {
        public void Contribute()
        {
            ComponentConfigInstaller.EnsureAllRegistered();

            var order = AssetDatabase.LoadAssetAtPath<ComponentOrderConfig>(GameFramePaths.ComponentOrder);
            if (order != null) ComponentOrderSync.Sync(order);
        }
    }
}
