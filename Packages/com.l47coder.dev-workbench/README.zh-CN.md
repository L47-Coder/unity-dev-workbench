# Dev Workbench

以 `ScriptableObject` + `Addressables` 为核心的 Unity 运行期 **Manager / Component**
框架，内置 **DevWorkbench** 编辑器面板，一键完成 Manager / Component / Addressable
分组的创建与可视化管理。

> 语言：[English](./README.md) · **简体中文**

> 版本：**0.1.0-preview.1** &mdash; 公共 API 在 `1.0` 前仍可能调整。

## 特性

- **约束清晰的运行期核心**：`BaseManager` / `BaseComponent` 一对基类统一了
  数据装载、生命周期与依赖注入，业务代码只看到强类型、开箱即用的服务。
- **两层结构**：架构层（`DevWorkbench`）只管生命周期与契约；调度层
  （`Game.Managers`）住在宿主工程的 `Assets/Game/Manager/` 下，随时可改、可扩、
  可删。
- **DevWorkbench 编辑器面板**：`Tools&nbsp;&rarr;&nbsp;DevWorkbench` 提供树 /
  表 / 顺序三种视图管理 Manager、Component、Addressable 分组；Creator 页面一键
  生成 Manager（`.cs` + `Data` / `Config` partial + `ScriptableObject` 资产 +
  Addressable 入口）。
- **默认 Manager 以模板形式投放**：内置的 `Asset / Component / Prefab` 三个
  默认 Manager，首次加载时会被拷贝成源码投放到 `Assets/Game/Manager/`，
  用户可自由查看、修改甚至删除。

## 先决条件

- Unity **2022.3 LTS** 及以上
- 以下 UPM 依赖（声明在 `package.json`）：
  - `com.unity.addressables` 1.23.1
  - `com.cysharp.unitask` 2.5.10
  - `jp.hadashikick.vcontainer` 1.17.0

UniTask、VContainer 通常通过各自的官方 Git URL 引入。`package.json` 中的版本号
仅供 UPM 做兼容性提示，实际接入请在宿主工程的 `Packages/manifest.json` 中
配置。

## 安装

### UPM &mdash; Git URL（推荐）

打开 `Window&nbsp;&rarr;&nbsp;Package Manager`，点 `+&nbsp;&rarr;&nbsp;Add
package from git URL&hellip;`，粘贴：

```
https://github.com/L47-Coder/unity-dev-workbench.git
```

或直接编辑 `Packages/manifest.json`：

```json
"com.l47coder.dev-workbench": "https://github.com/L47-Coder/unity-dev-workbench.git"
```

### UPM &mdash; 本地路径

将本仓库克隆 / 复制到工程的 `Packages/` 下，Unity 会自动识别为 embedded 包。

## 快速上手

1. 安装本包。
2. 打开 `Tools&nbsp;&rarr;&nbsp;DevWorkbench`。首次打开时会自动创建：
   - `Assets/Game/Frame/{ManagerOrder,ComponentOrder,PageOrder}.asset`
   - `Assets/Game/Manager/{Asset,Component,Prefab}/` 下的三个默认 Manager 与
     共用的 `Game.Managers.asmdef`。
3. 如果侧栏提示"架构未完成"，点击"一键完成"。
4. 在 *Manager&nbsp;/&nbsp;Creator* 页面创建自己的 Manager，或在
   *Component&nbsp;/&nbsp;Creator* 页面创建 Component。

## 宿主工程目录约定

Dev Workbench 采用**固定路径约定**，所有生成物统一落在 `Assets/Game/` 下：

```
Assets/
└── Game/
    ├── Frame/
    │   ├── ManagerOrder.asset
    │   ├── ComponentOrder.asset
    │   └── PageOrder.asset
    └── Manager/
        ├── Game.Managers.asmdef
        ├── Asset/     AssetManagerConfig.asset     + 源码
        ├── Component/ ComponentManagerConfig.asset + 源码
        └── Prefab/    PrefabManagerConfig.asset    + 源码
```

`Game.Managers` 程序集通过 `[InternalsVisibleTo]` 桥接到 `DevWorkbench`，
使用户自定 Manager 能访问框架的内部调度钩子（如
`BaseComponent.InternalSetGameObject`）。

## 程序集与命名空间

| 程序集（`.asmdef`）    | 命名空间              | 职责                                               |
| --------------------- | -------------------- | -------------------------------------------------- |
| `DevWorkbench`        | `DevWorkbench`       | 运行期契约、桥接器、加载工具。                      |
| `DevWorkbench.Editor` | `DevWorkbench.Editor`| 编辑器专用的 DevWorkbench 面板与 Bootstrap。        |
| `Game.Managers`       | *全局*               | 宿主工程的 Manager（默认 + 用户自定）。             |

Manager 层特意保持在全局命名空间，使生成的模板读起来更自然，业务代码也无需
额外 `using`。

## 许可证

采用 [MIT 许可证](./LICENSE.md) 发布。

第三方依赖的许可证汇总见
[`Third Party Notices.md`](./Third%20Party%20Notices.md)。
