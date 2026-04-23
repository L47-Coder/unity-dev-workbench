using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    // Framework / Sync —— 运行前手动"把所有东西对齐"的入口。
    //
    // 相对于 DevWindow.Open 自动跑的 DevWindowFrameworkGuard.Ensure()，这里多一步
    // RunAllRefreshers：Refresher 会覆盖每个 ManagerConfig 的 _configs 列表内容，属于
    // "可能破坏用户在 Inspector 里手填数据"的集体同步。所以不放进自动流程，只由这个
    // 页面上的按钮 / 用户自选的自动触发时机显式触发。
    //
    // 自动触发时机的配置和挂钩在 FrameworkSyncSettings；这里只负责 UI。
    internal sealed class FrameworkSyncPage : IPage
    {
        public string GroupTitle => "Framework";
        public string TabTitle => "Sync";

        private const float HPad = 16f;
        private const float VPad = 16f;
        private const float SectionSpacing = 10f;
        private const float CardPad = 14f;
        private const float SyncButtonHeight = 44f;
        private const float RadioRowHeight = 26f;

        private static readonly Color BgColor = new(0.17f, 0.17f, 0.17f);
        private static readonly Color CardBg = new(0.215f, 0.215f, 0.215f);
        private static readonly Color CardBorder = new(0.13f, 0.13f, 0.13f);
        private static readonly Color AccentBlue = new(0.35f, 0.65f, 1f);
        private static readonly Color HeaderTextColor = new(0.78f, 0.84f, 0.94f);
        private static readonly Color DimTextColor = new(0.65f, 0.65f, 0.65f);
        private static readonly Color RadioDescColor = new(0.55f, 0.55f, 0.55f);

        public void OnGUI(Rect rect)
        {
            EditorGUI.DrawRect(rect, BgColor);

            var content = new Rect(rect.x + HPad, rect.y + VPad,
                rect.width - HPad * 2f, rect.height - VPad * 2f);

            GUILayout.BeginArea(content);
            try
            {
                DrawIntroCard();
                GUILayout.Space(SectionSpacing);
                DrawSyncButton();
                GUILayout.Space(SectionSpacing);
                DrawTriggerCard();
                GUILayout.FlexibleSpace();
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private void DrawIntroCard()
        {
            BeginCard();
            DrawHeader("Sync Runtime");
            GUILayout.Label(
                "Ensure the framework's scaffolding assets exist and run every IManagerRefresher once. "
                + "Refreshers overwrite each ManagerConfig's _configs list, so this is kept behind an "
                + "explicit trigger instead of running on window open.",
                IntroStyle);
            EndCard();
        }

        private static void DrawSyncButton()
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = AccentBlue;
            if (GUILayout.Button("Sync Runtime", GUILayout.Height(SyncButtonHeight)))
                FrameworkSyncSettings.RunSync("manual");
            GUI.backgroundColor = prevBg;
        }

        private void DrawTriggerCard()
        {
            BeginCard();
            DrawHeader("Auto trigger");
            GUILayout.Label(
                "Choose when the sync should also run automatically. The button above is always available regardless of this choice.",
                IntroStyle);
            GUILayout.Space(6f);

            var current = FrameworkSyncSettings.Trigger;
            current = DrawRadio(current, FrameworkSyncTrigger.Manual,
                "Manual only",
                "Only run when the Sync Runtime button above is clicked.");
            current = DrawRadio(current, FrameworkSyncTrigger.OnWorkbenchClose,
                "When Dev Workbench closes",
                "Run once each time this window is closed.");
            current = DrawRadio(current, FrameworkSyncTrigger.BeforePlayMode,
                "Before entering Play Mode",
                "Run once right before the editor enters Play Mode.");

            if (current != FrameworkSyncSettings.Trigger)
                FrameworkSyncSettings.Trigger = current;

            EndCard();
        }

        private static FrameworkSyncTrigger DrawRadio(
            FrameworkSyncTrigger current, FrameworkSyncTrigger value, string label, string desc)
        {
            var row = GUILayoutUtility.GetRect(0f, RadioRowHeight, GUILayout.ExpandWidth(true));
            var selected = current == value;

            // Unity 有现成的 radio，但我们在 Rect 模式里手绘一下更可控；这里直接用
            // EditorGUI.Toggle+自定义命中区，行为等价：点整行都切过来。
            var toggleRect = new Rect(row.x + 4f, row.y + (row.height - 16f) * 0.5f, 16f, 16f);
            var labelRect = new Rect(toggleRect.xMax + 8f, row.y, row.width - (toggleRect.xMax + 8f - row.x), row.height);

            if (EditorGUI.Toggle(toggleRect, selected, EditorStyles.radioButton) && !selected)
                current = value;

            EditorGUI.LabelField(labelRect, label, RadioLabelStyle);

            // 描述一行，单独占一行以免挤在同一行截断。
            var descRect = GUILayoutUtility.GetRect(0f, 16f, GUILayout.ExpandWidth(true));
            var descIndented = new Rect(descRect.x + 28f, descRect.y, descRect.width - 28f, descRect.height);
            EditorGUI.LabelField(descIndented, desc, RadioDescStyle);
            GUILayout.Space(4f);

            // 点整行（非 toggle 自身）也切——比只能命中那 16px 的 box 友好得多。
            if (Event.current.type == EventType.MouseDown
                && (row.Contains(Event.current.mousePosition) || descRect.Contains(Event.current.mousePosition))
                && !toggleRect.Contains(Event.current.mousePosition))
            {
                current = value;
                GUI.FocusControl(null);
                Event.current.Use();
            }

            return current;
        }

        // ── Chrome ────────────────────────────────────────────────────────────────

        private static void BeginCard()
        {
            var rect = EditorGUILayout.BeginVertical();
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, CardBg);
                DrawOutline(rect, CardBorder);
            }
            GUILayout.Space(CardPad);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(CardPad);
            EditorGUILayout.BeginVertical();
        }

        private static void EndCard()
        {
            EditorGUILayout.EndVertical();
            GUILayout.Space(CardPad);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(CardPad);
            EditorGUILayout.EndVertical();
        }

        private static void DrawHeader(string title)
        {
            GUILayout.Label(title, HeaderStyle);
            GUILayout.Space(3f);
            var line = GUILayoutUtility.GetRect(0f, 2f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(line, new Color(0.30f, 0.30f, 0.30f));
            GUILayout.Space(10f);
        }

        private static void DrawOutline(Rect r, Color c)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }

        // ── Styles ────────────────────────────────────────────────────────────────

        private static GUIStyle _headerStyle;
        private static GUIStyle HeaderStyle => _headerStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            normal = { textColor = HeaderTextColor },
        };

        private static GUIStyle _introStyle;
        private static GUIStyle IntroStyle => _introStyle ??= new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            wordWrap = true,
            normal = { textColor = DimTextColor },
        };

        private static GUIStyle _radioLabelStyle;
        private static GUIStyle RadioLabelStyle => _radioLabelStyle ??= new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.92f, 0.92f, 0.92f) },
        };

        private static GUIStyle _radioDescStyle;
        private static GUIStyle RadioDescStyle => _radioDescStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 11,
            alignment = TextAnchor.UpperLeft,
            wordWrap = false,
            normal = { textColor = RadioDescColor },
        };
    }
}
