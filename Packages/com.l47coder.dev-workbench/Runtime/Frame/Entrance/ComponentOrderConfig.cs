using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevWorkbench
{
    /// <summary>
    /// ScriptableObject that lists the Component types exposed to the
    /// DevWorkbench. The workbench uses this to drive its Component viewer
    /// and ordering UI; the runtime framework itself does not depend on it.
    /// </summary>
    public class ComponentOrderConfig : ScriptableObject
    {
        [SerializeField]
        private List<ComponentOrderEntry> _entries = new();

        /// <summary>The ordered list of Component entries.</summary>
        public List<ComponentOrderEntry> Entries => _entries;
    }

    /// <summary>
    /// Single row inside a <see cref="ComponentOrderConfig"/>; identifies a
    /// Component type by its <see cref="Type.Name"/>.
    /// </summary>
    [Serializable]
    public class ComponentOrderEntry
    {
        /// <summary>The type name (without namespace) of the Component.</summary>
        [TableColumn(Header = "Component", Editable = false)]
        public string Name;
    }
}