using System.Runtime.CompilerServices;

// The in-package editor assembly (DevWorkbench.Editor) needs access to Runtime
// internal types (e.g. *ManagerData, *ManagerConfig.EditorConfigs), so we open
// InternalsVisibleTo for it here.
[assembly: InternalsVisibleTo("DevWorkbench.Editor")]

// The user project's Assets/Game/Manager/ Game.Managers asmdef hosts all
// Manager code (the default AssetManager / ComponentManager / PrefabManager,
// plus any Manager generated via the Creator). Managers live in the
// "dispatch layer" and are allowed to touch internal APIs such as the
// BaseComponent lifecycle back door.
[assembly: InternalsVisibleTo("Game.Managers")]
