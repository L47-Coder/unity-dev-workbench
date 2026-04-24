namespace DevWorkbench.Editor
{
    /// <summary>
    /// Single source of truth for the in-project folders the Dev Workbench
    /// provisions and operates on (the <c>Assets/Game</c> tree). Keeping these
    /// centralized removes the dozens of string literals that previously lived
    /// in Creators / Installers / Viewers, and gives us one place to swap in a
    /// user-authored override (e.g. a <c>DevWorkbenchSettings</c> asset) in a
    /// follow-up refactor.
    /// </summary>
    internal static class GameProjectPaths
    {
        public const string AssetsRoot = "Assets";

        /// <summary>Root of the user's game code and assets.</summary>
        public const string GameRoot = AssetsRoot + "/Game";

        /// <summary>Frame folder that owns the order assets and <c>GameBoot.cs</c>.</summary>
        public const string FrameRoot = GameRoot + "/Frame";

        /// <summary>Root folder under which concrete Managers are scaffolded.</summary>
        public const string ManagerRoot = GameRoot + "/Manager";

        /// <summary>Root folder under which concrete Components are scaffolded.</summary>
        public const string ComponentRoot = GameRoot + "/Component";
    }
}
