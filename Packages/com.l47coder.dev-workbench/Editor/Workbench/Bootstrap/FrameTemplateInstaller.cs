using System.IO;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // Frame 层的"程序集容器 + 启动入口"安装器，职责对称于 ManagerTemplateInstaller /
    // ComponentTemplateInstaller，但内容完全固定，因此不走 Runtime~/Templates 文件拷贝，
    // 而是内嵌字符串直接代码生成：
    //   1. Game.Frame.asmdef —— Frame 层的程序集容器，依赖 DevWorkbench + Game.Managers
    //      等与 Game.Managers 对齐的外部库；Bootstrap 通过 IGameBoot 接口反向解耦，
    //      因此不需要额外依赖 Game.Components。
    //   2. GameBoot.cs     —— 启动入口的默认实现。每个项目必需且唯一；若用户已在旧位置
    //      Assets/Game/Manager/GameBoot.cs 保留了自定义实现，会用 AssetDatabase.MoveAsset
    //      迁移到 Frame 下以保留 GUID（场景里的 MonoBehaviour 引用因此不会丢）。
    //
    // Frame 目录过去只放 ScriptableObject 资产（ManagerOrder / ComponentOrder / PageOrder），
    // asmdef 只作用于 .cs 文件，两者可共存；这意味着宿主工程日后在 Assets/Game/Frame/ 下
    // 写的任何启动装配代码都会进入 Game.Frame 程序集。
    internal static class FrameTemplateInstaller
    {
        public const string FrameRootAssetPath = "Assets/Game/Frame";
        public const string AsmdefAssetPath = FrameRootAssetPath + "/Game.Frame.asmdef";
        public const string GameBootAssetPath = FrameRootAssetPath + "/GameBoot.cs";

        // Manager 下的历史落点：老版本 Installer 把 GameBoot.cs 投放在 Game.Managers
        // 程序集目录里。升级后若用户工程仍保留这份文件，优先迁移而不是丢弃——哪怕只是
        // 默认空实现，保住 .meta 里的 GUID 也能让场景上已挂的 GameBoot 组件引用不丢。
        private const string LegacyGameBootAssetPath =
            ManagerTemplateInstaller.ManagerRootAssetPath + "/GameBoot.cs";

        // asmdef 的外部依赖和 Game.Managers 对齐（少一项 Unity.Addressables.Editor，
        // 那是 editor-only、运行时启动不用）。Game.Frame 不引用 Game.Components：
        // GameBoot 的职责约定是调度 Manager，真要触达 Component 也通过 Manager 暴露。
        private const string AsmdefSource =
@"{
    ""name"": ""Game.Frame"",
    ""rootNamespace"": """",
    ""references"": [
        ""DevWorkbench"",
        ""Game.Managers"",
        ""UniTask"",
        ""UniTask.Addressables"",
        ""Unity.Addressables"",
        ""Unity.ResourceManager"",
        ""VContainer""
    ],
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}
";

        private const string GameBootSource =
@"using Cysharp.Threading.Tasks;
using DevWorkbench;
using UnityEngine;
using VContainer;

public class GameBoot : MonoBehaviour, IGameBoot
{
    public async UniTask OnGameStart()
    {
        await UniTask.CompletedTask;
    }
}
";

        // ── 容器 asmdef ───────────────────────────────────────────────────────────

        public static bool IsContainerInstalled() => File.Exists(ToAbsolute(AsmdefAssetPath));

        public static bool EnsureContainerInstalled()
        {
            if (IsContainerInstalled()) return false;

            FrameAssetInstaller.EnsureFolder(FrameRootAssetPath);

            var targetAbs = ToAbsolute(AsmdefAssetPath);
            File.WriteAllText(targetAbs, AsmdefSource);
            AssetDatabase.Refresh();
            Debug.Log("[FrameTemplateInstaller] Game.Frame.asmdef container generated.");
            return true;
        }

        // ── 默认 GameBoot ─────────────────────────────────────────────────────────

        public static bool IsGameBootInstalled() => File.Exists(ToAbsolute(GameBootAssetPath));

        public static bool EnsureGameBootInstalled()
        {
            if (IsGameBootInstalled()) return false;

            FrameAssetInstaller.EnsureFolder(FrameRootAssetPath);

            // 若历史版本已经在 Manager 下生成过 GameBoot.cs，用 AssetDatabase.MoveAsset
            // 迁移；这会同时搬动 .meta 保留 GUID，场景里的 GameBoot MonoBehaviour 引用
            // 不会变成 Missing。无论用户有没有修改过内容，迁移都是安全的（内容保留）。
            if (File.Exists(ToAbsolute(LegacyGameBootAssetPath)))
            {
                var error = AssetDatabase.MoveAsset(LegacyGameBootAssetPath, GameBootAssetPath);
                if (string.IsNullOrEmpty(error))
                {
                    Debug.Log($"[FrameTemplateInstaller] Migrated legacy GameBoot.cs from {LegacyGameBootAssetPath} to {GameBootAssetPath}.");
                    return true;
                }

                Debug.LogWarning($"[FrameTemplateInstaller] MoveAsset failed ({error}); falling back to code generation. Please remove {LegacyGameBootAssetPath} manually if it still exists.");
            }

            var targetAbs = ToAbsolute(GameBootAssetPath);
            File.WriteAllText(targetAbs, GameBootSource);
            AssetDatabase.Refresh();
            Debug.Log("[FrameTemplateInstaller] Default GameBoot.cs generated.");
            return true;
        }

        // ── 工具 ──────────────────────────────────────────────────────────────────

        private static string ToAbsolute(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, assetPath).Replace('\\', '/');
        }
    }
}
