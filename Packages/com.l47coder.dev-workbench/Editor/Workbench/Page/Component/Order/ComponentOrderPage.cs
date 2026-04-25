using System.Collections.Generic;

namespace DevWorkbench.Editor
{
    internal sealed class ComponentOrderPage : OrderPageBase<ComponentOrderConfig, ComponentOrderEntry>
    {
        public override string GroupTitle => "Component";
        public override string TabTitle   => "Order";

        protected override string ConfigAssetPath    => GameFramePaths.ComponentOrder;
        protected override string SearchColumnHeader => "Component";
        protected override void   Sync(ComponentOrderConfig config) => ComponentOrderSync.Sync(config);
        protected override List<ComponentOrderEntry> GetEntries(ComponentOrderConfig config) => config.Entries;
    }
}
