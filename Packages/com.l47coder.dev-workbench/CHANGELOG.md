# Changelog

All notable changes to this package will be documented in this file.

The format is loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0-preview.1] &mdash; 2026-04-19

First public preview release.

### Added

- Runtime architecture layer (`DevWorkbench` assembly / namespace):
  - `BaseManagerConfig`, `BaseManagerData`, `BaseManager`, `IAsyncInitManager`,
    `IManagerRefresher` &mdash; the Manager contract.
  - `BaseComponentConfig`, `BaseComponentData`, `BaseComponent` &mdash; the
    Component contract.
  - `ComponentBridge` / `PhysicsBridge` &mdash; MonoBehaviour bridges that
    forward Unity lifecycle and physics callbacks into Components without
    leaking Manager-layer concepts.
  - `FrameworkLoader` and `ManagerRefreshUtil` utilities.
  - `GameLifetimeScope` (VContainer) and `GameBootstrap` entry points.
  - `ManagerOrderConfig`, `ComponentOrderConfig` &mdash; ordered configuration
    ScriptableObjects.
- Editor layer (`DevWorkbench.Editor` assembly / namespace):
  - `DevWindow` (`Tools&nbsp;&rarr;&nbsp;Dev&nbsp;Workbench`) with Addressable,
    Component and Manager pages, each exposing Viewer / Order / Creator tabs.
  - One-click **framework bootstrap** that provisions the three order assets,
    the three default Manager configs and the matching Addressable entries.
  - Reusable IMGUI controls: `TreeView`, `TableView`, `ListView`, `TextView`
    and tool bars.
- Host project scaffolding (`Game.Managers` assembly):
  - Default `AssetManager`, `ComponentManager` and `PrefabManager`
    deployed on first load into `Assets/Game/Manager/` as plain source.
- Package metadata: `LICENSE.md` (MIT), `Third Party Notices.md`, bilingual
  README (`README.md` / `README.zh-CN.md`).

### Notes

- All user-facing strings in the editor UI, the bootstrap overlay, dialogs,
  `Debug.Log*` messages and runtime Manager template exceptions are in
  English. Public API XML documentation on the reusable IMGUI controls
  (`TreeView`, `ListView`, `TableView`, `TextView`) is also English-only.
  Chinese is only kept in internal implementation comments that are not
  surfaced to the user.
