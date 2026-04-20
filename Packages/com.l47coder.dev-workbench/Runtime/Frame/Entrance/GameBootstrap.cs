using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace DevWorkbench
{
    /// <summary>
    /// VContainer entry point that drives the framework boot sequence once the
    /// <see cref="GameLifetimeScope"/> has registered every Manager:
    /// <list type="number">
    ///   <item>load each Manager's data dict via
    ///     <see cref="BaseManager.InternalSetManagerDataDict"/>,</item>
    ///   <item>run <see cref="IAsyncInitManager.InitAsync"/> on Managers that
    ///     opted into the async init phase,</item>
    ///   <item>resolve the <see cref="IGameBoot"/> on the <c>Managers</c>
    ///     GameObject and invoke <see cref="IGameBoot.OnGameStart"/>.</item>
    /// </list>
    /// </summary>
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

            var gameBoot = GameObject.Find("Managers").GetComponent<IGameBoot>();
            _container.Inject(gameBoot);
            gameBoot.OnGameStart();
        }
    }
}
