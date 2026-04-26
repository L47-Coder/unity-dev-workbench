using System.Collections.Generic;
using System.Linq;

namespace DevWorkbench.Editor
{
    internal sealed class ComponentInstallerPage : InstallerPageBase
    {
        public override string GroupTitle => "Component";
        public override string TabTitle => "Installer";
        protected override string IntroHeader => "Built-in Component templates";
        protected override string IntroBodyText =>
            "Select templates to import. Only Game.Components.asmdef is created by default.";
        protected override string EmptyHelpBoxText =>
            "No built-in Component templates found. Use the Creator tab to create your own.";
        protected override string LogTag => "ComponentInstallerPage";

        protected override void InvalidateCache() => ComponentTemplateInstaller.InvalidateManifestCache();

        protected override IReadOnlyList<PackageInfo> LoadPackages()
            => ComponentTemplateInstaller.LoadManifest()
                .Select(p => new PackageInfo(p.id, p.displayName, p.description, p.recommended))
                .ToArray();

        protected override bool CheckIsInstalled(string id) => ComponentTemplateInstaller.IsPackageInstalled(id);

        protected override int PerformInstall(IList<string> ids) => ComponentTemplateInstaller.InstallPackages(ids);
    }
}
