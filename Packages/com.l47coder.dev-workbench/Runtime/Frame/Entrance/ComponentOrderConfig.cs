using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevWorkbench
{
    public class ComponentOrderConfig : ScriptableObject
    {
        [SerializeField]
        private List<ComponentOrderEntry> _entries = new();
        public List<ComponentOrderEntry> Entries => _entries;
    }

    [Serializable]
    public class ComponentOrderEntry
    {
        [TableColumn(Header = "Component", Editable = false)]
        public string Name;

        /// <summary>
        /// Assembly-qualified type name written by <c>ComponentOrderSync</c> at edit time.
        /// Used for unambiguous type resolution when multiple assemblies contain same-named components.
        /// Empty on legacy assets — resolved lazily on next sync.
        /// </summary>
        [TableColumn(Visible = false)]
        public string AssemblyQualifiedName;
    }
}
