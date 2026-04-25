using System.Collections.Generic;

namespace DevWorkbench.Editor
{
    internal sealed class ManagerOrderPage : OrderPageBase<ManagerOrderConfig, ManagerOrderEntry>
    {
        public override string GroupTitle => "Manager";
        public override string TabTitle   => "Order";

        protected override string ConfigAssetPath    => GameFramePaths.ManagerOrder;
        protected override string SearchColumnHeader => "Manager";
        protected override void   Sync(ManagerOrderConfig config) => ManagerOrderSync.Sync(config);
        protected override List<ManagerOrderEntry> GetEntries(ManagerOrderConfig config) => config.Entries;
    }
}
