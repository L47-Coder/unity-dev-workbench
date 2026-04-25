using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    /// <summary>
    /// Project-specific Dev Workbench path overrides.
    /// <para>
    /// Modify <see cref="GameRootOverride"/> when your game code lives outside
    /// <c>Assets/Game</c> (e.g. the folder is renamed or this is a secondary project).
    /// Leave it empty to keep the built-in default <c>Assets/Game</c>.
    /// </para>
    /// <para>Asset is auto-created at <see cref="AssetPath"/> the first time the workbench opens.</para>
    /// </summary>
    public sealed class DevWorkbenchSettings : ScriptableObject
    {
        internal const string DefaultGameRoot = "Assets/Game";

        /// <summary>Fixed asset path so it can be found without knowing <see cref="GameRootOverride"/> yet.</summary>
        internal const string AssetPath = "Assets/DevWorkbenchSettings.asset";

        [Tooltip(
            "Root folder that contains Frame/, Manager/, Component/ sub-folders.\n" +
            "Leave empty to use the default 'Assets/Game'.")]
        [SerializeField] private string _gameRootOverride = string.Empty;

        /// <summary>
        /// Custom game root path set by the user, or <see langword="null"/> / empty if using the default.
        /// </summary>
        public string GameRootOverride => _gameRootOverride;

        /// <summary>
        /// Effective game root: <see cref="GameRootOverride"/> when non-empty, otherwise <see cref="DefaultGameRoot"/>.
        /// </summary>
        public string GameRoot =>
            string.IsNullOrWhiteSpace(_gameRootOverride)
                ? DefaultGameRoot
                : _gameRootOverride.TrimEnd('/');

        // ── Static access ─────────────────────────────────────────────────────────

        private static DevWorkbenchSettings _cache;

        /// <summary>
        /// Returns the loaded settings asset, or <see langword="null"/> if the asset does not exist yet.
        /// The result is cached until the next domain reload or Inspector change.
        /// </summary>
        internal static DevWorkbenchSettings Current
        {
            get
            {
                if (_cache != null) return _cache;
                _cache = AssetDatabase.LoadAssetAtPath<DevWorkbenchSettings>(AssetPath);
                return _cache;
            }
        }

        private void OnValidate() => _cache = null; // flush when Inspector edits the value
    }
}
