using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevWorkbench
{
    public class ManagerOrderConfig : ScriptableObject
    {
        [SerializeField]
        private List<ManagerOrderEntry> _entries = new();
        public List<ManagerOrderEntry> Entries => _entries;
    }

    [Serializable]
    public class ManagerOrderEntry
    {
        [TableColumn(Header = "Manager", Editable = false)]
        public string Name;

        /// <summary>
        /// Assembly-qualified type name written by <c>ManagerOrderSync</c> at edit time.
        /// Used by <c>GameLifetimeScope</c> for O(1) type resolution with no ambiguity risk.
        /// Empty on legacy assets — resolved lazily on first play-mode launch.
        /// </summary>
        [TableColumn(Visible = false)]
        public string AssemblyQualifiedName;
    }
}
