using DevWorkbench;
using UnityEngine;

// 框架初始化时由 FrameworkBootstrapper 投放到 Assets/Game/Manager/GameBoot.cs。
// 挂在场景任意 GameObject 上即可；Bootstrap 会在所有 Manager 初始化完成后，
// 按 IGameBoot 类型扫描场景并调用 OnGameStart。
// 依赖可通过 VContainer 注入（字段 [Inject] / 构造注入都行），也可以直接
// 用本类所在的 Game.Managers 程序集里任何 Manager 的公开 API。
public class GameBoot : MonoBehaviour, IGameBoot
{
    public void OnGameStart()
    {
    }
}
