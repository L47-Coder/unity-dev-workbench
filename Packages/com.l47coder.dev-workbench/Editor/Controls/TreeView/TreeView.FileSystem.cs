#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    public sealed partial class TreeView
    {
        private enum NodeKind { Root, Branch, FolderLeaf, FileLeaf, ReadOnlyFile, ReadOnlyFolder }

        private sealed class TreeNode
        {
            public string Name;
            public string FullPath;   // Assets/ 相对路径
            public NodeKind Kind;
            public List<TreeNode> Children;   // FileLeaf 为 null
            public bool IsExpanded;
            public TreeNode Parent;
        }

        private readonly struct FlatNode
        {
            public readonly TreeNode Node;
            public readonly int Depth;
            public FlatNode(TreeNode node, int depth) { Node = node; Depth = depth; }
        }

        private void RebuildTree(string rootPath)
        {
            _cachedRootPath = rootPath;
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                _root = null;
                return;
            }

            _root = new TreeNode
            {
                Name = Path.GetFileName(rootPath),
                FullPath = rootPath,
                Kind = NodeKind.Root,
                IsExpanded = true,
                Children = new List<TreeNode>()
            };

            ScanDirectory(_root);
        }

        private void ScanDirectory(TreeNode parent)
        {
            string[] dirs;
            string[] files;
            try
            {
                dirs = Directory.GetDirectories(parent.FullPath);
                files = Directory.GetFiles(parent.FullPath);
            }
            catch { return; }

            foreach (var dir in dirs)
            {
                var assetPath = ToAssetPath(NormalizePath(dir));
                var name = Path.GetFileName(assetPath);
                if (IsIgnored(name)) continue;

                var isLeaf = File.Exists($"{assetPath}/{LeafMarkerFileName}");
                var node = new TreeNode
                {
                    Name = name,
                    FullPath = assetPath,
                    Kind = isLeaf ? NodeKind.FolderLeaf : NodeKind.Branch,
                    Children = new List<TreeNode>(),
                    Parent = parent
                };

                if (isLeaf) ScanReadOnlyContents(node);
                else ScanDirectory(node);

                parent.Children.Add(node);
            }

            if (parent.Kind == NodeKind.Root || parent.Kind == NodeKind.Branch)
            {
                foreach (var file in files)
                {
                    var assetPath = ToAssetPath(NormalizePath(file));
                    var name = Path.GetFileName(assetPath);
                    if (IsIgnored(name)) continue;
                    parent.Children.Add(new TreeNode
                    {
                        Name = name,
                        FullPath = assetPath,
                        Kind = NodeKind.FileLeaf,
                        Parent = parent
                    });
                }
            }
        }

        private void ScanReadOnlyContents(TreeNode parent)
        {
            string[] files;
            string[] dirs;
            try
            {
                files = Directory.GetFiles(parent.FullPath);
                dirs = Directory.GetDirectories(parent.FullPath);
            }
            catch { return; }

            foreach (var file in files)
            {
                var assetPath = ToAssetPath(NormalizePath(file));
                var name = Path.GetFileName(assetPath);
                if (IsIgnored(name)) continue;
                parent.Children.Add(new TreeNode
                {
                    Name = name,
                    FullPath = assetPath,
                    Kind = NodeKind.ReadOnlyFile,
                    Parent = parent
                });
            }

            foreach (var dir in dirs)
            {
                var assetPath = ToAssetPath(NormalizePath(dir));
                var name = Path.GetFileName(assetPath);
                if (IsIgnored(name)) continue;
                var sub = new TreeNode
                {
                    Name = name,
                    FullPath = assetPath,
                    Kind = NodeKind.ReadOnlyFolder,
                    Children = new List<TreeNode>(),
                    Parent = parent
                };
                ScanReadOnlyContents(sub);
                parent.Children.Add(sub);
            }
        }

        private bool IsIgnored(string name)
        {
            if (string.Equals(name, LeafMarkerFileName, StringComparison.OrdinalIgnoreCase))
                return true;
            if (name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                return true;
            if (IgnoredNames == null) return false;
            foreach (var pattern in IgnoredNames)
            {
                if (string.IsNullOrEmpty(pattern)) continue;
                // 兼容 VSCode files.exclude 格式：剥离 **/ 前缀和末尾 /
                var p = pattern;
                while (p.StartsWith("**/", StringComparison.Ordinal)) p = p[3..];
                if (p.EndsWith("/", StringComparison.Ordinal)) p = p.TrimEnd('/');
                if (!string.IsNullOrEmpty(p) && MatchesGlobPattern(name, p))
                    return true;
            }
            return false;
        }

        private static bool MatchesGlobPattern(string name, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return false;
            return GlobMatch(name.ToLowerInvariant(), pattern.ToLowerInvariant(), 0, 0);
        }

        private static bool GlobMatch(string text, string pattern, int ti, int pi)
        {
            while (ti < text.Length && pi < pattern.Length)
            {
                if (pattern[pi] == '?')
                { ti++; pi++; }
                else if (pattern[pi] == '*')
                {
                    while (pi < pattern.Length && pattern[pi] == '*') pi++;
                    if (pi == pattern.Length) return true;
                    while (ti < text.Length)
                    {
                        if (GlobMatch(text, pattern, ti, pi)) return true;
                        ti++;
                    }
                    return false;
                }
                else if (pattern[pi] == text[ti])
                { ti++; pi++; }
                else return false;
            }
            while (pi < pattern.Length && pattern[pi] == '*') pi++;
            return ti == text.Length && pi == pattern.Length;
        }

        private static bool CanOperateNode(TreeNode node) =>
            node != null && node.Kind != NodeKind.Root &&
            node.Kind != NodeKind.ReadOnlyFile && node.Kind != NodeKind.ReadOnlyFolder;

        private static bool CanAddChildToNode(TreeNode node) =>
            node != null && (node.Kind == NodeKind.Root || node.Kind == NodeKind.Branch);

        private static bool HasAnyLeafDescendant(TreeNode node)
        {
            if (node.Children == null) return false;
            foreach (var child in node.Children)
            {
                if (child.Kind == NodeKind.FolderLeaf || child.Kind == NodeKind.FileLeaf)
                    return true;
                if (child.Kind == NodeKind.Branch && HasAnyLeafDescendant(child))
                    return true;
            }
            return false;
        }

        private void ExecuteCreateFolder(TreeNode parent)
        {
            if (!CanAddChildToNode(parent)) return;
            var idx = 0;
            var name = "NewFolder";
            while (AssetDatabase.IsValidFolder($"{parent.FullPath}/{name}"))
                name = "NewFolder" + (++idx);
            var guid = AssetDatabase.CreateFolder(parent.FullPath, name);
            if (string.IsNullOrEmpty(guid)) { Debug.LogWarning("[TreeView] Failed to create folder."); return; }
            var newPath = $"{parent.FullPath}/{name}";
            RefreshTree(newPath);
            _onNodeCreated?.Invoke(newPath);
        }

        private void ExecuteRename(TreeNode node, string newName)
        {
            if (!CanOperateNode(node) || string.IsNullOrWhiteSpace(newName)) return;
            newName = newName.Trim();
            if (string.Equals(node.Name, newName, StringComparison.Ordinal)) return;
            var oldPath = node.FullPath;
            var destPath = $"{GetParentPath(oldPath)}/{newName}";
            var error = AssetDatabase.MoveAsset(oldPath, destPath);
            if (!string.IsNullOrEmpty(error)) { Debug.LogWarning($"[TreeView] Rename failed: {error}"); return; }
            if (string.Equals(_selectedPathBacking, oldPath, StringComparison.OrdinalIgnoreCase))
                _selectedPathBacking = destPath;
            RefreshTree(destPath);
            _onNodeRenamed?.Invoke(oldPath, destPath);
        }

        private void ExecuteDelete(TreeNode node)
        {
            if (!CanOperateNode(node)) return;
            if (!EditorUtility.DisplayDialog("Confirm deletion", $"Delete \"{node.Name}\"? This operation cannot be undone.", "Delete", "Cancel"))
                return;
            var deletedPath = node.FullPath;
            if (!AssetDatabase.DeleteAsset(deletedPath))
            { Debug.LogWarning($"[TreeView] Delete failed: {deletedPath}"); return; }
            if (string.Equals(_selectedPathBacking, deletedPath, StringComparison.OrdinalIgnoreCase))
                _selectedPathBacking = null;
            RefreshTree(_cachedRootPath);
            _onNodeDeleted?.Invoke(deletedPath);
        }

        private void ExecuteMove(string sourcePath, string targetDirPath)
        {
            var src = NormalizePath(sourcePath);
            var tgt = NormalizePath(targetDirPath);
            if (string.Equals(GetParentPath(src), tgt, StringComparison.OrdinalIgnoreCase)) return;
            if (tgt.StartsWith(src + "/", StringComparison.OrdinalIgnoreCase))
            { Debug.LogWarning("[TreeView] Cannot move a folder into one of its own subfolders."); return; }
            var destPath = $"{tgt}/{Path.GetFileName(src)}";
            var error = AssetDatabase.MoveAsset(src, destPath);
            if (!string.IsNullOrEmpty(error)) { Debug.LogWarning($"[TreeView] Move failed: {error}"); return; }
            if (string.Equals(_selectedPathBacking, src, StringComparison.OrdinalIgnoreCase))
                _selectedPathBacking = destPath;
            RefreshTree(destPath);
            _onNodeMoved?.Invoke(src, destPath);
        }

        private void RefreshTree(string focusPath = null)
        {
            AssetDatabase.Refresh();
            var expanded = CollectExpandedPaths();
            RebuildTree(_cachedRootPath);
            RestoreExpandedPaths(expanded);
            if (!string.IsNullOrEmpty(focusPath))
                _selectedPathBacking = NormalizePath(focusPath);
        }

        private HashSet<string> CollectExpandedPaths()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_root != null) CollectExpandedRec(_root, set);
            return set;
        }

        private static void CollectExpandedRec(TreeNode node, HashSet<string> set)
        {
            if (node.IsExpanded) set.Add(node.FullPath);
            if (node.Children == null) return;
            foreach (var child in node.Children) CollectExpandedRec(child, set);
        }

        private void RestoreExpandedPaths(HashSet<string> set)
        {
            if (_root != null) RestoreExpandedRec(_root, set);
        }

        private static void RestoreExpandedRec(TreeNode node, HashSet<string> set)
        {
            if (set.Contains(node.FullPath)) node.IsExpanded = true;
            if (node.Children == null) return;
            foreach (var child in node.Children) RestoreExpandedRec(child, set);
        }

        private List<FlatNode> BuildFlatList()
        {
            var list = new List<FlatNode>(32);
            if (_root == null) return list;
            if (!string.IsNullOrEmpty(_searchNormalized))
                CollectSearchResults(_root, list);
            else
                AppendFlatNode(_root, 0, list);
            return list;
        }

        private static void AppendFlatNode(TreeNode node, int depth, List<FlatNode> list)
        {
            list.Add(new FlatNode(node, depth));
            if (!node.IsExpanded || node.Children == null) return;
            foreach (var child in node.Children)
                AppendFlatNode(child, depth + 1, list);
        }

        private void CollectSearchResults(TreeNode node, List<FlatNode> list)
        {
            // 根节点作为固定标题行（depth=0），搜索结果缩进一级（depth=1）
            if (node.Kind == NodeKind.Root)
            {
                list.Add(new FlatNode(node, 0));
                if (node.Children == null) return;
                foreach (var child in node.Children)
                    CollectSearchResults(child, list);
                return;
            }

            // 只对叶子节点做名称匹配，扁平放在第一层
            if (node.Kind == NodeKind.FolderLeaf || node.Kind == NodeKind.FileLeaf)
            {
                if (MatchesFuzzySearch(node.Name, _searchNormalized))
                {
                    list.Add(new FlatNode(node, 1));
                    // FolderLeaf 展开时，在搜索结果中也展示其内部文件
                    if (node.Kind == NodeKind.FolderLeaf && node.IsExpanded && node.Children != null)
                        foreach (var child in node.Children)
                            AppendFlatNode(child, 2, list);
                }
                return;
            }

            // Branch 节点：不加入结果，继续向下递归子节点
            if (node.Children == null) return;
            foreach (var child in node.Children)
                CollectSearchResults(child, list);
        }

        private TreeNode FindNodeByPath(string path)
        {
            if (_root == null || string.IsNullOrEmpty(path)) return null;
            return FindNodeRec(_root, NormalizePath(path));
        }

        private static TreeNode FindNodeRec(TreeNode node, string path)
        {
            if (string.Equals(node.FullPath, path, StringComparison.OrdinalIgnoreCase)) return node;
            if (node.Children == null) return null;
            foreach (var child in node.Children)
            {
                var found = FindNodeRec(child, path);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif