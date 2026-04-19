using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DevWorkbench
{
    internal sealed class ComponentBridgeBag : MonoBehaviour
    {
        private readonly Dictionary<Type, HashSet<Component>> _leased = new();

        internal HashSet<Component> GetSet(Type type)
        {
            if (!_leased.TryGetValue(type, out var set))
            {
                set = new HashSet<Component>();
                _leased[type] = set;
            }
            return set;
        }
    }

    /// <summary>
    /// Cooperative lease broker for built-in Unity <see cref="Component"/>s.
    /// <para>
    /// <see cref="Acquire{T}"/> returns the first <typeparamref name="T"/> on
    /// <paramref name="go"/> that is not already leased; if none is available
    /// a new one is added. <see cref="Release{T}"/> returns a previously
    /// acquired instance to the pool so it can be re-acquired.
    /// </para>
    /// <para>
    /// The bridge lets several Dev Workbench Components share a single
    /// <see cref="GameObject"/> without stepping on each other's Unity
    /// components (e.g. two Components each wanting a <see cref="Rigidbody"/>).
    /// </para>
    /// </summary>
    public static class ComponentBridge
    {
        /// <summary>
        /// Acquire a <typeparamref name="T"/> component on <paramref name="go"/>
        /// that is not already leased. A fresh component is added when none
        /// remain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="go"/> is null.</exception>
        public static T Acquire<T>(GameObject go) where T : Component
        {
            if (go == null) throw new ArgumentNullException(nameof(go));

            if (!go.TryGetComponent<ComponentBridgeBag>(out var bag))
            {
                Debug.LogWarning($"[ComponentBridge] ComponentBridgeBag missing on GameObject, auto-attaching: {go.name}");
                bag = go.AddComponent<ComponentBridgeBag>();
            }

            var set = bag.GetSet(typeof(T));
            set.RemoveWhere(c => c == null);

            using (ListPool<T>.Get(out var buffer))
            {
                go.GetComponents(buffer);
                foreach (var c in buffer)
                {
                    if (c == null) continue;
                    if (set.Contains(c)) continue;
                    set.Add(c);
                    return c;
                }
            }

            var fresh = go.AddComponent<T>();
            set.Add(fresh);
            return fresh;
        }

        /// <summary>
        /// Return a previously acquired component to the shared pool. The
        /// underlying Unity <see cref="Component"/> is not destroyed.
        /// </summary>
        public static void Release<T>(T component) where T : Component
        {
            if (component == null) return;
            if (component.gameObject.TryGetComponent<ComponentBridgeBag>(out var bag))
                bag.GetSet(typeof(T)).Remove(component);
        }
    }
}
