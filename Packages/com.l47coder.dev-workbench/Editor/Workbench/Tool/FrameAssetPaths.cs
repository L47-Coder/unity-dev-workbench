namespace DevWorkbench.Editor
{
    // Frame 下三份 SO 的约定路径——Guard 与各 Page 模块的真相源。
    // ManagerOrder / ComponentOrder 挂 Addressables "Frame" 组；PageOrder editor-only。
    internal static class FrameAssetPaths
    {
        public const string Root = "Assets/Game/Frame";

        public const string ManagerOrder = Root + "/ManagerOrder.asset";
        public const string ComponentOrder = Root + "/ComponentOrder.asset";
        public const string PageOrder = Root + "/PageOrder.asset";
    }
}
