using System.IO;
using UnityEditor;
using UnityEngine;
using DevWorkbench;

namespace DevWorkbench.Editor
{

// 首次加载框架包时，把 Runtime~/DefaultManagers/ 下的三套默认 Manager 模板
// 拷贝到 Assets/Game/Manager/。从此这三个 Manager 与 Creator 生成的用户 Manager 一样：
// 都住在 Assets 下、都被 Game.Managers.asmdef 接管、都可被用户自由阅读和修改。
//
// 幂等判据：Assets/Game/Manager/Game.Managers.asmdef 是否已存在。
// 该 asmdef 是投放内容本身的一部分，它在 = 程序集已建立 = 默认 Manager 已投放；
// 用它当标记就不用再维护额外的"状态证明文件"。
//
// 注意：Unity 会忽略任何以 `~` 结尾的目录，所以包里 Runtime~ 的内容
// 不会被 Unity 当作 asset 编译，这正是"模板仓"该有的行为。
internal static class DefaultManagerInstaller
{
    // 包里的源模板目录。以 package:// 形式写死，只要用户不改包名就能拿到。
    private const string PackageSourceRelative =
        "Packages/com.l47coder.dev-workbench/Runtime~/DefaultManagers";

    // 投放根目录（= Creator 生成用户 Manager 的同一个根目录）。
    public const string ManagerRootAssetPath = "Assets/Game/Manager";

    // 投放完成的判据文件（asmdef 是默认投放内容的关键产物之一）。
    public const string AsmdefAssetPath = "Assets/Game/Manager/Game.Managers.asmdef";

    // 触发 Bootstrap 在下次 domain reload 后继续跑的会话标记。投放完后 Unity 会重新编译，
    // 此时旧 AppDomain 还不认识三个默认 ManagerConfig，所以 InitializeAll 后半段（建 asset、
    // 注册 Addressable、跑 Refresher）必须等到编译完再跑一次。
    internal const string SessionKeyRerunInitialize = "DevWorkbench.Bootstrapper.RerunInitialize";

    public static bool IsInstalled()
    {
        var absPath = ToAbsolute(AsmdefAssetPath);
        return File.Exists(absPath);
    }

    public static bool EnsureInstalled()
    {
        if (IsInstalled()) return false;

        var sourceAbs = ResolveSourceAbsolute();
        if (string.IsNullOrEmpty(sourceAbs) || !Directory.Exists(sourceAbs))
        {
            Debug.LogError($"[DefaultManagerInstaller] Template source folder not found: {PackageSourceRelative}");
            return false;
        }

        FrameAssetInstaller.EnsureFolder(ManagerRootAssetPath);

        var targetAbs = ToAbsolute(ManagerRootAssetPath);
        CopyDirectory(sourceAbs, targetAbs);

        // 告诉 Bootstrapper：下一次编译结束后要重跑一遍 InitializeAll，把三个 asset 建上。
        SessionState.SetBool(SessionKeyRerunInitialize, true);

        AssetDatabase.Refresh();
        Debug.Log("[DefaultManagerInstaller] Deployed the three default Managers under Assets/Game/Manager/.");
        return true;
    }

    private static void CopyDirectory(string sourceAbs, string targetAbs)
    {
        if (!Directory.Exists(targetAbs))
            Directory.CreateDirectory(targetAbs);

        foreach (var file in Directory.GetFiles(sourceAbs))
        {
            var name = Path.GetFileName(file);
            var dest = Path.Combine(targetAbs, name);
            File.Copy(file, dest, overwrite: false);
        }

        foreach (var sub in Directory.GetDirectories(sourceAbs))
        {
            var name = Path.GetFileName(sub);
            CopyDirectory(sub, Path.Combine(targetAbs, name));
        }
    }

    private static string ResolveSourceAbsolute()
    {
        try { return Path.GetFullPath(PackageSourceRelative); }
        catch { return null; }
    }

    private static string ToAbsolute(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return null;
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.Combine(projectRoot, assetPath).Replace('\\', '/');
    }
}
}
