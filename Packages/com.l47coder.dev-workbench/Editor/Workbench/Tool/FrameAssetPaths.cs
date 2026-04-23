namespace DevWorkbench.Editor
{
    // Frame 下三份 SO 的约定路径——Guard 和各 Page 模块共用此真相源。
    //
    // 放在 Tool/ 而不是 Frame/ 的原因：Guard 在"完整性兜底"阶段就要先看到这些常量
    // 来建资产，此时 DevWindow 还没打开。Tool 层是底层、Frame 层是 UI 入口，
    // 让 Tool 提供路径、Frame/Page 按需消费，避免反向依赖。
    //
    // ManagerOrder / ComponentOrder 走 Addressables "Frame" 组；PageOrder 是
    // editor-only 偏好，不挂 Addressables，只供 DevWindow 读。
    internal static class FrameAssetPaths
    {
        public const string Root = "Assets/Game/Frame";

        public const string ManagerOrder = Root + "/ManagerOrder.asset";
        public const string ComponentOrder = Root + "/ComponentOrder.asset";
        public const string PageOrder = Root + "/PageOrder.asset";
    }
}
