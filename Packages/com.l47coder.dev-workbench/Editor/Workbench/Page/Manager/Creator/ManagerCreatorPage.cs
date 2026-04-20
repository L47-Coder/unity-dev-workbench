using UnityEditor;
using UnityEngine;

namespace DevWorkbench.Editor
{
    internal sealed class ManagerCreatorPage : IPage
    {
        public string GroupTitle => "Manager";
        public string TabTitle => "Creator";

        private const float HPad = 16f;
        private const float VPad = 16f;
        private const float SectionSpacing = 10f;
        private const float CardPad = 14f;
        private const float CreateButtonHeight = 38f;
        private const float LabelWidth = 150f;
        private const float IncludeConfigCardHeight = 42f;
        private const float DotSize = 8f;
        private const float DotLeftPad = 4f;
        private const float FieldSpacing = 4f;

        private static readonly Color BgColor = new(0.17f, 0.17f, 0.17f);
        private static readonly Color CardBg = new(0.215f, 0.215f, 0.215f);
        private static readonly Color CardBorder = new(0.13f, 0.13f, 0.13f);
        private static readonly Color AccentBlue = new(0.35f, 0.65f, 1f);
        private static readonly Color HeaderTextColor = new(0.78f, 0.84f, 0.94f);
        private static readonly Color ValueBg = new(0.15f, 0.15f, 0.15f);
        private static readonly Color ValueBorder = new(0.11f, 0.11f, 0.11f);
        private static readonly Color CreateColor = new(0.29f, 0.78f, 0.40f);
        private static readonly Color WriteColor = new(0.95f, 0.75f, 0.22f);
        private static readonly Color SkipColor = new(0.85f, 0.35f, 0.35f);
        private static readonly Color NeutralColor = new(0.50f, 0.50f, 0.50f);
        private static readonly Color ConfigCardOnBg = new(0.16f, 0.33f, 0.22f);
        private static readonly Color ConfigCardOffBg = new(0.23f, 0.23f, 0.23f);
        private static readonly Color ConfigCardOnBorder = new(0.30f, 0.78f, 0.42f);
        private static readonly Color ConfigCardOffBorder = new(0.36f, 0.36f, 0.36f);

        private readonly ManagerCreatorState _state = new();
        private Vector2 _scroll;
        private bool _isInitialized;
        private GUIStyle _configTitleStyle;
        private GUIStyle _configDescStyle;

        public void OnEnter()
        {
            if (!_isInitialized)
            {
                _state.Reset();
                _isInitialized = true;
                return;
            }
            _state.RefreshDerivedState();
        }

        public void OnLeave()
        {
            _scroll = Vector2.zero;
            _state.Reset();
            _isInitialized = false;
        }

        public void OnGUI(Rect rect)
        {
            EditorGUI.DrawRect(rect, BgColor);

            var content = new Rect(rect.x + HPad, rect.y + VPad,
                rect.width - HPad * 2f, rect.height - VPad * 2f);

            GUILayout.BeginArea(content);
            var prevLW = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelWidth;
            try
            {
                DrawInputSection();
                GUILayout.Space(SectionSpacing);

                if (_state.HasPreview)
                    DrawPreviewSections();

                GUILayout.FlexibleSpace();
                GUILayout.Space(SectionSpacing);
                DrawCreateButton();
            }
            finally
            {
                EditorGUIUtility.labelWidth = prevLW;
                GUILayout.EndArea();
            }
        }

        // ── Input section ─────────────────────────────────────────────────────────

        private void DrawInputSection()
        {
            BeginCard();
            DrawHeader("Input");

            var newName = DrawEditableField("Manager name", _state.InputManagerName, _state.GetInputStatus());
            if (newName != _state.InputManagerName)
                _state.SetInputManagerName(newName);

            GUILayout.Space(4f);
            DrawIncludeConfigCard();

            if (!string.IsNullOrEmpty(_state.ErrorMessage))
            {
                GUILayout.Space(8f);
                EditorGUILayout.HelpBox(_state.ErrorMessage, MessageType.Warning);
            }

            EndCard();
        }

        // ── Preview sections ──────────────────────────────────────────────────────

