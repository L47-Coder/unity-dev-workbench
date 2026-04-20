#if UNITY_EDITOR
using System;

namespace DevWorkbench
{
    /// <summary>
    /// Editor-only convention that maps between a Manager's short name, its
    /// generated <see cref="BaseManagerConfig"/> type name, and the Addressable
    /// address the framework uses to load that config at runtime. This is the
    /// single source of truth for the <c>ManagerConfig/{ManagerName}</c>
    /// convention shared by the Creator (which bakes the literal into the
    /// generated partial and registers the Addressable entry) and by Editor
    /// tooling that validates existing assets against that convention.
    /// </summary>
    public static class ManagerAddressConvention
    {
        /// <summary>
        /// Prefix every Manager config address starts with (trailing slash
        /// included). Appended with the Manager's short name to form the full
        /// Addressable address.
        /// </summary>
        public const string AddressPrefix = "ManagerConfig/";

        private const string ConfigTypeSuffix = "ManagerConfig";

        /// <summary>
        /// Compose the Addressable address for a Manager given its short name
        /// (for example <c>"Prefab"</c> → <c>"ManagerConfig/Prefab"</c>).
        /// Returns <c>null</c> for null/empty input.
        /// </summary>
        public static string AddressOf(string managerName)
            => string.IsNullOrEmpty(managerName) ? null : AddressPrefix + managerName;

        /// <summary>
        /// Extract the Manager's short name from a <see cref="BaseManagerConfig"/>
        /// subtype whose name follows the <c>{ManagerName}ManagerConfig</c>
        /// convention (for example <c>PrefabManagerConfig</c> → <c>Prefab</c>).
        /// Returns <c>null</c> when the input type is null or its name does
        /// not match the convention.
        /// </summary>
        public static string ManagerNameOf(Type configType)
        {
            var name = configType?.Name;
            if (string.IsNullOrEmpty(name) || !name.EndsWith(ConfigTypeSuffix, StringComparison.Ordinal))
                return null;

            var managerName = name[..^ConfigTypeSuffix.Length];
            return string.IsNullOrEmpty(managerName) ? null : managerName;
        }

        /// <summary>
        /// Resolve the expected Addressable address from a
        /// <see cref="BaseManagerConfig"/> subtype whose name follows the
        /// <c>{ManagerName}ManagerConfig</c> convention. Returns <c>null</c>
        /// when the input type is null or its name does not match the
        /// convention.
        /// </summary>
        public static string AddressOfConfigType(Type configType)
            => AddressOf(ManagerNameOf(configType));
    }
}
#endif
