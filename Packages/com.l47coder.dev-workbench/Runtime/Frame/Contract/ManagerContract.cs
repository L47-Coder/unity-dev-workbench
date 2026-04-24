using System;
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
    /// 游戏启动入口契约。具体实现由宿主工程在 Game.Frame 程序集里提供
    /// （框架初始化时代码生成 Assets/Game/Frame/GameBoot.cs），挂到场景任意
    /// GameObject 上；Bootstrap 完成所有 Manager 初始化后按接口类型扫描场景
    /// 并调用 OnGameStart。
    /// </summary>
    public interface IGameBoot
    {
        UniTask OnGameStart();
    }

#if UNITY_EDITOR
    /// <summary>
    /// 标记一段"编辑器侧一次性同步逻辑"。被标记的方法必须是 public / internal static 无参，
    /// Framework/Sync 页的 Sync Runtime 按钮会反射枚举所有带此标记的方法并按 Order 升序逐一调用。
    /// 每个 Manager 的 Refresher、或任何 game-level 的编辑器对齐脚本都可以挂这个特性。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class EditorSyncAttribute : Attribute
    {
        public int Order { get; }
        public EditorSyncAttribute(int order = 0) { Order = order; }
    }
#endif
}
