using System;
using System.Collections.Generic;
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

        private static readonly Dictionary<string, Type> _managerTypeCache = new(StringComparer.Ordinal);

        private static Type ResolveManagerType(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (_managerTypeCache.TryGetValue(name, out var cached)) return cached;

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

                    _managerTypeCache[name] = type;
                    return type;
                }
            }

            _managerTypeCache[name] = null;
            return null;
        }
    }
}
