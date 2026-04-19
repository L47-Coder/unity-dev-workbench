# Dev Workbench

A Unity runtime **Manager / Component** framework powered by `ScriptableObject` +
`Addressables`, shipped with an in-editor **DevWorkbench** panel that provides
one-click creation and visual management of Managers, Components and
Addressable groups.

> Languages: **English** · [简体中文](./README.zh-CN.md)

> Status: **0.1.0-preview.1** &mdash; the public API may still change before `1.0`.

## Highlights

- **Opinionated runtime core.** A tiny `BaseManager` / `BaseComponent` pair
  standardises data loading, lifecycle and dependency injection, so gameplay
  code only ever sees well-typed, ready-to-use services.
- **Two-layer separation.** The architecture layer (`DevWorkbench`) owns
  lifecycle and contracts; the dispatch layer (`Game.Managers`) lives in your
  project under `Assets/Game/Manager/` and is free to be modified, extended or
  replaced.
- **DevWorkbench editor panel.** Tools&nbsp;&rarr;&nbsp;DevWorkbench gives you
  tree / table / order views for every Manager, Component and Addressable
  group, plus a Creator that scaffolds a new Manager (`.cs` + generated
  `Data` / `Config` partials + `ScriptableObject` asset + Addressable entry)
  with a single click.
- **Default Managers ship as templates, not binaries.** The bundled `Asset /
  Component / Prefab` Managers are installed on first load into
  `Assets/Game/Manager/` as plain source &mdash; you can read them, tweak them,
  or delete them.

## Requirements

- Unity **2022.3 LTS** or newer
- The following UPM dependencies (declared in `package.json`):
  - `com.unity.addressables` 1.23.1
  - `com.cysharp.unitask` 2.5.10
  - `jp.hadashikick.vcontainer` 1.17.0

UniTask and VContainer are usually consumed via their official Git URLs. The
version numbers in `package.json` are used by UPM for compatibility hints only
&mdash; the actual entry points belong in your host project's
`Packages/manifest.json`.

## Installation

### UPM &mdash; Git URL (recommended)

Open `Window&nbsp;&rarr;&nbsp;Package Manager`, click `+&nbsp;&rarr;&nbsp;Add
package from git URL&hellip;` and paste:

```
https://github.com/L47-Coder/unity-dev-workbench.git?path=Packages/com.l47coder.dev-workbench
```

Or add the following entry to `Packages/manifest.json`:

```json
"com.l47coder.dev-workbench": "https://github.com/L47-Coder/unity-dev-workbench.git?path=Packages/com.l47coder.dev-workbench"
```

> The repository is an entire Unity project, so the `?path=` segment is
> **required** &mdash; it tells UPM which sub-directory actually contains
> `package.json`.

### UPM &mdash; local path

Clone / copy this package into your project's `Packages/` folder. Unity will
pick it up automatically as an embedded package.

## Quick Start

1. Install the package.
2. Open `Tools&nbsp;&rarr;&nbsp;DevWorkbench`. On first launch the workbench
   creates:
   - `Assets/Game/Frame/{ManagerOrder,ComponentOrder,PageOrder}.asset`
   - `Assets/Game/Manager/{Asset,Component,Prefab}/` with the three default
     Managers and a shared `Game.Managers.asmdef`.
3. If the side panel says *"Framework not initialised"*, click **Initialise**.
4. Use the *Manager&nbsp;/&nbsp;Creator* page to scaffold your own Manager, or
   the *Component&nbsp;/&nbsp;Creator* page for a Component.

## Host Project Layout

Dev Workbench follows a **fixed path convention**. All generated files land
under `Assets/Game/`:

```
Assets/
└── Game/
    ├── Frame/
    │   ├── ManagerOrder.asset
    │   ├── ComponentOrder.asset
    │   └── PageOrder.asset
    └── Manager/
        ├── Game.Managers.asmdef
        ├── Asset/    AssetManagerConfig.asset    + source
        ├── Component/ ComponentManagerConfig.asset + source
        └── Prefab/   PrefabManagerConfig.asset   + source
```

The `Game.Managers` assembly has an `[InternalsVisibleTo]` bridge into
`DevWorkbench`, so user-written Managers can access the framework's internal
dispatch hooks (e.g. `BaseComponent.InternalSetGameObject`).

## Assembly & Namespace Layout

| Assembly (`.asmdef`)   | Namespace             | Purpose                                            |
| ---------------------- | --------------------- | -------------------------------------------------- |
| `DevWorkbench`         | `DevWorkbench`        | Runtime contracts, bridges, loader utilities.      |
| `DevWorkbench.Editor`  | `DevWorkbench.Editor` | Editor-only DevWorkbench panel and bootstrapper.   |
| `Game.Managers`        | *global*              | Host-project Managers (default + user-authored).   |

The Manager layer intentionally stays in the global namespace so that
scaffolded templates read naturally and gameplay code does not need an extra
`using`.

## License

Released under the [MIT License](./LICENSE.md).

Third-party dependency licenses are tracked in
[`Third Party Notices.md`](./Third%20Party%20Notices.md).
