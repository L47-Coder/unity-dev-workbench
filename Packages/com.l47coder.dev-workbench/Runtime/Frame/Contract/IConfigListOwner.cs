using System;
using System.Collections;

namespace DevWorkbench
{
    /// <summary>
    /// Exposes a config asset's underlying list of items to tooling (the Dev
    /// Workbench Viewer, code generators, refreshers, ...) via an explicit
    /// contract instead of reflecting a private backing field.
    /// <para>
    /// Implementers are typically generated <c>Config</c> partials such as
    /// <c>FooManagerConfig</c> or <c>BarComponentConfig</c>. The Viewer reads the
    /// list through this interface, so renaming the serialized backing field
    /// (e.g. <c>_configs</c>) is safe as long as the interface stays.
    /// </para>
    /// </summary>
    public interface IConfigListOwner
    {
        /// <summary>Element type of the list returned by <see cref="GetConfigList"/>.</summary>
        Type ConfigItemType { get; }

        /// <summary>
        /// The underlying list instance. Returned as-is so callers can mutate
        /// it in place; after mutation, callers are expected to mark the owning
        /// asset dirty (e.g. via <c>EditorUtility.SetDirty</c>).
        /// </summary>
        IList GetConfigList();
    }
}
