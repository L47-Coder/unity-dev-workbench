using System.Runtime.CompilerServices;

// 包内 Editor 装配体（DevWorkbench.Editor）需要访问 Runtime 的 internal 类型
// （如 *ManagerData、*ManagerConfig.EditorConfigs），因此放开 InternalsVisibleTo。
[assembly: InternalsVisibleTo("DevWorkbench.Editor")]

// 用户工程 Assets/Game/Manager/ 下的 Game.Managers asmdef 承载所有 Manager 代码
// （默认 AssetManager / ComponentManager / PrefabManager 以及用户 Creator 生成的 Manager）。
// 这些 Manager 属于"调度层"，允许访问 BaseComponent 生命周期后门等 internal 接口。
[assembly: InternalsVisibleTo("Game.Managers")]