        private void DrawPreviewSections()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            try
            {
                DrawPreviewCard("Type names", _state.GetNamePreviewItems());
                GUILayout.Space(SectionSpacing);
                DrawPreviewCard("Output paths", _state.GetPathPreviewItems());
                if (_state.IncludeConfig)
                {
                    GUILayout.Space(SectionSpacing);
                    DrawPreviewCard("Addressables", _state.GetAddressablePreviewItems());
                }
                GUILayout.Space(SectionSpacing + 2f);
                DrawLegendRow();
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private static void DrawPreviewCard(string title, params ManagerCreatorState.PreviewItem[] items)
        {
            BeginCard();
            DrawHeader(title);
            for (var i = 0; i < items.Length; i++)
            {
                if (i > 0) GUILayout.Space(FieldSpacing);
                DrawReadonlyField(items[i].Label, items[i].Value, items[i].Status);
            }
            EndCard();
        }

        // ── Include config card ───────────────────────────────────────────────────

        private void DrawIncludeConfigCard()
        {
            var rect = GUILayoutUtility.GetRect(0f, IncludeConfigCardHeight, GUILayout.ExpandWidth(true));
            var isOn = _state.IncludeConfig;

            EditorGUI.DrawRect(rect, isOn ? ConfigCardOnBg : ConfigCardOffBg);
            DrawOutline(rect, isOn ? ConfigCardOnBorder : ConfigCardOffBorder);

            var titleRect = new Rect(rect.x + 10f, rect.y + 5f, rect.width - 72f, 18f);
            var descRect = new Rect(rect.x + 10f, rect.y + 21f, rect.width - 72f, 16f);
            var toggleRect = new Rect(rect.xMax - 44f,
                rect.y + (rect.height - EditorGUIUtility.singleLineHeight) * 0.5f,
                34f, EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(titleRect, "Include config", ConfigTitleStyle);
            EditorGUI.LabelField(descRect,
                isOn
                    ? "Generates the config, data and manager partials under Generated/, plus the config asset and Addressables entry."
                    : "Generates the main script and the manager partial stub under Generated/, without a config asset.",
                ConfigDescStyle);

            var next = EditorGUI.Toggle(toggleRect, isOn);
            if (next != isOn)
                _state.SetIncludeConfig(next);

            if (Event.current.type == EventType.MouseDown &&
                rect.Contains(Event.current.mousePosition) &&
                !toggleRect.Contains(Event.current.mousePosition))
            {
                _state.SetIncludeConfig(!isOn);
                GUI.FocusControl(null);
                Event.current.Use();
            }
        }

        // ── Create button ─────────────────────────────────────────────────────────

        private void DrawCreateButton()
        {
            var prevBg = GUI.backgroundColor;
            if (_state.IsValid)
                GUI.backgroundColor = AccentBlue;

            using (new EditorGUI.DisabledScope(!_state.IsValid))
            {
                if (GUILayout.Button("Create Manager", GUILayout.Height(CreateButtonHeight)))
                {
                    ManagerCreationService.CreateManager(_state);
                    _state.Reset();
                    _scroll = Vector2.zero;
                    GUI.FocusControl(null);
                }
            }

            GUI.backgroundColor = prevBg;
        }

        // ── Field drawing ─────────────────────────────────────────────────────────

        private static string DrawEditableField(string label, string value,
            ManagerCreatorState.PreviewStatus status)
        {
            var r = EditorGUILayout.GetControlRect(false, 20f);
            DrawFieldLabel(r, label, status);
            var valueRect = new Rect(r.x + EditorGUIUtility.labelWidth + 2f, r.y,
                r.width - EditorGUIUtility.labelWidth - 2f, r.height);
            return EditorGUI.TextField(valueRect, value ?? string.Empty, InputFieldStyle);
        }

        private static void DrawReadonlyField(string label, string value,
            ManagerCreatorState.PreviewStatus status)
        {
            var r = EditorGUILayout.GetControlRect(false, 20f);
            DrawFieldLabel(r, label, status);
            var valueRect = new Rect(r.x + EditorGUIUtility.labelWidth + 2f, r.y,
                r.width - EditorGUIUtility.labelWidth - 2f, r.height);

            EditorGUI.DrawRect(valueRect, ValueBg);
            DrawOutline(valueRect, ValueBorder);
            var textRect = new Rect(valueRect.x + 6f, valueRect.y,
                valueRect.width - 12f, valueRect.height);
            EditorGUI.LabelField(textRect, value ?? string.Empty, ValueLabelStyle);
        }

        private static void DrawFieldLabel(Rect fullRect, string label,
            ManagerCreatorState.PreviewStatus status)
        {
            var dotRect = new Rect(
                fullRect.x + DotLeftPad,
                fullRect.y + (fullRect.height - DotSize) * 0.5f,
                DotSize, DotSize);
            DrawDot(dotRect, StatusColor(status));

            var labelRect = new Rect(
                dotRect.xMax + 6f, fullRect.y,
                EditorGUIUtility.labelWidth - DotSize - DotLeftPad - 6f, fullRect.height);
            EditorGUI.LabelField(labelRect, label);
        }

        private static Color StatusColor(ManagerCreatorState.PreviewStatus s) => s switch
        {
            ManagerCreatorState.PreviewStatus.Create => CreateColor,
            ManagerCreatorState.PreviewStatus.Write => WriteColor,
            ManagerCreatorState.PreviewStatus.Skip => SkipColor,
            _ => NeutralColor,
        };

        // ── Card / chrome ─────────────────────────────────────────────────────────

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

        private static GUIStyle _headerStyle;
        private static GUIStyle HeaderStyle => _headerStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            normal = { textColor = HeaderTextColor },
        };

        private static GUIStyle _inputFieldStyle;
        private static GUIStyle InputFieldStyle => _inputFieldStyle ??= new GUIStyle(EditorStyles.textField)
        {
            alignment = TextAnchor.MiddleLeft,
        };

        private static GUIStyle _valueLabelStyle;
        private static GUIStyle ValueLabelStyle => _valueLabelStyle ??= new GUIStyle(EditorStyles.label)
        {
            fontSize = 11,
            normal = { textColor = new Color(0.55f, 0.55f, 0.55f) },
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip,
        };

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

        // ── Round dot ─────────────────────────────────────────────────────────────

        private static Texture2D _circleTex;
        private static Texture2D CircleTex
        {
            get
            {
                if (_circleTex != null) return _circleTex;
                const int sz = 16;
                _circleTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };
                var center = new Vector2((sz - 1) * 0.5f, (sz - 1) * 0.5f);
                var radius = sz * 0.5f;
                for (var y = 0; y < sz; y++)
                    for (var x = 0; x < sz; x++)
                    {
                        var dist = Vector2.Distance(new Vector2(x, y), center);
                        _circleTex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - dist)));
                    }
                _circleTex.Apply();
                return _circleTex;
            }
        }

        private static void DrawDot(Rect rect, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, CircleTex);
            GUI.color = prev;
        }

        // ── Legend ─────────────────────────────────────────────────────────────────

        private static readonly (Color color, string label)[] LegendItems =
        {
        (CreateColor, "Create"),
        (WriteColor,  "Write"),
        (SkipColor,   "Skip"),
    };

        private static void DrawLegendRow()
        {
            var r = EditorGUILayout.GetControlRect(false, 16f);
            var x = r.x + 2f;
            foreach (var (color, label) in LegendItems)
            {
                var dotRect = new Rect(x, r.y + (r.height - DotSize) * 0.5f, DotSize, DotSize);
                DrawDot(dotRect, color);
                x += DotSize + 4f;
                var size = EditorStyles.miniLabel.CalcSize(new GUIContent(label));
                EditorGUI.LabelField(new Rect(x, r.y, size.x, r.height), label, EditorStyles.miniLabel);
                x += size.x + 16f;
            }
        }

        // ── Lazy styles ───────────────────────────────────────────────────────────

        private GUIStyle ConfigTitleStyle => _configTitleStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleLeft,
        };

        private GUIStyle ConfigDescStyle => _configDescStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
        };
    }
}
