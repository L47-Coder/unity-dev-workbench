# Changelog

All notable changes to this package will be documented in this file.

The format is loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0-preview.1] &mdash; 2026-04-19

First public preview release.

### Added

- Runtime architecture layer (`DevWorkbench` assembly / namespace):
  - `BaseManagerConfig`, `BaseManagerData`, `BaseManager`, `IGameBoot`,
    `IAsyncInitManager`, `IManagerRefresher` &mdash; the Manager contract.
  - `BaseComponentConfig`, `BaseComponentData`, `BaseComponent` &mdash; the
    Component contract, with an `OnAdd / OnEnable / OnUpdate / OnDisable /
    OnRemove` lifecycle driven by the owning Manager.
  - `ComponentBridge` &mdash; cooperative lease broker so several Components
    can share built-in Unity components on one `GameObject` without stepping
    on each other.
  - `PhysicsBridge` with `IOnTrigger` / `IOnCollision` &mdash; forwards Unity
    physics callbacks as plain C# events, keeping Components out of the
    `MonoBehaviour` hierarchy.
  - `FrameworkLoader`, `ManagerAddressConvention`, `ManagerRefreshUtil` and
    the `TableColumnAttribute` utility.
  - `GameLifetimeScope` (VContainer) and `GameBootstrap` entry points that
    boot the framework in three phases: config load &rarr; async init &rarr;
    `IGameBoot.OnGameStart`.
  - `ManagerOrderConfig`, `ComponentOrderConfig` &mdash; ordered configuration
    ScriptableObjects. `ManagerOrderConfig` drives DI registration order;
    `ComponentOrderConfig` drives Component attach order inside Prefab
    instances and editor UI.
- Editor layer (`DevWorkbench.Editor` assembly / namespace):
  - `DevWindow` (`Tools&nbsp;&rarr;&nbsp;Dev&nbsp;Workbench`) with Manager,
    Component and Addressable pages, each exposing Viewer / Order / Creator /
    Installer tabs where applicable.
  - `PageOrder` ScriptableObject that persists the user-defined order of
    DevWindow menu groups and tabs.
  - One-click **framework bootstrap** (`FrameworkBootstrapper`) that
    provisions the three Order assets, both `Game.Managers.asmdef` /
    `Game.Components.asmdef` containers, and registers every discovered
    Manager / Component config as an Addressable entry.
  - Installer tabs that import the bundled Manager / Component templates on
    demand, reading manifests from the package's `Runtime~/Templates/`
    directory.
  - Creator tabs that scaffold a new Manager or Component as
    `.cs` + generated `Data` / `Config` partials + `ScriptableObject` asset +
    Addressable entry.
  - Reusable IMGUI controls: `TreeView`, `TableView`, `ListView`, `TextView`,
    shared `BoxDrawer` / `ControlsToolbar` infrastructure.
- Host project scaffolding:
  - First-time `Initialise` creates `Assets/Game/Frame/{ManagerOrder,
    ComponentOrder,PageOrder}.asset` and two empty assembly containers:
    `Assets/Game/Manager/Game.Managers.asmdef` and
    `Assets/Game/Component/Game.Components.asmdef`.
  - The default `AssetManager`, `ComponentManager` and `PrefabManager` are
    **not** pre-deployed &mdash; users import them on demand from the
    *Manager&nbsp;/&nbsp;Installer* tab. Once imported, they land in
    `Assets/Game/Manager/{Asset,Component,Prefab}/` as plain source.
- Package metadata: `LICENSE.md` (MIT), `Third Party Notices.md`, bilingual
  README (`README.md` / `README.zh-CN.md`).

### Notes

- All user-facing strings in the editor UI, the bootstrap overlay, dialogs,
  `Debug.Log*` messages and runtime Manager template exceptions are in
  English. Public API XML documentation on the reusable IMGUI controls
  (`TreeView`, `ListView`, `TableView`, `TextView`) is also English-only.
  Chinese is only kept in internal implementation comments that are not
  surfaced to the user.
