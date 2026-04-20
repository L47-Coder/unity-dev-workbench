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
  （`Game.Managers` / `Game.Components`）住在宿主工程的 `Assets/Game/` 下，
  随时可改、可扩、可删。
- **DevWorkbench 编辑器面板**：`Tools&nbsp;&rarr;&nbsp;Dev&nbsp;Workbench` 为
  Manager、Component、Addressable 分组各自提供 Viewer / Order / Creator /
  Installer 四个 Tab；Creator 一键生成 Manager 或 Component（`.cs` + `Data` /
  `Config` partial + `ScriptableObject` 资产 + Addressable 入口）。
- **默认 Manager 以按需模板形式投放**：首次初始化只铺 Order 资产和两个空的
  `asmdef` 容器；内置的 `Asset / Component / Prefab` 三个默认 Manager 由
  *Manager&nbsp;/&nbsp;Installer* 页面按需导入，落地后即用户自有的源码，可自由
  查看、修改或删除。

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
https://github.com/L47-Coder/unity-dev-workbench.git?path=Packages/com.l47coder.dev-workbench
```

或直接编辑 `Packages/manifest.json`：

```json
"com.l47coder.dev-workbench": "https://github.com/L47-Coder/unity-dev-workbench.git?path=Packages/com.l47coder.dev-workbench"
```

> 本仓库整体是一个 Unity 工程，因此 `?path=` 段**不能省略** &mdash;
> 它告诉 UPM 包的 `package.json` 在哪个子目录。

### UPM &mdash; 本地路径

将本仓库克隆 / 复制到工程的 `Packages/` 下，Unity 会自动识别为 embedded 包。

## 快速上手

1. 安装本包。
2. 打开 `Tools&nbsp;&rarr;&nbsp;Dev&nbsp;Workbench`。若侧栏提示
   *"Framework not initialised"*，点击 **Initialise**，此时会创建：
   - `Assets/Game/Frame/{ManagerOrder,ComponentOrder,PageOrder}.asset`
   - `Assets/Game/Manager/Game.Managers.asmdef` 与
     `Assets/Game/Component/Game.Components.asmdef` 两个空容器。
3. 打开 *Manager&nbsp;/&nbsp;Installer* 页面，按需导入内置的
   `Asset / Component / Prefab` 三个默认 Manager，它们会以源码形式落在
   `Assets/Game/Manager/{Asset,Component,Prefab}/`，用户可自由修改或删除。
4. 在 *Manager&nbsp;/&nbsp;Creator* 或 *Component&nbsp;/&nbsp;Creator*
   页面创建自己的 Manager / Component。

## 宿主工程目录约定

Dev Workbench 采用**固定路径约定**，所有生成物统一落在 `Assets/Game/` 下：

```
Assets/
└── Game/
    ├── Frame/
    │   ├── ManagerOrder.asset
    │   ├── ComponentOrder.asset
    │   └── PageOrder.asset
    ├── Manager/
    │   ├── Game.Managers.asmdef
    │   ├── Asset/      （按需导入）
    │   ├── Component/  （按需导入）
    │   └── Prefab/     （按需导入）
    └── Component/
        └── Game.Components.asmdef
```

`Game.Managers` 与 `Game.Components` 都通过 `[InternalsVisibleTo]` 桥接到
`DevWorkbench`，使用户自定 Manager 与 Component 能访问框架的内部调度钩子
（如 `BaseComponent.InternalSetGameObject`）。

## 程序集与命名空间

| 程序集（`.asmdef`）    | 命名空间              | 职责                                                  |
| --------------------- | -------------------- | ----------------------------------------------------- |
| `DevWorkbench`        | `DevWorkbench`       | 运行期契约、桥接器、加载工具。                          |
| `DevWorkbench.Editor` | `DevWorkbench.Editor`| 编辑器专用的 DevWorkbench 面板与 Bootstrap。            |
| `Game.Managers`       | *全局*               | 宿主工程的 Manager（默认 + 用户自定）。                 |
| `Game.Components`     | *全局*               | 宿主工程的 Component，由 Creator 按需生成。             |

Manager / Component 层特意保持在全局命名空间，使生成的模板读起来更自然，
业务代码也无需额外 `using`。

## 许可证

采用 [MIT 许可证](./LICENSE.md) 发布。

第三方依赖的许可证汇总见
[`Third Party Notices.md`](./Third%20Party%20Notices.md)。
