using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DevWorkbench
{
    public abstract class BaseManagerConfig : ScriptableObject
    {
        protected abstract Dictionary<string, BaseManagerData> GetManagerDataDict();
        internal Dictionary<string, BaseManagerData> ExportManagerDataDict() => GetManagerDataDict();
    }

    public abstract class BaseManagerData { }

    public abstract class BaseManager
    {
        protected abstract UniTask SetManagerDataDict();
        internal async UniTask InternalSetManagerDataDict() => await SetManagerDataDict();
    }

    public interface IGameBoot
    {
        UniTask OnGameStart();
    }
}
