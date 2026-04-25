namespace DevWorkbench.Editor
{
    /// <summary>
    /// Well-known asset paths for the game's runtime code folders.
    /// All paths are derived from <see cref="DevWorkbenchSettings.GameRoot"/>,
    /// which defaults to <c>Assets/Game</c> and can be overridden in
    /// <c>Assets/DevWorkbenchSettings.asset</c>.
    /// </summary>
    internal static class GameProjectPaths
    {
        public const string AssetsRoot = "Assets";

        /// <summary>Root folder that contains Frame/, Manager/, Component/.</summary>
        public static string GameRoot =>
            DevWorkbenchSettings.Current?.GameRoot ?? DevWorkbenchSettings.DefaultGameRoot;

        public static string FrameRoot => GameRoot + "/Frame";
        public static string ManagerRoot => GameRoot + "/Manager";
        public static string ComponentRoot => GameRoot + "/Component";
    }
}
