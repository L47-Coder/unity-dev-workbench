using Cysharp.Threading.Tasks;
using DevWorkbench;
using UnityEngine;
using VContainer;

public class GameBoot : MonoBehaviour, IGameBoot
{
    public async UniTask OnGameStart()
    {
        await UniTask.CompletedTask;
    }
}
