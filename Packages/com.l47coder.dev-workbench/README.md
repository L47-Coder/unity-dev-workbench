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
  lifecycle and contracts; the dispatch layer (`Game.Managers` /
  `Game.Components`) lives in your project under `Assets/Game/` and is free
  to be modified, extended or replaced.
- **DevWorkbench editor panel.** Tools&nbsp;&rarr;&nbsp;Dev&nbsp;Workbench gives you
  Viewer / Order / Creator / Installer tabs for every Manager, Component and
  Addressable group. The Creator scaffolds a new Manager or Component
  (`.cs` + generated `Data` / `Config` partials + `ScriptableObject` asset +
  Addressable entry) with a single click.
- **Default Managers ship as on-demand templates.** On first launch the
  workbench only provisions the Order assets and two empty `asmdef`
  containers. The bundled `Asset / Component / Prefab` Managers are imported
  from the *Manager&nbsp;/&nbsp;Installer* tab as plain source you own and can
  modify or delete.

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
2. Open `Tools&nbsp;&rarr;&nbsp;Dev&nbsp;Workbench`. If the side panel says
   *"Framework not initialised"*, click **Initialise**. This creates:
   - `Assets/Game/Frame/{ManagerOrder,ComponentOrder,PageOrder}.asset`
   - `Assets/Game/Manager/Game.Managers.asmdef` and
     `Assets/Game/Component/Game.Components.asmdef` as empty containers.
3. Open the *Manager&nbsp;/&nbsp;Installer* tab and import the default
   `Asset / Component / Prefab` Managers on demand. They land under
   `Assets/Game/Manager/{Asset,Component,Prefab}/` as plain source you can
   modify or delete.
4. Use the *Manager&nbsp;/&nbsp;Creator* or *Component&nbsp;/&nbsp;Creator*
   tab to scaffold your own Manager / Component.

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
    ├── Manager/
    │   ├── Game.Managers.asmdef
    │   ├── Asset/      (installed on demand)
    │   ├── Component/  (installed on demand)
    │   └── Prefab/     (installed on demand)
    └── Component/
        └── Game.Components.asmdef
```

`Game.Managers` and `Game.Components` both have an `[InternalsVisibleTo]`
bridge into `DevWorkbench`, so user-written Managers and Components can access
the framework's internal dispatch hooks (e.g.
`BaseComponent.InternalSetGameObject`).

## Assembly & Namespace Layout

| Assembly (`.asmdef`)   | Namespace             | Purpose                                                  |
| ---------------------- | --------------------- | -------------------------------------------------------- |
| `DevWorkbench`         | `DevWorkbench`        | Runtime contracts, bridges, loader utilities.            |
| `DevWorkbench.Editor`  | `DevWorkbench.Editor` | Editor-only DevWorkbench panel and bootstrapper.         |
| `Game.Managers`        | *global*              | Host-project Managers (default + user-authored).         |
| `Game.Components`      | *global*              | Host-project Components, scaffolded by the Creator.      |

The Manager / Component layers intentionally stay in the global namespace so
that scaffolded templates read naturally and gameplay code does not need an
extra `using`.

## License

Released under the [MIT License](./LICENSE.md).

Third-party dependency licenses are tracked in
[`Third Party Notices.md`](./Third%20Party%20Notices.md).
