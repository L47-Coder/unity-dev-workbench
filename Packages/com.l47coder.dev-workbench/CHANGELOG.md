# Changelog

All notable changes to this package will be documented in this file.

The format is loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0-preview.3] &mdash; 2026-04-23

Third preview. Introduces a dedicated `Game.Frame` assembly as the top-most
host-project layer, reorganises the editor source tree around per-page
`Installer / Order / Viewer` folders, and replaces the old *Sync Runtime*
menu entry with a first-class **Framework / Sync** page.

### Added

- **`Game.Frame` assembly + relocated `GameBoot.cs`.** The first-time
  bootstrap now provisions `Assets/Game/Frame/Game.Frame.asmdef` and moves
  `GameBoot.cs` out of `Assets/Game/Manager/` into `Assets/Game/Frame/`.
  `Game.Frame` references both `Game.Managers` and `Game.Components` and is
  the intended home for app-level glue (boot, scene wiring, cross-cutting
  concerns). Existing projects with `Assets/Game/Manager/GameBoot.cs` are
  auto-migrated via `AssetDatabase.MoveAsset`, preserving the original GUID.
- **Framework / Sync page.** A new `Framework` group in the Dev Workbench
  window exposes the old *Sync Runtime* action as a prominent button plus
  three auto-trigger options (`Manual only` / `When Dev Workbench closes`
  / `Before entering Play Mode`). The choice is stored per-developer in
  `EditorPrefs` &mdash; it is not committed to the project.
- **`ProjectFilesAutoSync` (`AssetPostprocessor`).** After any `.cs` /
  `.asmdef` / `.asmref` / `.rsp` import, Unity now auto-invokes
  `CodeEditor.CurrentEditor.SyncAll()`. This keeps external IDEs such as
  Cursor / VS Code reading up-to-date `.csproj` files, eliminating spurious
  "type or namespace not found" errors right after scaffolding a new
  Manager / Component / asmdef.
- **`IGameBoot` discovery is now interface-based.** `GameBootstrap` scans
  the scene for any `MonoBehaviour` implementing `IGameBoot` instead of
  resolving a fixed type. The concrete `GameBoot` class is free to live in
  any user assembly (the default template lives in `Game.Frame`).
- **`DevWindowFrameworkGuard`.** A single `[InitializeOnLoad]` gatekeeper
  replaces the previous `FrameworkBootstrapper` / `FrameAssetInstaller`
  pair. It owns the three-phase ensure flow (skeleton copy &rarr;
  Addressables / Order assets &rarr; `IPage.OnWorkbenchOpen` fan-out) and
  survives domain reloads via `SessionState`.

### Changed

- **Editor source tree reorganised.** `Editor/Workbench/Bootstrap/` has been
  retired. Its responsibilities are now split into:
  - `Editor/Workbench/Tool/` &mdash; framework-wide helpers
    (`DevWindowFrameworkGuard`, `AssetFolderCopier`, `AssetPathUtil`,
    `FrameAssetPaths`, `ProjectFilesAutoSync`, ...).
  - `Editor/Workbench/Page/<Group>/<Tab>/` &mdash; per-tab files colocated
    with the page itself. For example the Manager-template installer now
    lives in `Page/Manager/Installer/ManagerTemplateInstaller.cs` alongside
    `ManagerInstallerPage.cs`.
  - `Editor/Workbench/Frame/` keeps `DevWindow`, `PageOrder` and other
    window-chrome code.
- **`GameBootstrap` boot sequence documented** and hardened: `ResolveGameBoot`
  now emits explicit warnings when zero or multiple `IGameBoot` behaviours
  are found in the scene.
- **Default Manager templates** (`AssetManager`, `ComponentManager`,
  `PrefabManager`) gained XML summaries on their public surface and tidied
  their `OnDisable` / teardown ordering so Components receive
  `InternalSetEnabled(false)` before `InternalOnRemove()` in every path.

### Removed

- **`Tools / Dev Workbench / Sync Runtime` menu item.** Its behaviour moved
  into the new *Framework / Sync* page, so the workbench window is now the
  single entry point for that action.
- **`FrameworkBootstrapper`, `FrameAssetInstaller`, `FrameTemplateInstaller`,
  `WorkbenchPageRunner`.** Their logic was consolidated into
  `DevWindowFrameworkGuard` + the per-page installers.
- **`README.zh-CN.md`** was removed to keep a single canonical English README.

### Fixed

- **`PrefabManager.DestroyPoolAsync` / `OnDisable` ordering.** Active
  instances now disable their Components (reverse order) before the
  subsequent remove-pass, matching the lifecycle contract used elsewhere
  in the framework.
- **`ManagerRefreshUtil.Sync` no longer throws on blank keys.** List entries
  whose key is `null` / empty / whitespace (typically a half-filled row left
  in the Inspector) are now treated as stale and removed, instead of raising
  `ArgumentNullException` from `Dictionary.ContainsKey(null)` and aborting
  the whole refresh. All three built-in Refreshers (Asset / Component /
  Prefab) and any user-authored Refresher that routes through `Sync` benefit
  automatically.
- **Assembly-definition regressions** in `Game.Frame` / `DevWorkbench.Editor`
  that caused stale csproj references after the tree reshuffle.

## [0.1.0-preview.2] &mdash; 2026-04-20

Second preview. Focuses on making default Managers optional, clarifying the
framework boot sequence, and aligning documentation with the current editor
flow.

### Changed

- **Default Managers are now on-demand templates.** The first-time
  `Initialise` only provisions `Assets/Game/Frame/{ManagerOrder,ComponentOrder,
  PageOrder}.asset` and two empty assembly containers &mdash;
  `Assets/Game/Manager/Game.Managers.asmdef` and
  `Assets/Game/Component/Game.Components.asmdef`. The bundled `Asset /
  Component / Prefab` Managers are imported on demand from the
  *Manager&nbsp;/&nbsp;Installer* tab and land in
  `Assets/Game/Manager/{Asset,Component,Prefab}/` as plain source.
- `GameBootstrap` gained a class-level XML doc describing its three-phase
  sequence (config load &rarr; async init &rarr; `IGameBoot.OnGameStart`),
  and dropped a stray startup `Debug.Log` that printed every Manager's type
  name.
- README: Highlights, Quick Start, Host Project Layout and the Assembly
  table updated to reflect the Installer-driven flow and the new
  `Game.Components` assembly.

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
- Package metadata: `LICENSE.md` (MIT), `Third Party Notices.md`, `README.md`.

### Notes

- All user-facing strings in the editor UI, the bootstrap overlay, dialogs,
  `Debug.Log*` messages and runtime Manager template exceptions are in
  English. Public API XML documentation on the reusable IMGUI controls
  (`TreeView`, `ListView`, `TableView`, `TextView`) is also English-only.
  Chinese is only kept in internal implementation comments that are not
  surfaced to the user.
