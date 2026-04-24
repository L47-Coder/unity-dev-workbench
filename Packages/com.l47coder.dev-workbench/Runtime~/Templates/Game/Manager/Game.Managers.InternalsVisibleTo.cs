using System.Runtime.CompilerServices;

// 配套 Game.Managers.asmdef 的程序集元数据文件。
// Game.Editor 程序集里的 Refresher 等编辑器代码要访问
// *ManagerData / *ManagerConfig.EditorConfigs 这类 internal 成员，
// 这是 C# 层面唯一的声明位置，需跟随 Game.Managers 一起存在。
[assembly: InternalsVisibleTo("Game.Editor")]
