# Contributing to Dev Workbench

Thanks for your interest in contributing! This document explains how the
repository is laid out, how to get a working development environment, and the
conventions we follow for commits, versioning and pull requests.

> Languages: **English** · [简体中文](#简体中文)

## Repository model

This repository is **both** a Unity host project **and** the source of the
`com.l47coder.dev-workbench` package. The package lives at
`Packages/com.l47coder.dev-workbench/` as an *embedded* UPM package, which
means changes to the package source are picked up by Unity immediately — no
re-import or re-install step is required.

```
Packages/
├── com.l47coder.dev-workbench/   <- the package (edit here)
└── manifest.json                  <- host project's dependencies
```

Everything under `Assets/` is the host project: it exists so maintainers have
something to click through while iterating on the package. On first launch of
`Tools → DevWorkbench`, the workbench scaffolds the default layout under
`Assets/Game/` from the templates in `Packages/com.l47coder.dev-workbench/Runtime~/DefaultManagers/`.

## Prerequisites

- **Unity 2022.3 LTS** (developed against `2022.3.60f1`).
- Git ≥ 2.30 and, if you intend to touch binary art, **Git LFS** — run
  `git lfs install` once per machine.
- Optional: JetBrains Rider or Visual Studio (OmniSharp also works).

## Getting started

```bash
git clone https://github.com/L47-Coder/unity-dev-workbench.git
cd unity-dev-workbench
```

Open the folder with Unity Hub as a normal project. Unity will resolve UPM
dependencies (Addressables, UniTask, VContainer) on first open.

Then:

1. Open `Tools → DevWorkbench`. If the side panel says *"Framework not
   initialised"*, click **Initialise** — this creates the three order assets
   and deploys the default Managers under `Assets/Game/`.
2. Make your changes under `Packages/com.l47coder.dev-workbench/`.
3. Exercise the change through the workbench windows or a scratch scene.

## Coding conventions

- Respect `.editorconfig`: UTF-8, LF line endings, 4-space indent for C#,
  2-space indent for YAML / JSON / UXML / USS, trailing whitespace trimmed.
- `.gitattributes` is the source of truth for Unity YAML merging and LFS
  tracking. Don't commit binary assets without first running
  `git lfs install`.
- Runtime contracts live under `Packages/com.l47coder.dev-workbench/Runtime/`
  and must stay inside the `DevWorkbench` namespace. Editor-only code belongs
  in `Packages/com.l47coder.dev-workbench/Editor/` and uses the
  `DevWorkbench.Editor` namespace.
- Default Manager templates (`Runtime~/DefaultManagers/`) are **source** that
  ships to the user's `Assets/Game/Manager/` on first boot — treat them as
  public API.
- Keep user-visible behavioural changes small and focused; a PR that mixes
  refactor + feature + doc update is hard to review.

## Commit & branch conventions

- Work on a feature branch (`feat/…`, `fix/…`, `docs/…`, `chore/…`).
- Commit messages follow a lightweight
  [Conventional Commits](https://www.conventionalcommits.org/) subset:
  - `feat: …` new functionality
  - `fix: …` bug fix
  - `docs: …` documentation only
  - `refactor: …` no user-visible change
  - `chore: …` tooling / build / CI / meta
  - `test: …` tests only
- Keep commits green — a WIP push that intentionally fails to compile should
  be squashed before review.

## Changelog & versioning

The package follows [Semantic Versioning](https://semver.org/) and
[Keep a Changelog](https://keepachangelog.com/).

- For any user-visible change, append an entry under an `## [Unreleased]`
  heading in [`Packages/com.l47coder.dev-workbench/CHANGELOG.md`](./Packages/com.l47coder.dev-workbench/CHANGELOG.md).
- Releases are cut by bumping `version` in
  [`Packages/com.l47coder.dev-workbench/package.json`](./Packages/com.l47coder.dev-workbench/package.json),
  promoting the `Unreleased` section to the new version heading, and tagging
  the commit as `v<version>` (e.g. `v0.2.0`).

## Pull requests

1. Open an issue first for non-trivial changes so we can agree on scope.
2. Make sure the editor compiles without new warnings.
3. Run through the flows you touched (Creator, Viewer, Order panels, etc.).
4. Fill in the PR template — especially the *Scope* and *Reproduction / test
   notes* sections.

We squash-merge PRs by default; your commit history on the branch does not
need to be pristine.

## Reporting bugs

Use the **Bug report** issue template. Please include:

- Package version (from `package.json`).
- Unity version and editor platform.
- Minimal steps to reproduce and, when possible, the relevant console output.

## License

By contributing, you agree that your contributions will be licensed under the
[MIT License](./LICENSE).

---

## 简体中文

感谢你愿意为 Dev Workbench 贡献代码！本文说明仓库结构、本地开发流程、
以及提交 / 版本号 / PR 的约定。

### 仓库结构

本仓库**同时**是一个 Unity 宿主工程和包 `com.l47coder.dev-workbench` 的源码。
包以 embedded UPM 包形式位于 `Packages/com.l47coder.dev-workbench/`，改动
Unity 会即时识别，不需要重新安装。

`Assets/` 下是宿主工程，用于维护者日常点测。首次打开
`Tools → DevWorkbench` 时，工作台会把
`Packages/com.l47coder.dev-workbench/Runtime~/DefaultManagers/` 下的模板
部署到 `Assets/Game/`。

### 环境要求

- **Unity 2022.3 LTS**（开发用 `2022.3.60f1`）。
- Git ≥ 2.30；若要改动美术类二进制资源，请先 `git lfs install`。
- 可选：Rider 或 Visual Studio。

### 上手

```bash
git clone https://github.com/L47-Coder/unity-dev-workbench.git
```

用 Unity Hub 打开根目录。打开后：

1. 菜单栏 `Tools → DevWorkbench`，若提示"架构未完成"，点击"一键完成"。
2. 在 `Packages/com.l47coder.dev-workbench/` 下进行改动。
3. 通过工作台面板或测试场景走一遍修改影响的流程。

### 代码规范

- 遵循 `.editorconfig`：UTF-8、LF、C# 4 空格缩进、YAML / JSON / UXML / USS
  2 空格缩进、去除行尾空格。
- `.gitattributes` 是 Unity YAML merge 和 Git LFS 的唯一事实来源，提交二进制
  资源前请先 `git lfs install`。
- 运行期契约放在 `Runtime/`，命名空间 `DevWorkbench`；编辑器代码放在
  `Editor/`，命名空间 `DevWorkbench.Editor`。
- `Runtime~/DefaultManagers/` 里的默认 Manager 模板会被复制到用户工程的
  `Assets/Game/Manager/`，请视为公共 API。
- 一次 PR 只做一件事，避免重构、特性、文档混在一起。

### 提交 & 分支约定

- 按 `feat/…`、`fix/…`、`docs/…`、`chore/…` 开功能分支。
- 提交信息使用
  [Conventional Commits](https://www.conventionalcommits.org/) 的精简子集：
  `feat / fix / docs / refactor / chore / test`。
- 提交前确保编辑器能编译通过。

### 变更日志与版本号

包遵循 [语义化版本](https://semver.org/lang/zh-CN/) 与
[Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。

- 任何用户可见的改动都要在
  [`Packages/com.l47coder.dev-workbench/CHANGELOG.md`](./Packages/com.l47coder.dev-workbench/CHANGELOG.md)
  的 `## [Unreleased]` 下追加条目。
- 发布时修改
  [`Packages/com.l47coder.dev-workbench/package.json`](./Packages/com.l47coder.dev-workbench/package.json)
  的 `version`，把 `Unreleased` 改为正式版本号，并打上 `v<version>` 的
  Git tag（例如 `v0.2.0`）。

### Pull Request

1. 较大改动先开 issue 对齐范围。
2. 编辑器无新增警告、相关流程手测通过。
3. 按 PR 模板填写 *Scope* 和 *Reproduction / test notes*。

默认按 **Squash merge** 合并，分支上的中间提交不必打磨。

### 许可证

提交代码即视为同意以 [MIT 许可证](./LICENSE) 发布。
