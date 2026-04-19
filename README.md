# Dev Workbench

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)
[![Unity 2022.3](https://img.shields.io/badge/Unity-2022.3%20LTS-black.svg?logo=unity)](https://unity.com/releases/editor/whats-new/2022.3.60)
[![Package](https://img.shields.io/badge/UPM-com.l47coder.dev--workbench-1a7ad4.svg)](./Packages/com.l47coder.dev-workbench)

> Languages: **English** · [简体中文](#简体中文)

This repository hosts the source of the **Dev Workbench** Unity package
(`com.l47coder.dev-workbench`) together with a minimal Unity host project that
is used for developing and dogfooding the package.

- **Package source** — [`Packages/com.l47coder.dev-workbench/`](./Packages/com.l47coder.dev-workbench)
- **Package README** — [English](./Packages/com.l47coder.dev-workbench/README.md) · [简体中文](./Packages/com.l47coder.dev-workbench/README.zh-CN.md)
- **Changelog** — [`CHANGELOG.md`](./Packages/com.l47coder.dev-workbench/CHANGELOG.md)
- **License** — [MIT](./LICENSE)

Dev Workbench is a Unity **Manager / Component** framework powered by
`ScriptableObject` + `Addressables`, shipped with an in-editor **DevWorkbench**
panel (`Tools → Dev Workbench`) that provides one-click creation and visual
management of Managers, Components and Addressable groups.

## Repository layout

```
.
├── Assets/                              # Minimal host project (Unity 2022.3.60f1)
│   ├── Game/                            # Generated on first workbench launch
│   └── Scenes/SampleScene.unity
├── Packages/
│   ├── com.l47coder.dev-workbench/      # The package source (embedded)
│   └── manifest.json                    # Host project dependencies
├── ProjectSettings/                     # Unity project settings
├── .editorconfig
├── .gitattributes
├── .gitignore
├── CONTRIBUTING.md
├── LICENSE
└── README.md                            # (this file)
```

Because the package is kept as an **embedded** package (not a git submodule),
cloning this repository and opening it in Unity gives you a working editor
install — any change you make under `Packages/com.l47coder.dev-workbench/` is
picked up live.

## Install the package in your own project

Open `Window → Package Manager`, click `+ → Add package from git URL…` and
paste:

```
https://github.com/L47-Coder/unity-dev-workbench.git?path=Packages/com.l47coder.dev-workbench
```

Or edit your project's `Packages/manifest.json` directly:

```json
{
  "dependencies": {
    "com.l47coder.dev-workbench": "https://github.com/L47-Coder/unity-dev-workbench.git?path=Packages/com.l47coder.dev-workbench"
  }
}
```

The `?path=` segment is required because this repository is an entire Unity
project — without it, UPM would try to treat the repo root as the package.

See the package README for full usage details, quick-start and host-project
layout conventions.

## Requirements

- Unity **2022.3 LTS** or newer (developed against `2022.3.60f1`)
- UPM dependencies resolved automatically by `package.json`:
  - `com.unity.addressables` 1.23.1
  - `com.cysharp.unitask` 2.5.10
  - `jp.hadashikick.vcontainer` 1.17.0

## Development

Clone and open the repository as a regular Unity project:

```bash
git clone https://github.com/L47-Coder/unity-dev-workbench.git
```

Then open the folder in **Unity 2022.3 LTS**. The embedded package is live —
edit `Packages/com.l47coder.dev-workbench/…` and Unity will recompile.

Contribution guidelines live in [`CONTRIBUTING.md`](./CONTRIBUTING.md).

## License

Released under the [MIT License](./LICENSE). See
[`Third Party Notices.md`](./Packages/com.l47coder.dev-workbench/Third%20Party%20Notices.md)
for upstream dependency licenses.

---

## 简体中文

本仓库是 Unity 包 **Dev Workbench**（`com.l47coder.dev-workbench`）的源码仓库，
同时内置一个用于开发和自测的最小 Unity 宿主工程。

- **包源码**：[`Packages/com.l47coder.dev-workbench/`](./Packages/com.l47coder.dev-workbench)
- **包 README**：[English](./Packages/com.l47coder.dev-workbench/README.md) · [简体中文](./Packages/com.l47coder.dev-workbench/README.zh-CN.md)
- **变更日志**：[`CHANGELOG.md`](./Packages/com.l47coder.dev-workbench/CHANGELOG.md)
- **许可证**：[MIT](./LICENSE)

Dev Workbench 是一个以 `ScriptableObject` + `Addressables` 为核心的 Unity
运行期 **Manager / Component** 框架，内置 **DevWorkbench** 编辑器面板
（`Tools → Dev Workbench`），支持对 Manager、Component、Addressable 分组的
一键创建与可视化管理。

### 在自己的工程里安装

打开 `Window → Package Manager`，点击 `+ → Add package from git URL…`，粘贴：

```
https://github.com/L47-Coder/unity-dev-workbench.git?path=Packages/com.l47coder.dev-workbench
```

或直接在 `Packages/manifest.json` 里添加：

```json
{
  "dependencies": {
    "com.l47coder.dev-workbench": "https://github.com/L47-Coder/unity-dev-workbench.git?path=Packages/com.l47coder.dev-workbench"
  }
}
```

`?path=` 不能省：本仓库整体是一个 Unity 工程，不加 `?path=` UPM 会把整个仓库
当成包而装不上。

详细用法请看包内的 [中文 README](./Packages/com.l47coder.dev-workbench/README.zh-CN.md)。

### 本地开发

```bash
git clone https://github.com/L47-Coder/unity-dev-workbench.git
```

然后用 **Unity 2022.3 LTS** 打开根目录即可。包以 embedded 形式位于
`Packages/com.l47coder.dev-workbench/`，修改立即生效。

贡献指南见 [`CONTRIBUTING.md`](./CONTRIBUTING.md)。
