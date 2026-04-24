namespace DevWorkbench.Editor
{
    /// <summary>
    /// Shortcut constants for the Frame folder's well-known assets. The root
    /// itself is owned by <see cref="GameProjectPaths"/>; this type only adds
    /// the per-asset paths so callers don't have to repeat the file names.
    /// </summary>
    internal static class GameFramePaths
    {
        public const string Root = GameProjectPaths.FrameRoot;
        public const string ManagerOrder = Root + "/ManagerOrder.asset";
        public const string ComponentOrder = Root + "/ComponentOrder.asset";
        public const string PageOrder = Root + "/PageOrder.asset";
    }
}
