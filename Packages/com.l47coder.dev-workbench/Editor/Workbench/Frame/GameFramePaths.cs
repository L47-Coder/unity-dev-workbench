namespace DevWorkbench.Editor
{
    // Frame 下三份 SO 的约定路径——Guard 与各 Page 模块的真相源。
    // ManagerOrder / ComponentOrder 挂 Addressables "Frame" 组；PageOrder editor-only。
    // 宿主工程 Assets/Game/Frame/ 下的资产路径常量（区别于本包 Editor/Workbench/Frame/
    // 的 DevWindow 窗体骨架）。命名里的 "GameFrame" 指用户项目的 game-level Frame 层。
    internal static class GameFramePaths
    {
        public const string Root = "Assets/Game/Frame";
        public const string ManagerOrder = Root + "/ManagerOrder.asset";
        public const string ComponentOrder = Root + "/ComponentOrder.asset";
        public const string PageOrder = Root + "/PageOrder.asset";
    }
}
