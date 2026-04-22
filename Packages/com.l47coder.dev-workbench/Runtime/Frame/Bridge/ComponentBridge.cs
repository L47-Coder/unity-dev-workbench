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

    public static class ComponentBridge
    {
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

        public static void Release<T>(T component) where T : Component
        {
            if (component == null) return;
            if (component.gameObject.TryGetComponent<ComponentBridgeBag>(out var bag))
                bag.GetSet(typeof(T)).Remove(component);
        }
    }
}
