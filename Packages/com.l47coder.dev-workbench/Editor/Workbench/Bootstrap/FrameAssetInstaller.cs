using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // Frame 组 + ManagerOrder / ComponentOrder 资产的幂等安装器。
    // Page（ManagerOrderPage、ComponentOrderPage）和一键初始化流程共享此处逻辑。
    internal static class FrameAssetInstaller
    {
        public const string FrameGroupName = "Frame";
        public const string ManagerConfigGroupName = "ManagerConfig";

        public const string ManagerOrderAssetPath = "Assets/Game/Frame/ManagerOrder.asset";
        public const string ComponentOrderAssetPath = "Assets/Game/Frame/ComponentOrder.asset";
        // PageOrder 属于 editor-only 偏好，不走 Addressables；放在这里只是登记"Frame 下有哪些 asset"。
        public const string PageOrderAssetPath = "Assets/Game/Frame/PageOrder.asset";
        public const string FrameConfigFolder = "Assets/Game/Frame";

        public static readonly string ManagerOrderAddress =
            $"{FrameGroupName}/{Path.GetFileNameWithoutExtension(ManagerOrderAssetPath)}";
        public static readonly string ComponentOrderAddress =
            $"{FrameGroupName}/{Path.GetFileNameWithoutExtension(ComponentOrderAssetPath)}";

        // ── Addressables ──────────────────────────────────────────────────────────

        public static bool EnsureAddressablesInitialized()
        {
            if (AddressableAssetSettingsDefaultObject.Settings != null) return false;

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                Debug.LogError("[FrameAssetInstaller] Failed to create AddressableAssetSettings.");
                return false;
            }

            AssetDatabase.SaveAssets();
            return true;
        }

        public static AddressableAssetGroup EnsureGroup(string groupName)
        {
            if (string.IsNullOrEmpty(groupName)) return null;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return null;

            var group = settings.groups.FirstOrDefault(g => g != null && g.Name == groupName);
            if (group != null) return group;

            group = settings.CreateGroup(groupName, false, false, true, null);
            EditorUtility.SetDirty(settings);
            return group;
        }

        // ── Order 资产 ───────────────────────────────────────────────────────────

        public static ManagerOrderConfig EnsureManagerOrderAsset()
        {
            return EnsureOrderAsset<ManagerOrderConfig>(ManagerOrderAssetPath, ManagerOrderAddress);
        }

        public static ComponentOrderConfig EnsureComponentOrderAsset()
        {
            return EnsureOrderAsset<ComponentOrderConfig>(ComponentOrderAssetPath, ComponentOrderAddress);
        }

        private static T EnsureOrderAsset<T>(string assetPath, string address) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                EnsureFolder(FrameConfigFolder);
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.SaveAssets();
            }

            EnsureAddressableEntry(assetPath, FrameGroupName, address);
            return asset;
        }

        // 把指定 asset 挂到指定 group，并对齐 address。已就位时短路返回。
        public static bool EnsureAddressableEntry(string assetPath, string groupName, string address)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning($"[FrameAssetInstaller] AddressableAssetSettings not found; skipping registration of {assetPath}.");
                return false;
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid)) return false;

            var group = EnsureGroup(groupName);
            if (group == null) return false;

            var existing = settings.FindAssetEntry(guid);
            if (existing != null && existing.parentGroup == group && existing.address == address)
                return false;

            var entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = address;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
            AssetDatabase.SaveAssets();
            return true;
        }

        // ── Order 同步 ───────────────────────────────────────────────────────────

        public static void SyncManagerOrderEntries(ManagerOrderConfig config)
        {
            if (config == null) return;
            SyncEntries(
                config.Entries,
                CollectTypeNames(typeof(BaseManager)),
                e => e.Name,
                name => new ManagerOrderEntry { Name = name },
                () => EditorUtility.SetDirty(config));
        }

        public static void SyncComponentOrderEntries(ComponentOrderConfig config)
        {
            if (config == null) return;
            SyncEntries(
                config.Entries,
                CollectTypeNames(typeof(BaseComponent)),
                e => e.Name,
                name => new ComponentOrderEntry { Name = name },
                () => EditorUtility.SetDirty(config));
        }

        private static void SyncEntries<TEntry>(
            List<TEntry> entries,
            HashSet<string> live,
            Func<TEntry, string> nameOf,
            Func<string, TEntry> factory,
            Action markDirty)
        {
            var removed = entries.RemoveAll(e => !live.Contains(nameOf(e))) > 0;

            var existing = new HashSet<string>(entries.Select(nameOf), StringComparer.Ordinal);
            var added = false;
            foreach (var name in live.Where(n => !existing.Contains(n)))
            {
                entries.Add(factory(name));
                added = true;
            }

            if (removed || added)
            {
                markDirty?.Invoke();
                AssetDatabase.SaveAssets();
            }
        }

        private static HashSet<string> CollectTypeNames(Type baseType)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!type.IsAbstract && baseType.IsAssignableFrom(type) && type != baseType)
                            result.Add(type.Name);
                    }
                }
                catch { }
            }
            return result;
        }

        // ── 文件夹工具 ───────────────────────────────────────────────────────────

        public static void EnsureFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
            if (assetPath == "Assets" || AssetDatabase.IsValidFolder(assetPath)) return;

            var parts = assetPath.Split('/');
            for (var i = 1; i < parts.Length; i++)
            {
                var parent = string.Join("/", parts, 0, i);
                var child = $"{parent}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(child))
                    AssetDatabase.CreateFolder(parent, parts[i]);
            }
        }
    }
}
