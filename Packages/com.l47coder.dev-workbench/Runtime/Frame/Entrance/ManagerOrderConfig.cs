using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevWorkbench
{
    /// <summary>
    /// ScriptableObject that declares the order in which Managers are
    /// registered into the <see cref="GameLifetimeScope"/>. Entries are resolved
    /// by type name, so the same asset works regardless of the assembly each
    /// Manager lives in.
    /// </summary>
    public class ManagerOrderConfig : ScriptableObject
    {
        [SerializeField]
        private List<ManagerOrderEntry> _entries = new();

        /// <summary>The ordered list of Manager entries.</summary>
        public List<ManagerOrderEntry> Entries => _entries;
    }

    /// <summary>
    /// Single row inside a <see cref="ManagerOrderConfig"/>; identifies a
    /// Manager by its <see cref="Type.Name"/>.
    /// </summary>
    [Serializable]
    public class ManagerOrderEntry
    {
        /// <summary>
        /// The type name (without namespace) of the Manager. Matched against
        /// types that derive from <see cref="BaseManager"/> at start-up.
        /// </summary>
        [TableColumn(Header = "Manager", Editable = false)]
        public string Name;
    }
}
