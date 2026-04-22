using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DevWorkbench
{
    public abstract class BaseManagerConfig : ScriptableObject
    {
        protected abstract Dictionary<string, BaseManagerData> GetManagerDataDict();
        public Dictionary<string, BaseManagerData> ExportManagerDataDict() => GetManagerDataDict();
    }

    public abstract class BaseManagerData { }

    public abstract class BaseManager
    {
        protected abstract UniTask SetManagerDataDict();
        internal async UniTask InternalSetManagerDataDict() => await SetManagerDataDict();
    }

    /// <summary>
    /// 游戏启动入口契约。具体实现由宿主工程在 Game.Managers 程序集里提供
    /// （默认模板会投放到 Assets/Game/Manager/GameBoot.cs），挂到场景任意
    /// GameObject 上；Bootstrap 完成所有 Manager 初始化后按接口类型扫描场景
    /// 并调用 OnGameStart。
    /// </summary>
    public interface IGameBoot
    {
        void OnGameStart();
    }

#if UNITY_EDITOR
    public interface IManagerRefresher
    {
        void Refresh(BaseManagerConfig config);
    }
#endif
}
