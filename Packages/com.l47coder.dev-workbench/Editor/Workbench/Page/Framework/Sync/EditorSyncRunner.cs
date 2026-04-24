using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    /// <summary>
    /// 扫描并运行所有带 <see cref="EditorSyncAttribute"/> 的无参静态方法。
    /// 原来 IManagerRefresher 的"按类型 → 反推配对 Refresher"链路被替换为
    /// "按 attribute 直接发现入口"——Refresher 不再绑定一个 Config，纯粹是
    /// 一段 editor-only 的同步逻辑。
    /// </summary>
    internal static class EditorSyncRunner
    {
        public readonly struct Entry
        {
            public readonly MethodInfo Method;
            public readonly int Order;
            public Entry(MethodInfo method, int order) { Method = method; Order = order; }
            public string DisplayName => $"{Method.DeclaringType?.FullName}.{Method.Name}";
        }

        public static int RunAll()
        {
            var entries = CollectEntries();
            entries.Sort((a, b) => a.Order.CompareTo(b.Order));

            var executed = 0;
            foreach (var entry in entries)
            {
                try
                {
                    entry.Method.Invoke(null, null);
                    executed++;
                }
                catch (TargetInvocationException tex)
                {
                    Debug.LogError($"[EditorSync] {entry.DisplayName} threw: {tex.InnerException ?? tex}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EditorSync] {entry.DisplayName} failed to invoke: {ex}");
                }
            }
            return executed;
        }

        public static List<Entry> CollectEntries()
        {
            var result = new List<Entry>();
            foreach (var method in TypeCache.GetMethodsWithAttribute<EditorSyncAttribute>())
            {
                if (method == null) continue;
                if (!method.IsStatic)
                {
                    Debug.LogWarning($"[EditorSync] {method.DeclaringType?.FullName}.{method.Name} skipped: must be static.");
                    continue;
                }
                if (method.GetParameters().Length != 0)
                {
                    Debug.LogWarning($"[EditorSync] {method.DeclaringType?.FullName}.{method.Name} skipped: must be parameterless.");
                    continue;
                }
                if (method.ReturnType != typeof(void))
                {
                    Debug.LogWarning($"[EditorSync] {method.DeclaringType?.FullName}.{method.Name} skipped: must return void.");
                    continue;
                }

                var attr = method.GetCustomAttribute<EditorSyncAttribute>();
                result.Add(new Entry(method, attr?.Order ?? 0));
            }
            return result;
        }
    }
}
