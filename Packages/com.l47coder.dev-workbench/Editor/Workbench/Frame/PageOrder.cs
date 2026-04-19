using System;
using System.Collections.Generic;
using System.Linq;
using DevWorkbench;
using UnityEngine;

namespace DevWorkbench.Editor
{

    internal sealed class PageOrder : ScriptableObject
    {
        [Serializable]
        private class GroupEntry
        {
            public string Title;
            public int Order;
            public List<TabEntry> Tabs = new();
        }

        [Serializable]
        private struct TabEntry
        {
            public string Title;
            public int Order;
        }

        [SerializeField] private List<GroupEntry> _groups = new();

        public Dictionary<string, int> GetGroupDict() =>
            _groups.ToDictionary(g => g.Title, g => g.Order);

        public Dictionary<string, int> GetTabDict(string groupTitle)
        {
            var group = _groups.FirstOrDefault(g => g.Title == groupTitle);
            return group?.Tabs.ToDictionary(t => t.Title, t => t.Order) ?? new Dictionary<string, int>();
        }

        public void SetGroupDict(Dictionary<string, int> dict)
        {
            var existing = _groups.ToDictionary(g => g.Title);
            _groups = dict
                .Select(kv => new GroupEntry
                {
                    Title = kv.Key,
                    Order = kv.Value,
                    Tabs = existing.TryGetValue(kv.Key, out var g) ? g.Tabs : new List<TabEntry>()
                })
                .ToList();
        }

        public void SetTabDict(string groupTitle, Dictionary<string, int> dict)
        {
            var group = _groups.FirstOrDefault(g => g.Title == groupTitle);
            if (group == null) return;
            group.Tabs = dict.Select(kv => new TabEntry { Title = kv.Key, Order = kv.Value }).ToList();
        }
    }
}
