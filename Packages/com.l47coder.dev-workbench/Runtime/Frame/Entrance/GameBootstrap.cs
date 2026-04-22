using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace DevWorkbench
{
    public interface IAsyncInitManager
    {
        UniTask InitAsync(CancellationToken token);
    }

    internal class GameBootstrap : IAsyncStartable
    {
        private readonly IObjectResolver _container;

        public GameBootstrap(IObjectResolver container) => _container = container;

        public async UniTask StartAsync(CancellationToken token)
        {
            var managers = _container.Resolve<IReadOnlyList<BaseManager>>();

            foreach (var manager in managers)
                await manager.InternalSetManagerDataDict();

            foreach (var manager in managers)
            {
                if (manager is IAsyncInitManager init)
                    await init.InitAsync(token);
            }

            var gameBoot = ResolveGameBoot();
            if (gameBoot == null) return;

            _container.Inject(gameBoot);
            gameBoot.OnGameStart();
        }

        // 按 IGameBoot 接口扫描场景里所有 MonoBehaviour；不依赖具体实现类型的符号，
        // 因此具体 GameBoot 类可以住在 Game.Frame 等上层程序集里。
        private static IGameBoot ResolveGameBoot()
        {
            var behaviours = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);

            IGameBoot first = null;
            int count = 0;
            MonoBehaviour firstOwner = null;

            foreach (var b in behaviours)
            {
                if (b is not IGameBoot boot) continue;
                if (first == null) { first = boot; firstOwner = b; }
                count++;
            }

            if (count == 0)
            {
                Debug.LogWarning("[DevWorkbench] No IGameBoot in scene.");
                return null;
            }

            if (count > 1)
                Debug.LogWarning($"[DevWorkbench] Multiple IGameBoot ({count}); using '{firstOwner.name}'.");

            return first;
        }
    }
}
