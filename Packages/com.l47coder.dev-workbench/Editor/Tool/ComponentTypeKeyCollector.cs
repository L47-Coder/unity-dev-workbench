using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace DevWorkbench.Editor
{
    internal readonly struct ComponentTypeGroup
    {
        public readonly string ComponentClassName;
        public readonly string[] DataKeys;

        public ComponentTypeGroup(string componentClassName, string[] dataKeys)
        {
            ComponentClassName = componentClassName;
            DataKeys = dataKeys;
        }
    }

    /// <summary>
    /// 枚举当前项目中所有可用的组件数据。结果随 projectChanged 事件自动失效。
    /// </summary>
    internal static class ComponentTypeKeyCollector
    {
        private static List<ComponentTypeGroup> _groupCache;

        static ComponentTypeKeyCollector()
        {
            EditorApplication.projectChanged -= Invalidate;
            EditorApplication.projectChanged += Invalidate;
        }

        public static void Invalidate() => _groupCache = null;

        /// <summary>返回按 ComponentClassName 分组的数据，每组含有序 DataKey 数组。</summary>
        public static List<ComponentTypeGroup> CollectGrouped()
        {
            if (_groupCache != null) return _groupCache;

            _groupCache = new List<ComponentTypeGroup>();

            foreach (var configType in TypeCache.GetTypesDerivedFrom<BaseComponentConfig>())
            {
                if (configType.IsAbstract) continue;

                var componentName = ComponentAddressConvention.ComponentNameOf(configType);
                if (string.IsNullOrEmpty(componentName)) continue;

                var assetPath = ComponentAssetIndex.FindComponentAsset($"{configType.Name}.asset");
                if (string.IsNullOrEmpty(assetPath)) continue;

                var config = AssetDatabase.LoadAssetAtPath<BaseComponentConfig>(assetPath);
                if (config == null) continue;

                var list = config.GetConfigList();
                var itemType = config.ConfigItemType;
                if (list == null || itemType == null) continue;

                var keyField = itemType.GetField("Key", BindingFlags.Public | BindingFlags.Instance);
                if (keyField == null || keyField.FieldType != typeof(string)) continue;

                var keys = new List<string>();
                foreach (var item in list)
                {
                    var key = (string)keyField.GetValue(item);
                    if (!string.IsNullOrWhiteSpace(key)) keys.Add(key.Trim());
                }

                if (keys.Count > 0)
                    _groupCache.Add(new ComponentTypeGroup($"{componentName}Component", keys.ToArray()));
            }

            _groupCache.Sort((a, b) => System.StringComparer.Ordinal.Compare(a.ComponentClassName, b.ComponentClassName));
            return _groupCache;
        }

        /// <summary>返回所有可用 TypeKey 的平铺列表（格式：ComponentClassName_DataKey）。</summary>
        public static List<string> Collect()
        {
            var result = new List<string>();
            foreach (var group in CollectGrouped())
                foreach (var key in group.DataKeys)
                    result.Add($"{group.ComponentClassName}_{key}");
            result.Sort(System.StringComparer.Ordinal);
            return result;
        }
    }
}
