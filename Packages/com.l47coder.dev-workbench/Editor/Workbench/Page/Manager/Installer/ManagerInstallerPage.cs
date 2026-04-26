using System.Collections.Generic;
using System.Linq;

namespace DevWorkbench.Editor
{
    internal sealed class ManagerInstallerPage : InstallerPageBase
    {
        public override string GroupTitle => "Manager";
        public override string TabTitle => "Installer";
        protected override string IntroHeader => "Built-in Manager templates";
        protected override string IntroBodyText =>
            "Pick the Manager templates you want and click Import. These templates are optional; only the Game.Managers.asmdef container is created by the framework itself.";
        protected override string EmptyHelpBoxText =>
            "No built-in Manager templates ship with this package version. "
            + $"Use the Creator tab to scaffold your own Manager classes under {GameProjectPaths.ManagerRoot}/.";
        protected override string LogTag => "ManagerInstallerPage";

        protected override void InvalidateCache() => ManagerTemplateInstaller.InvalidateManifestCache();

        protected override IReadOnlyList<PackageInfo> LoadPackages()
            => ManagerTemplateInstaller.LoadManifest()
                .Select(p => new PackageInfo(p.id, p.displayName, p.description, p.recommended))
                .ToArray();

        protected override bool CheckIsInstalled(string id) => ManagerTemplateInstaller.IsPackageInstalled(id);

        protected override int PerformInstall(IList<string> ids) => ManagerTemplateInstaller.InstallPackages(ids);
    }
}
