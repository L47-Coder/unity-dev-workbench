namespace DevWorkbench.Editor
{
    /// <summary>
    /// Single source of truth for package-local paths (package id, runtime
    /// template roots, ...). Centralizing these allows the package to be
    /// renamed or forked without touching every installer / guard.
    /// <para>
    /// Must stay in sync with <c>package.json</c>'s <c>name</c> field.
    /// </para>
    /// </summary>
    internal static class DevWorkbenchPackageInfo
    {
        /// <summary>The upm package id, equal to <c>package.json</c>'s <c>name</c>.</summary>
        public const string PackageId = "com.l47coder.dev-workbench";

        /// <summary>Virtual folder that resolves to the installed package root.</summary>
        public const string PackageRoot = "Packages/" + PackageId;

        /// <summary>
        /// Hidden runtime asset folder (the tilde suffix keeps Unity from
        /// importing it). Holds skeleton + manager + component templates.
        /// </summary>
        public const string RuntimeTemplatesRoot = PackageRoot + "/Runtime~/Templates";

        /// <summary>Folder containing the <c>Assets/Game</c> skeleton copied on first open.</summary>
        public const string GameSkeletonTemplateFolder = RuntimeTemplatesRoot + "/Game";

        /// <summary>Folder containing the bundled Manager templates (Asset / Prefab / Component).</summary>
        public const string ManagerTemplatesFolder = RuntimeTemplatesRoot + "/Managers";

        /// <summary>Folder containing the bundled Component templates.</summary>
        public const string ComponentTemplatesFolder = RuntimeTemplatesRoot + "/Components";
    }
}
