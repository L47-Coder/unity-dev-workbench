using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace DevWorkbench
{

    internal class GameBootstrap : IAsyncStartable
    {
        private readonly IObjectResolver _container;
        public GameBootstrap(IObjectResolver container) => _container = container;
        public async UniTask StartAsync(CancellationToken token)
        {
            var managers = _container.Resolve<IReadOnlyList<BaseManager>>();

            foreach (var manager in managers)
            {
                Debug.Log(manager.GetType().ToString());

                await manager.InternalSetManagerDataDict();
            }


            foreach (var manager in managers)
            {
                if (manager is IAsyncInitManager init)
                    await init.InitAsync(token);
            }

            var gameBoot = GameObject.Find("Managers").GetComponent<IGameBoot>();
            _container.Inject(gameBoot);
            gameBoot.OnGameStart();
        }
    }
}
