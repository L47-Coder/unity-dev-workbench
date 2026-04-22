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
    }
}
