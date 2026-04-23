using System;
using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    internal sealed class FrameworkSyncPage : IPage
    {
        public string GroupTitle => "Framework";
        public string TabTitle => "Sync";

        private const float HPad = 16f;
        private const float VPad = 16f;
        private const float SectionSpacing = 10f;
        private const float CardPad = 14f;
        private const float SyncButtonHeight = 44f;

        private static readonly Color BgColor = new(0.17f, 0.17f, 0.17f);
        private static readonly Color CardBg = new(0.215f, 0.215f, 0.215f);
        private static readonly Color CardBorder = new(0.13f, 0.13f, 0.13f);
        private static readonly Color AccentBlue = new(0.35f, 0.65f, 1f);
        private static readonly Color HeaderTextColor = new(0.78f, 0.84f, 0.94f);
        private static readonly Color DimTextColor = new(0.65f, 0.65f, 0.65f);
        private static readonly Color OkTextColor = new(0.50f, 0.85f, 0.60f);

        private DateTime? _lastRunAt;
        private int _lastRefreshedCount;

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
                DrawLastRunCard();
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
                + "explicit click instead of running on window open.",
                IntroStyle);
            EndCard();
        }

        private void DrawSyncButton()
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = AccentBlue;
            if (GUILayout.Button("Sync Runtime", GUILayout.Height(SyncButtonHeight)))
                PerformSync();
            GUI.backgroundColor = prevBg;
        }

        private void DrawLastRunCard()
        {
            BeginCard();
            DrawHeader("Last run");
            if (_lastRunAt == null)
            {
                GUILayout.Label("Not run yet this session.", IntroStyle);
            }
            else
            {
                GUILayout.Label(
                    $"Last synced at {_lastRunAt.Value:HH:mm:ss}  \u2014  {_lastRefreshedCount} refresher(s) executed.",
                    OkStyle);
            }
            EndCard();
        }

        private void PerformSync()
        {
            DevWindowFrameworkGuard.Ensure();
            _lastRefreshedCount = ManagerConfigInstaller.RunAllRefreshers();
            _lastRunAt = DateTime.Now;

            Debug.Log($"[DevWorkbench] Runtime synced. Refreshers executed: {_lastRefreshedCount}.");
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

        private static GUIStyle _okStyle;
        private static GUIStyle OkStyle => _okStyle ??= new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            wordWrap = true,
            normal = { textColor = OkTextColor },
        };
    }
}
