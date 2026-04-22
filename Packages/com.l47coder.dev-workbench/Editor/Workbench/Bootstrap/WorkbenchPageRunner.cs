using System;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // 批量调用所有 IPage 的 OnWorkbenchOpen。三处入口复用：
    //   1. DevWindow.OnEnable（一会话一次，SessionState 压）；
    //   2. FrameworkBootstrapper 的 domain reload rerun（Installer 装完模板后）；
    //   3. Tools/Dev Workbench/Sync Runtime 菜单。
    //
    // 这里独立反射实例化 Page，不复用 DevWindow 里的那批——OnWorkbenchOpen 的契约是
    // "纯静态动作（只读/写项目资产，不依赖 UI 字段）"，新建一次性实例调完就丢弃完全安全。
    // 这么做也使菜单/rerun 路径不依赖"窗口已打开"。
    internal static class WorkbenchPageRunner
    {
        public static void RunOnWorkbenchOpenForAll()
        {
            foreach (var type in TypeCache.GetTypesDerivedFrom<IPage>())
            {
                if (type.IsAbstract || type.IsInterface) continue;

                IPage page;
                try
                {
                    page = Activator.CreateInstance(type) as IPage;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[WorkbenchPageRunner] Failed to instantiate {type.FullName}: {ex.Message}");
                    continue;
                }

                if (page == null) continue;

                try
                {
                    page.OnWorkbenchOpen();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[WorkbenchPageRunner] {type.Name}.OnWorkbenchOpen threw: {ex}");
                }
            }
        }
    }
}
