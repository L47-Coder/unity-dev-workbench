using System;
using System.Collections.Generic;
using System.Text;
using VContainer;
using VContainer.Unity;

namespace DevWorkbench
{
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var config = FrameworkLoader.LoadSync<ManagerOrderConfig>("Frame/ManagerOrder");

            foreach (var entry in config.Entries)
            {
                var type = ResolveManagerType(entry?.Name);
                if (type == null) continue;

                builder.Register(type, Lifetime.Singleton).As<BaseManager>().AsImplementedInterfaces();
            }
            builder.RegisterEntryPoint<GameBootstrap>();
        }

        // 缓存值为 null 表示"查过但未找到或有冲突"，下次不重复扫描。
        private static readonly Dictionary<string, Type> _managerTypeCache = new(StringComparer.Ordinal);

        private static Type ResolveManagerType(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (_managerTypeCache.TryGetValue(name, out var cached)) return cached;

            var matches = new List<Type>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch { continue; }

                foreach (var type in types)
                {
                    if (type.IsAbstract) continue;
                    if (!typeof(BaseManager).IsAssignableFrom(type)) continue;
                    if (!string.Equals(type.Name, name, StringComparison.Ordinal)) continue;
                    matches.Add(type);
                }
            }

            if (matches.Count == 0)
            {
                UnityEngine.Debug.LogError(
                    $"[DevWorkbench] Manager type '{name}' not found in any loaded assembly. " +
                    "Check that the class exists, is not abstract, and its assembly is loaded.");
                _managerTypeCache[name] = null;
                return null;
            }

            if (matches.Count > 1)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[DevWorkbench] Ambiguous Manager name '{name}': {matches.Count} types found:");
                foreach (var t in matches)
                    sb.AppendLine($"  {t.FullName}  ({t.Assembly.GetName().Name})");
                sb.Append("Rename one of them so every Manager class name is unique across all assemblies.");
                throw new InvalidOperationException(sb.ToString());
            }

            _managerTypeCache[name] = matches[0];
            return matches[0];
        }
    }
}
