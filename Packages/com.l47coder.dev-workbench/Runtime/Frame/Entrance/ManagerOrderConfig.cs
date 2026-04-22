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
    }
}
