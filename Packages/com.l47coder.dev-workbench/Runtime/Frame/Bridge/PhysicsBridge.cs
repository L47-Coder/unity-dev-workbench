using System;
using UnityEngine;

namespace DevWorkbench
{
    /// <summary>
    /// Optional contract for Components that want to react to trigger collisions
    /// on their attached <see cref="GameObject"/>. Implementations should do the
    /// minimum work inside the callback and defer expensive processing to
    /// <c>OnUpdate</c>.
    /// </summary>
    public interface IOnTrigger
    {
        /// <summary>Called when another <see cref="Collider"/> enters the trigger.</summary>
        void OnTriggerEntered(Collider other);

        /// <summary>Called when another <see cref="Collider"/> leaves the trigger.</summary>
        void OnTriggerExited(Collider other);
    }

    /// <summary>
    /// Optional contract for Components that want to react to non-trigger
    /// collisions on their attached <see cref="GameObject"/>.
    /// </summary>
    public interface IOnCollision
    {
        /// <summary>Called when a <see cref="Collision"/> begins.</summary>
        void OnCollisionEntered(Collision collision);

        /// <summary>Called when a <see cref="Collision"/> ends.</summary>
        void OnCollisionExited(Collision collision);
    }

    /// <summary>
    /// <see cref="MonoBehaviour"/> bridge that forwards Unity physics callbacks
    /// as plain C# events. Managers (typically the Prefab Manager) subscribe to
    /// the internal events and dispatch them to the owning Component. The
    /// architecture layer intentionally emits the source
    /// <see cref="GameObject"/> rather than any Manager-layer handle, keeping
    /// the dependency direction one-way.
    /// </summary>
    public sealed class PhysicsBridge : MonoBehaviour
    {
        internal event Action<GameObject, Collider> TriggerEnter;
        internal event Action<GameObject, Collider> TriggerExit;
        internal event Action<GameObject, Collision> CollisionEnter;
        internal event Action<GameObject, Collision> CollisionExit;

        private void OnTriggerEnter(Collider other)       => TriggerEnter?.Invoke(gameObject, other);
        private void OnTriggerExit(Collider other)        => TriggerExit?.Invoke(gameObject, other);
        private void OnCollisionEnter(Collision collision) => CollisionEnter?.Invoke(gameObject, collision);
        private void OnCollisionExit(Collision collision)  => CollisionExit?.Invoke(gameObject, collision);
    }
}
