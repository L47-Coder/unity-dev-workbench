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
`Tools → Dev Workbench`, the workbench scaffolds the default layout under
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

1. Open `Tools → Dev Workbench`. If the side panel says *"Framework not
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

## Release flow

Every release follows the same six-step ritual. The tag name is always
`v<version>`, taken verbatim from the value in `package.json` with a leading
`v` — e.g. `version: "0.2.0"` → tag `v0.2.0`, `version: "1.0.0-rc.1"` → tag
`v1.0.0-rc.1`.

1. **Make sure `main` is clean and up-to-date.**

   ```bash
   git checkout main
   git pull --ff-only
   git status  # should be clean
   ```

2. **Bump the package version.** Edit
   `Packages/com.l47coder.dev-workbench/package.json` and set `version` to the
   new value. Follow [SemVer](https://semver.org/):
   - `0.x` → public API may still break between minors
   - pre-releases use `-preview.N`, `-rc.N`, `-beta.N` suffixes
   - `1.0.0` is the first commitment to a stable public API

3. **Promote the CHANGELOG entry.** In
   `Packages/com.l47coder.dev-workbench/CHANGELOG.md`:
   - rename the top `## [Unreleased]` section to
     `## [<new-version>] — YYYY-MM-DD`
   - open a fresh empty `## [Unreleased]` above it so future PRs have somewhere
     to land

4. **Commit and push.**

   ```bash
   git add Packages/com.l47coder.dev-workbench/package.json \
           Packages/com.l47coder.dev-workbench/CHANGELOG.md
   git commit -m "chore(release): v<new-version>"
   git push origin main
   ```

5. **Create the tag.** Always use an annotated tag (not lightweight) so the
   author and date are recorded:

   ```bash
   git tag -a v<new-version> -m "Dev Workbench <new-version>"
   git push origin v<new-version>
   ```

   If you tagged the wrong commit *and* nobody has depended on it yet, delete
   both sides before re-tagging:

   ```bash
   git tag -d v<new-version>
   git push origin :refs/tags/v<new-version>
   ```

6. **Publish the GitHub Release.** Prefer the CLI:

   ```bash
   # Pre-release (preview / rc / beta) — keeps the "Latest" badge off
   gh release create v<new-version> \
     --title "v<new-version>" \
     --notes-file release-notes.md \
     --prerelease

   # Stable release
   gh release create v<new-version> \
     --title "v<new-version>" \
     --notes-file release-notes.md
   ```

   `release-notes.md` is a throwaway file holding the CHANGELOG section for
   this version. If you don't want to curate it, `--generate-notes` will build
   a PR-based summary automatically.

   Alternatively, open
   `https://github.com/L47-Coder/unity-dev-workbench/releases/new`, pick the
   existing tag, paste the CHANGELOG entry, tick **Set as a pre-release** for
   any `-preview` / `-rc` / `-beta` version, and publish.

### Post-release verification

- The tag appears at <https://github.com/L47-Coder/unity-dev-workbench/tags>.
- The release appears at <https://github.com/L47-Coder/unity-dev-workbench/releases>.
- UPM install with the tag works:

  ```
  https://github.com/L47-Coder/unity-dev-workbench.git?path=Packages/com.l47coder.dev-workbench#v<new-version>
  ```

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
`Tools → Dev Workbench` 时，工作台会把
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

1. 菜单栏 `Tools → Dev Workbench`，若侧栏提示 *"Framework not initialised"*，点击 **Initialise**。
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

### 发版流程

每次发版都按同一套六步走。tag 名永远是 `v<version>`，也就是 `package.json`
里的 `version` 前面加个 `v`：`"0.2.0"` → `v0.2.0`，`"1.0.0-rc.1"` →
`v1.0.0-rc.1`。

1. **保证 `main` 是干净的。**

   ```bash
   git checkout main
   git pull --ff-only
   git status
   ```

2. **改包版本号。** 编辑
   `Packages/com.l47coder.dev-workbench/package.json` 的 `version`。遵循
   [SemVer](https://semver.org/lang/zh-CN/)：
   - `0.x` 阶段，小版本之间允许破坏性变更
   - 预发布用 `-preview.N` / `-rc.N` / `-beta.N` 后缀
   - `1.0.0` 是对外正式承诺 API 稳定的第一版

3. **推进 CHANGELOG。** 在
   `Packages/com.l47coder.dev-workbench/CHANGELOG.md` 里：
   - 把顶部的 `## [Unreleased]` 改为 `## [<新版本号>] — YYYY-MM-DD`
   - 在它上面新开一个空的 `## [Unreleased]`，供下一批 PR 落脚

4. **提交并推送。**

   ```bash
   git add Packages/com.l47coder.dev-workbench/package.json \
           Packages/com.l47coder.dev-workbench/CHANGELOG.md
   git commit -m "chore(release): v<新版本号>"
   git push origin main
   ```

5. **打 tag。** 一律使用 annotated tag（不要用 lightweight tag）：

   ```bash
   git tag -a v<新版本号> -m "Dev Workbench <新版本号>"
   git push origin v<新版本号>
   ```

   如果打错了 tag 且**还没有人依赖它**，可以先删再重打：

   ```bash
   git tag -d v<新版本号>
   git push origin :refs/tags/v<新版本号>
   ```

6. **在 GitHub 上发 Release。** 推荐 CLI：

   ```bash
   # 预发布版本(preview / rc / beta)，保持侧栏 "Latest" 不被占用
   gh release create v<新版本号> \
     --title "v<新版本号>" \
     --notes-file release-notes.md \
     --prerelease

   # 正式版本
   gh release create v<新版本号> \
     --title "v<新版本号>" \
     --notes-file release-notes.md
   ```

   `release-notes.md` 是一个临时文件，内容就是 CHANGELOG 里本版本那一节。懒得
   写可以用 `--generate-notes` 让 GitHub 按 PR 自动生成。

   也可以走网页：`https://github.com/L47-Coder/unity-dev-workbench/releases/new`，
   选上一步推上去的 tag，粘贴 CHANGELOG 那一节，`-preview` / `-rc` / `-beta`
   版务必勾 **Set as a pre-release**，然后 Publish。

#### 发版后的自检

- tag 出现在 <https://github.com/L47-Coder/unity-dev-workbench/tags>
- Release 出现在 <https://github.com/L47-Coder/unity-dev-workbench/releases>
- 用 tag 锁版本装包可用：

  ```
  https://github.com/L47-Coder/unity-dev-workbench.git?path=Packages/com.l47coder.dev-workbench#v<新版本号>
  ```

### Pull Request

1. 较大改动先开 issue 对齐范围。
2. 编辑器无新增警告、相关流程手测通过。
3. 按 PR 模板填写 *Scope* 和 *Reproduction / test notes*。

默认按 **Squash merge** 合并，分支上的中间提交不必打磨。

### 许可证

提交代码即视为同意以 [MIT 许可证](./LICENSE) 发布。
