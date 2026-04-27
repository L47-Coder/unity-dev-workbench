using Cysharp.Threading.Tasks;
using DevWorkbench;
using UnityEngine;
using VContainer;

public class GameBoot : MonoBehaviour, IGameBoot
{
    [Inject] private readonly IPrefabManager _prefabManager;
    public async UniTask OnGameStart()
    {
        var handle = await _prefabManager.LoadPrefabAsync("Ground");

        await UniTask.Delay(2000);

        await _prefabManager.ReleasePrefabAsync(handle);

        await UniTask.Delay(2000);

        await _prefabManager.LoadPrefabAsync("Ground");

        await UniTask.CompletedTask;
    }
}
