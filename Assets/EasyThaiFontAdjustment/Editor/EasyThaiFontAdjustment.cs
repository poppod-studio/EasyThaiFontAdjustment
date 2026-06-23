// Assets/EasyThaiFontAdjustment/Editor/EasyThaiFontAdjustment.cs
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

#if TMP_3_2_0_PRE_2 || UGUI_2_0
using GlyphAdjustmentRecord = UnityEngine.TextCore.LowLevel.GlyphAdjustmentRecord;
using GlyphPairAdjustmentRecord = UnityEngine.TextCore.LowLevel.GlyphPairAdjustmentRecord;
using GlyphValueRecord = UnityEngine.TextCore.LowLevel.GlyphValueRecord;
#else
using GlyphAdjustmentRecord = TMPro.TMP_GlyphAdjustmentRecord;
using GlyphPairAdjustmentRecord = TMPro.TMP_GlyphPairAdjustmentRecord;
using GlyphValueRecord = TMPro.TMP_GlyphValueRecord;
#endif

namespace EasyThaiFont
{
    /// <summary>
    /// Editor-only tool that fixes floating Thai vowels and tone marks in TextMeshPro
    /// font assets. It generates Glyph Pair Adjustment Records (kerning) for every
    /// problematic Thai character combination and writes them into the selected
    /// <see cref="TMP_FontAsset"/>. Adjustment values are calculated automatically from
    /// the font's metrics (point size and scale).
    ///
    /// Access via: Tools &gt; Easy Thai Font Adjustment.
    /// </summary>
    public class EasyThaiFontAdjustment : EditorWindow
    {
        // ===================== State =====================

        TMP_FontAsset fontAsset;

        string sampleText = @"ปิ่น อื้อ จี๊ด อื้ม ปรื๋อ ผื่น ลิ้น
ติ๋ม ปริ่ม หั่น ปั้น ตั๊ก ป้า ม๊า
ฝ่า ป่า ฟ้า ผ่า ผ้า จ๋า สิทธิ์
ย่ำ ถ้ำ ฎุ ฎูำ";

        Vector2 scrollPosition;
        readonly List<AdjustmentRule> adjustmentRules = new List<AdjustmentRule>();
        int selectedTab = 0;
        readonly string[] tabNames = { "Templates", "Custom", "Rules" };

        readonly Dictionary<string, bool> categoryFoldouts = new Dictionary<string, bool>();
        readonly Dictionary<string, PresetConfig> presetConfigs = new Dictionary<string, PresetConfig>();

        // One-click pipeline state.
        bool showAdvancedOptions = false;
        bool isProcessing = false;
        float progress = 0f;
        string progressMessage = "";

        // Undo backup: snapshot of the pair-adjustment table before the last apply.
        List<GlyphPairAdjustmentRecord> backupRecords = null;
        TMP_FontAsset lastModifiedFont = null;

        // Cached GUIStyles, built once on first OnGUI (avoids per-frame allocation).
        bool stylesInitialized = false;
        GUIStyle headerStyle, oneClickTitleStyle, bigButtonStyle, infoStyle, ruleLabelStyle;

        // Thai-glyph coverage of the selected font, recomputed when the font changes.
        // -1 = not checked; otherwise the number of base consonants (out of 44) present.
        int thaiConsonantCoverage = -1;

        // EditorPrefs keys for persisting the user's last session.
        const string PrefFont = "EasyThaiFontAdjustment.LastFontPath";
        const string PrefSampleText = "EasyThaiFontAdjustment.SampleText";
        const string PrefShowAdvanced = "EasyThaiFontAdjustment.ShowAdvanced";

        // ===================== Data Types =====================

        /// <summary>
        /// A single character-pair kerning rule: the two glyphs and the X/Y placement
        /// offset applied to the second glyph. <see cref="selected"/> controls whether
        /// it is written to the font asset on Apply.
        /// </summary>
        [System.Serializable]
        class AdjustmentRule
        {
            public string ruleName;
            public char firstChar;
            public char secondChar;
            public float xPlacement;
            public float yPlacement;
            public bool selected = true;
            public string category;

            public AdjustmentRule(string name, char first, char second, float x, float y, string cat = "")
            {
                ruleName = name;
                firstChar = first;
                secondChar = second;
                xPlacement = x;
                yPlacement = y;
                category = cat;
            }

            public string DisplayName => $"{firstChar} + {secondChar}";

            /// <summary>Unique key for de-duplication: the ordered glyph pair.</summary>
            public string Key => $"{firstChar}_{secondChar}";
        }

        /// <summary>
        /// Configuration for one template (a whole group of pairs sharing the same
        /// default offset). The value actually applied is <see cref="FinalX"/> /
        /// <see cref="FinalY"/> (default plus user offset).
        /// </summary>
        [System.Serializable]
        class PresetConfig
        {
            public string name;
            public string description;
            public float defaultX;
            public float defaultY;
            public float offsetX = 0f; // Extra offset added on top of the calculated value.
            public float offsetY = 0f;
            public Color color;
            public bool expanded = false;

            public PresetConfig(string n, string desc, float x, float y, Color c)
            {
                name = n;
                description = desc;
                defaultX = x;
                defaultY = y;
                color = c;
            }

            public float FinalX => defaultX + offsetX;
            public float FinalY => defaultY + offsetY;
        }

        // ===================== Thai Character Sets =====================

        static readonly char[] allConsonants =
        {
            'ก', 'ข', 'ฃ', 'ค', 'ฅ', 'ฆ', 'ง', 'จ', 'ฉ', 'ช',
            'ซ', 'ฌ', 'ญ', 'ฎ', 'ฏ', 'ฐ', 'ฑ', 'ฒ', 'ณ', 'ด',
            'ต', 'ถ', 'ท', 'ธ', 'น', 'บ', 'ป', 'ผ', 'ฝ', 'พ',
            'ฟ', 'ภ', 'ม', 'ย', 'ร', 'ล', 'ว', 'ศ', 'ษ', 'ส',
            'ห', 'ฬ', 'อ', 'ฮ'
        };

        // Consonants with a tall ascender that collide harder with upper marks.
        static readonly char[] ascenderConsonants = { 'ป', 'ฝ', 'ฟ', 'ฬ' };
        // Consonants with a lower descender that collide with lower vowels.
        static readonly char[] descenderConsonants = { 'ฎ', 'ฏ' };

        static readonly char[] upperVowels = { 'ิ', 'ี', 'ึ', 'ื', '็', 'ั' };
        static readonly char[] lowerVowels = { 'ุ', 'ู' };
        static readonly char saraAm = 'ำ';
        static readonly char[] toneMarks = { '่', '้', '๊', '๋' };
        static readonly char thanThaKhaat = '์';

        // ===================== Window Lifecycle =====================

        /// <summary>Opens (or focuses) the Easy Thai Font Adjustment window.</summary>
        [MenuItem("Tools/Easy Thai Font Adjustment")]
        public static void ShowWindow()
        {
            var window = GetWindow<EasyThaiFontAdjustment>("Easy Thai Font Adjustment");
            window.minSize = new Vector2(600, 700);
        }

        /// <summary>
        /// Right-click entry on a TMP Font Asset (Inspector header) that opens the window
        /// pre-loaded with that font.
        /// </summary>
        [MenuItem("CONTEXT/TMP_FontAsset/Easy Thai Font Adjustment")]
        static void ShowWindowForFontAsset(MenuCommand command)
        {
            var window = GetWindow<EasyThaiFontAdjustment>("Easy Thai Font Adjustment");
            window.minSize = new Vector2(600, 700);
            window.SetFont(command.context as TMP_FontAsset);
        }

        void OnEnable()
        {
            LoadPreferences();
            InitializePresets();
            CheckFontCoverage();
        }

        void OnDisable()
        {
            SavePreferences();
        }

        /// <summary>Selects a font and refreshes presets/coverage. Used by the context menu.</summary>
        public void SetFont(TMP_FontAsset font)
        {
            if (font == null) return;
            fontAsset = font;
            adjustmentRules.Clear();
            InitializePresets();
            CheckFontCoverage();
            Repaint();
        }

        // ===================== Preset Initialization & Value Calculation =====================

        /// <summary>
        /// Rebuilds all template configs, recalculating the default adjustment values
        /// from the currently selected font's metrics.
        /// </summary>
        void InitializePresets()
        {
            presetConfigs.Clear();

            float baseAdjustment = CalculateBaseAdjustment();
            float upperToneAdjustment = CalculateUpperToneAdjustment();
            float ascenderAdjustment = CalculateAscenderAdjustment();

            presetConfigs["consonant_upper"] = new PresetConfig(
                "Consonant + Upper Vowel (พยัญชนะ + สระบน)",
                "All consonants (ก-ฮ) + upper vowels (ิ ี ึ ื ั ็)",
                0f, baseAdjustment,
                new Color(0.2f, 0.6f, 1f, 0.3f));

            presetConfigs["consonant_tone"] = new PresetConfig(
                "Consonant + Tone Mark (พยัญชนะ + วรรณยุกต์)",
                "All consonants (ก-ฮ) + tone marks (่ ้ ๊ ๋)",
                0f, baseAdjustment,
                new Color(1f, 0.6f, 0.2f, 0.3f));

            presetConfigs["consonant_thanthakhaat"] = new PresetConfig(
                "Consonant + Thanthakhat (พยัญชนะ + ทัณฑฆาต ์)",
                "All consonants (ก-ฮ) + ์",
                0f, baseAdjustment,
                new Color(0.6f, 0.2f, 1f, 0.3f));

            presetConfigs["upper_tone"] = new PresetConfig(
                "Upper Vowel + Tone Mark (สระบน + วรรณยุกต์)",
                "Upper vowels (ิ ี ึ ื ั ็) + tone marks (่ ้ ๊ ๋)",
                0f, upperToneAdjustment, // Special value for stacked upper-vowel + tone.
                new Color(1f, 0.3f, 0.5f, 0.3f));

            presetConfigs["upper_thanthakhaat"] = new PresetConfig(
                "Upper Vowel + Thanthakhat (สระบน + ทัณฑฆาต ์)",
                "Upper vowels (ิ ี ึ ื ั ็) + ์",
                0f, upperToneAdjustment,
                new Color(0.5f, 1f, 0.3f, 0.3f));

            presetConfigs["sara_am_tone"] = new PresetConfig(
                "Sara Am + Tone Mark (สระอำ + วรรณยุกต์)",
                "Sara Am (ำ) + tone marks (่ ้ ๊ ๋)",
                0f, upperToneAdjustment, // Same value as upper-vowel + tone.
                new Color(1f, 0.5f, 1f, 0.3f));

            presetConfigs["ascender_upper"] = new PresetConfig(
                "Ascender Consonant + Mark (พยัญชนะหางบน)",
                "Ascender consonants (ป ฝ ฟ ฬ) + upper vowel/tone — stronger offset",
                0f, ascenderAdjustment, // Special value for ascender consonants.
                new Color(1f, 0.2f, 0.2f, 0.3f));

            presetConfigs["descender_lower"] = new PresetConfig(
                "Descender Consonant + Lower Vowel (พยัญชนะหางล่าง)",
                "Descender consonants (ฎ ฏ) + lower vowels (ุ ู)",
                0f, 2f,
                new Color(0.2f, 1f, 0.8f, 0.3f));
        }

        /// <summary>Base offset for consonant + upper mark: -2% of point size.</summary>
        float CalculateBaseAdjustment()
        {
            if (fontAsset == null) return -2f; // Default for point size 100.

            float pointSize = fontAsset.faceInfo.pointSize;
            float scale = fontAsset.faceInfo.scale;
            float adjustment = -(pointSize * 0.02f * scale);
            return Mathf.Round(adjustment * 100f) / 100f;
        }

        /// <summary>Offset for stacked upper-vowel + tone: 19.4% of point size.</summary>
        float CalculateUpperToneAdjustment()
        {
            if (fontAsset == null) return 19.4f; // Default for point size 100.

            float pointSize = fontAsset.faceInfo.pointSize;
            float scale = fontAsset.faceInfo.scale;
            float adjustment = (pointSize * 0.194f * scale);
            return Mathf.Round(adjustment * 100f) / 100f;
        }

        /// <summary>Stronger offset for ascender consonants (ป ฝ ฟ ฬ): -3.5% of point size.</summary>
        float CalculateAscenderAdjustment()
        {
            if (fontAsset == null) return -3.5f; // Default for point size 100.

            float pointSize = fontAsset.faceInfo.pointSize;
            float scale = fontAsset.faceInfo.scale;
            float adjustment = -(pointSize * 0.035f * scale);
            return Mathf.Round(adjustment * 100f) / 100f;
        }

        // ===================== GUI =====================

        /// <summary>Builds and caches all GUIStyles once (OnGUI runs every frame).</summary>
        void EnsureStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };

            oneClickTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };

            bigButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(20, 20, 15, 15)
            };

            infoStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                fontSize = 11
            };

            ruleLabelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 14
            };

            stylesInitialized = true;
        }

        void OnGUI()
        {
            EnsureStyles();

            EditorGUILayout.Space(10);

            // Header.
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Easy Thai Font Adjustment", headerStyle, GUILayout.Height(30));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("Easily fix Thai vowel and tone-mark positioning in TextMeshPro fonts.", MessageType.Info);

            EditorGUILayout.Space(5);
            DrawLine();
            EditorGUILayout.Space(5);

            // Font asset selection.
            TMP_FontAsset newFontAsset = EditorGUILayout.ObjectField("Font Asset", fontAsset, typeof(TMP_FontAsset), false) as TMP_FontAsset;

            if (newFontAsset != fontAsset)
            {
                fontAsset = newFontAsset;
                adjustmentRules.Clear();

                // Recalculate template defaults and coverage for the new font.
                if (fontAsset != null)
                    InitializePresets();
                CheckFontCoverage();
            }

            if (fontAsset == null)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox("Please select a Font Asset first.", MessageType.Warning);
                return;
            }

            // Compact font-metrics readout.
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Point Size: {fontAsset.faceInfo.pointSize}", GUILayout.Width(150));
            EditorGUILayout.LabelField($"Scale: {fontAsset.faceInfo.scale:F2}", GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            // Thai-glyph coverage feedback.
            if (thaiConsonantCoverage == 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "This font contains no Thai consonants, so adjustments cannot be applied.\n" +
                    "Choose a font with Thai glyphs, or regenerate the Font Asset to include Thai characters.",
                    MessageType.Error);
                return;
            }
            if (thaiConsonantCoverage > 0 && thaiConsonantCoverage < allConsonants.Length)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    $"This font is missing {allConsonants.Length - thaiConsonantCoverage} of {allConsonants.Length} Thai consonants. " +
                    "Pairs for missing glyphs will be skipped automatically.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(5);
            DrawLine();
            EditorGUILayout.Space(10);

            DrawOneClickSection();

            EditorGUILayout.Space(10);
            DrawLine();
            EditorGUILayout.Space(5);

            showAdvancedOptions = EditorGUILayout.Foldout(showAdvancedOptions, "Advanced Options", true, EditorStyles.foldoutHeader);

            if (showAdvancedOptions)
            {
                EditorGUILayout.Space(5);

                selectedTab = GUILayout.Toolbar(selectedTab, tabNames, GUILayout.Height(30));

                EditorGUILayout.Space(10);

                switch (selectedTab)
                {
                    case 0: DrawPresetsTab(); break;
                    case 1: DrawCustomTextTab(); break;
                    case 2: DrawRulesTab(); break;
                }
            }
        }

        /// <summary>Draws the big green one-click auto-fix box and the undo button.</summary>
        void DrawOneClickSection()
        {
            var boxStyle = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(15, 15, 15, 15) };

            EditorGUILayout.BeginVertical(boxStyle);

            EditorGUILayout.LabelField("ONE-CLICK AUTO FIX", oneClickTitleStyle);

            EditorGUILayout.Space(5);
            DrawLine();
            EditorGUILayout.Space(10);

            // Main action button.
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            GUI.enabled = !isProcessing;
            if (GUILayout.Button("Fix Everything Automatically", bigButtonStyle, GUILayout.Height(60)))
                OneClickAutoFix();
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);

            if (isProcessing)
            {
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(25)), progress, progressMessage);
                EditorGUILayout.Space(5);
            }
            else
            {
                EditorGUILayout.LabelField("This will:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  - Generate all rules ({CalculateTotalPossiblePairs()} pairs)", infoStyle);
                EditorGUILayout.LabelField("  - Auto-calculate values from font size", infoStyle);
                EditorGUILayout.LabelField("  - Apply to the Font Asset", infoStyle);
                EditorGUILayout.LabelField("  - Refresh TextMeshPro in the scene", infoStyle);
            }

            EditorGUILayout.EndVertical();

            // Undo button (only when a backup exists for the current font).
            if (backupRecords != null && lastModifiedFont == fontAsset)
            {
                EditorGUILayout.Space(5);
                GUI.backgroundColor = new Color(1f, 0.7f, 0.3f);
                if (GUILayout.Button("Undo Last Changes", GUILayout.Height(35)))
                    UndoLastChanges();
                GUI.backgroundColor = Color.white;
            }
        }

        /// <summary>Draws the Templates tab: per-template editors and "Generate All".</summary>
        void DrawPresetsTab()
        {
            EditorGUILayout.LabelField("Select a template", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Each template generates every possible character pair.\nDefault values are calculated automatically from the font metrics.", MessageType.Info);

            if (GUILayout.Button("Recalculate Default Values from Font", GUILayout.Height(30)))
            {
                InitializePresets();
                EditorUtility.DisplayDialog("Recalculated",
                    $"Recalculated values from the font metrics.\n\n" +
                    $"Base Adjustment: {CalculateBaseAdjustment()}\n" +
                    $"Upper+Tone Adjustment: {CalculateUpperToneAdjustment()}\n" +
                    $"Ascender Adjustment: {CalculateAscenderAdjustment()}",
                    "OK");
            }

            EditorGUILayout.Space(10);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawPresetButton("consonant_upper",
                () => GenerateConsonantUpperVowelPairs(),
                allConsonants.Length * upperVowels.Length);

            DrawPresetButton("consonant_tone",
                () => GenerateConsonantToneMarkPairs(),
                allConsonants.Length * toneMarks.Length);

            DrawPresetButton("consonant_thanthakhaat",
                () => GenerateConsonantThanThaKhaatPairs(),
                allConsonants.Length);

            DrawPresetButton("upper_tone",
                () => GenerateUpperVowelToneMarkPairs(),
                upperVowels.Length * toneMarks.Length);

            DrawPresetButton("upper_thanthakhaat",
                () => GenerateUpperVowelThanThaKhaatPairs(),
                upperVowels.Length);

            DrawPresetButton("sara_am_tone",
                () => GenerateSaraAmToneMarkPairs(),
                toneMarks.Length);

            DrawPresetButton("ascender_upper",
                () => GenerateAscenderUpperGlyphPairs(),
                ascenderConsonants.Length * (upperVowels.Length + toneMarks.Length + 1));

            DrawPresetButton("descender_lower",
                () => GenerateDescenderLowerVowelPairs(),
                descenderConsonants.Length * lowerVowels.Length);

            EditorGUILayout.Space(10);
            DrawLine();
            EditorGUILayout.Space(10);

            // Generate All.
            var allCount = CalculateTotalPossiblePairs();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Generate every possible pair", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Creates {allCount:N0} character pairs in total", EditorStyles.miniLabel);

            if (GUILayout.Button("Generate All Templates", GUILayout.Height(40)))
            {
                if (EditorUtility.DisplayDialog("Confirmation",
                    $"Generate all templates ({allCount:N0} pairs)?\n\nThis may take a moment.",
                    "Yes", "Cancel"))
                {
                    GenerateAllPresets();
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
        }

        /// <summary>Draws a single collapsible template card with X/Y/offset editors.</summary>
        void DrawPresetButton(string key, System.Action generateAction, int pairCount)
        {
            if (!presetConfigs.ContainsKey(key)) return;

            var config = presetConfigs[key];

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            var colorRect = EditorGUILayout.GetControlRect(GUILayout.Width(20), GUILayout.Height(20));
            EditorGUI.DrawRect(colorRect, config.color);

            config.expanded = EditorGUILayout.Foldout(config.expanded, config.name, true, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            if (config.expanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(config.description, EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField($"Pairs: {pairCount:N0}", EditorStyles.miniLabel);

                // Explain where each value comes from.
                if (key == "upper_tone" || key == "upper_thanthakhaat")
                    EditorGUILayout.HelpBox($"Calculated from Point Size × 19.4% = {config.defaultY}", MessageType.None);
                else if (key == "ascender_upper")
                    EditorGUILayout.HelpBox("Calculated from Point Size × 3.5% (ascender consonants)", MessageType.None);
                else if (key.StartsWith("consonant"))
                    EditorGUILayout.HelpBox("Calculated from Point Size × 2%", MessageType.None);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Default X:", GUILayout.Width(70));
                EditorGUI.BeginChangeCheck();
                float newX = EditorGUILayout.FloatField(config.defaultX, GUILayout.Width(60));
                if (EditorGUI.EndChangeCheck()) { config.defaultX = newX; Repaint(); }

                EditorGUILayout.LabelField("Y:", GUILayout.Width(20));
                EditorGUI.BeginChangeCheck();
                float newY = EditorGUILayout.FloatField(config.defaultY, GUILayout.Width(60));
                if (EditorGUI.EndChangeCheck()) { config.defaultY = newY; Repaint(); }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Offset:", GUILayout.Width(80));
                EditorGUILayout.LabelField("X:", GUILayout.Width(20));
                EditorGUI.BeginChangeCheck();
                float newOffsetX = EditorGUILayout.FloatField(config.offsetX, GUILayout.Width(60));
                if (EditorGUI.EndChangeCheck()) { config.offsetX = newOffsetX; Repaint(); }

                EditorGUILayout.LabelField("Y:", GUILayout.Width(20));
                EditorGUI.BeginChangeCheck();
                float newOffsetY = EditorGUILayout.FloatField(config.offsetY, GUILayout.Width(60));
                if (EditorGUI.EndChangeCheck()) { config.offsetY = newOffsetY; Repaint(); }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Final: X={config.FinalX:F2}, Y={config.FinalY:F2}", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button($"Add {pairCount:N0} Rules", GUILayout.Height(30)))
                    generateAction();

                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(25);
                EditorGUILayout.LabelField($"{pairCount:N0} pairs", EditorStyles.miniLabel, GUILayout.Width(80));
                if (GUILayout.Button("Add", GUILayout.Height(25)))
                    generateAction();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        /// <summary>Draws the Custom tab: detect pairs from a block of sample text.</summary>
        void DrawCustomTextTab()
        {
            EditorGUILayout.LabelField("Analyze from Text", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Enter text with floating vowels; the tool will detect the pairs that need adjusting.", MessageType.Info);

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Sample text:", EditorStyles.boldLabel);
            sampleText = EditorGUILayout.TextArea(sampleText, GUILayout.Height(150));

            EditorGUILayout.Space(10);

            if (GUILayout.Button("🔍 Analyze & Add Rules", GUILayout.Height(40)))
                ScanAndDetectPairs();
        }

        /// <summary>Draws the Rules tab: review, edit, select and apply generated rules.</summary>
        void DrawRulesTab()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Total rules: {adjustmentRules.Count}", EditorStyles.boldLabel);

            if (adjustmentRules.Count > 0 && GUILayout.Button("Clear All", GUILayout.Width(80)))
            {
                if (EditorUtility.DisplayDialog("Confirmation", "Clear all rules?", "Yes", "Cancel"))
                {
                    adjustmentRules.Clear();
                    Repaint();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (adjustmentRules.Count == 0)
            {
                EditorGUILayout.HelpBox("No rules yet.\nGo to the 'Templates' or 'Custom' tab to add some.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(5);

            var categories = adjustmentRules.GroupBy(r => r.category).OrderBy(g => g.Key);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All", GUILayout.Width(80)))
            {
                foreach (var rule in adjustmentRules) rule.selected = true;
                Repaint();
            }
            if (GUILayout.Button("Deselect All", GUILayout.Width(100)))
            {
                foreach (var rule in adjustmentRules) rule.selected = false;
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

            foreach (var category in categories)
            {
                var categoryKey = string.IsNullOrEmpty(category.Key) ? "Other" : category.Key;

                if (!categoryFoldouts.ContainsKey(categoryKey))
                    categoryFoldouts[categoryKey] = true;

                var count = category.Count();
                var selectedCount = category.Count(r => r.selected);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                categoryFoldouts[categoryKey] = EditorGUILayout.Foldout(
                    categoryFoldouts[categoryKey],
                    $"{categoryKey} ({selectedCount}/{count})",
                    true,
                    EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                if (categoryFoldouts[categoryKey])
                {
                    EditorGUI.indentLevel++;
                    foreach (var rule in category)
                        DrawRuleItem(rule);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(3);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);
            DrawLine();
            EditorGUILayout.Space(5);

            var totalSelected = adjustmentRules.Count(r => r.selected);

            if (totalSelected > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Selected: {totalSelected}", EditorStyles.boldLabel);

                if (GUILayout.Button($"Apply to Font Asset ({totalSelected})", GUILayout.Height(50)))
                    ApplyAdjustments();

                EditorGUILayout.Space(5);

                if (GUILayout.Button("Force Refresh TextMeshPro in Scene", GUILayout.Height(30)))
                {
                    RefreshTextMeshProComponents(fontAsset);
                    EditorUtility.DisplayDialog("Refreshed", "Force-refreshed TextMeshPro components in the scene.", "OK");
                }

                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.HelpBox("Please select the rules you want to apply.", MessageType.Warning);
            }
        }

        /// <summary>Draws one editable rule row (toggle + pair label + X/Y fields).</summary>
        void DrawRuleItem(AdjustmentRule rule)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            bool newSelected = EditorGUILayout.Toggle(rule.selected, GUILayout.Width(20));
            if (EditorGUI.EndChangeCheck()) { rule.selected = newSelected; Repaint(); }

            EditorGUILayout.LabelField($"{rule.firstChar} + {rule.secondChar}", ruleLabelStyle, GUILayout.Width(60));

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField("X:", GUILayout.Width(15));
            EditorGUI.BeginChangeCheck();
            float newX = EditorGUILayout.FloatField(rule.xPlacement, GUILayout.Width(50));
            if (EditorGUI.EndChangeCheck()) { rule.xPlacement = newX; Repaint(); }

            EditorGUILayout.LabelField("Y:", GUILayout.Width(15));
            EditorGUI.BeginChangeCheck();
            float newY = EditorGUILayout.FloatField(rule.yPlacement, GUILayout.Width(50));
            if (EditorGUI.EndChangeCheck()) { rule.yPlacement = newY; Repaint(); }

            EditorGUILayout.EndHorizontal();
        }

        // ===================== One-Click Pipeline =====================

        /// <summary>
        /// Runs the full fix in one step: backup, generate every rule, apply to the font
        /// asset, and refresh scene TextMeshPro components. Shows a progress bar and a
        /// summary dialog.
        /// </summary>
        async void OneClickAutoFix()
        {
            if (fontAsset == null) return;

            isProcessing = true;
            progress = 0f;

            try
            {
                BackupFontAsset();

                // Step 1 — generate rules.
                progressMessage = "Generating rules...";
                Repaint();
                await System.Threading.Tasks.Task.Delay(100);

                adjustmentRules.Clear();
                GenerateAllRules();
                progress = 0.33f;
                Repaint();

                // Step 2 — apply to font.
                progressMessage = "Applying to Font Asset...";
                await System.Threading.Tasks.Task.Delay(100);

                ApplyAdjustmentsInternal();
                progress = 0.66f;
                Repaint();

                // Step 3 — refresh.
                progressMessage = "Refreshing TextMeshPro...";
                await System.Threading.Tasks.Task.Delay(100);

                RefreshTextMeshProComponents(fontAsset);
                progress = 1f;
                progressMessage = "Done!";
                Repaint();

                await System.Threading.Tasks.Task.Delay(500);

                int totalRules = fontAsset.fontFeatureTable.glyphPairAdjustmentRecords.Count;
                EditorUtility.DisplayDialog("Success!",
                    $"Font adjustment complete!\n\n" +
                    $"Total rules: {totalRules}\n" +
                    $"Ready to use.",
                    "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error",
                    $"Operation failed.\n\nError: {e.Message}",
                    "OK");
                Debug.LogError($"[EasyThaiFontAdjustment] One-click failed: {e}");
            }
            finally
            {
                isProcessing = false;
                progress = 0f;
                progressMessage = "";
                // The window may have been closed while awaiting; guard the repaint.
                if (this != null) Repaint();
            }
        }

        // ===================== Backup / Undo =====================

        /// <summary>Snapshots the current pair-adjustment table so it can be restored.</summary>
        void BackupFontAsset()
        {
            if (fontAsset == null) return;

            backupRecords = new List<GlyphPairAdjustmentRecord>(
                fontAsset.fontFeatureTable.glyphPairAdjustmentRecords);
            lastModifiedFont = fontAsset;
        }

        /// <summary>Restores the pair-adjustment table from the last backup.</summary>
        void UndoLastChanges()
        {
            if (fontAsset == null || backupRecords == null) return;

            if (!EditorUtility.DisplayDialog("Confirm Undo",
                "Revert the last changes?",
                "Yes", "Cancel"))
                return;

            Undo.RecordObject(fontAsset, "Undo Thai Font Adjustments");

            fontAsset.fontFeatureTable.glyphPairAdjustmentRecords.Clear();
            fontAsset.fontFeatureTable.glyphPairAdjustmentRecords.AddRange(backupRecords);

            EditorUtility.SetDirty(fontAsset);
            fontAsset.ReadFontAssetDefinition();
            TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, fontAsset);
            AssetDatabase.SaveAssets();

            RefreshTextMeshProComponents(fontAsset);

            backupRecords = null;
            lastModifiedFont = null;

            EditorUtility.DisplayDialog("Success", "Changes reverted.", "OK");
        }

        // ===================== Rule Generation =====================

        /// <summary>
        /// Generates every template into <see cref="adjustmentRules"/> without showing
        /// dialogs. Ascender consonants (ป ฝ ฟ ฬ) are generated last with
        /// <c>overrideExisting</c> so their stronger offset wins over the base value
        /// added by the consonant templates.
        /// </summary>
        void GenerateAllRules()
        {
            var consonantUpper = presetConfigs["consonant_upper"];
            foreach (var consonant in allConsonants)
                foreach (var vowel in upperVowels)
                    AddRule($"{consonant}+{vowel}", consonant, vowel, consonantUpper.FinalX, consonantUpper.FinalY, consonantUpper.name);

            var consonantTone = presetConfigs["consonant_tone"];
            foreach (var consonant in allConsonants)
                foreach (var tone in toneMarks)
                    AddRule($"{consonant}+{tone}", consonant, tone, consonantTone.FinalX, consonantTone.FinalY, consonantTone.name);

            var consonantThan = presetConfigs["consonant_thanthakhaat"];
            foreach (var consonant in allConsonants)
                AddRule($"{consonant}+์", consonant, thanThaKhaat, consonantThan.FinalX, consonantThan.FinalY, consonantThan.name);

            var upperTone = presetConfigs["upper_tone"];
            foreach (var vowel in upperVowels)
                foreach (var tone in toneMarks)
                    AddRule($"{vowel}+{tone}", vowel, tone, upperTone.FinalX, upperTone.FinalY, upperTone.name);

            var upperThan = presetConfigs["upper_thanthakhaat"];
            foreach (var vowel in upperVowels)
                AddRule($"{vowel}+์", vowel, thanThaKhaat, upperThan.FinalX, upperThan.FinalY, upperThan.name);

            var saraAmTone = presetConfigs["sara_am_tone"];
            foreach (var tone in toneMarks)
                AddRule($"ำ+{tone}", saraAm, tone, saraAmTone.FinalX, saraAmTone.FinalY, saraAmTone.name);

            // Ascender consonants overlap the base consonant templates; override so the
            // stronger ascender offset replaces the base value already added above.
            var ascender = presetConfigs["ascender_upper"];
            foreach (var consonant in ascenderConsonants)
            {
                foreach (var vowel in upperVowels)
                    AddRule($"{consonant}+{vowel}", consonant, vowel, ascender.FinalX, ascender.FinalY, ascender.name, overrideExisting: true);
                foreach (var tone in toneMarks)
                    AddRule($"{consonant}+{tone}", consonant, tone, ascender.FinalX, ascender.FinalY, ascender.name, overrideExisting: true);
                AddRule($"{consonant}+์", consonant, thanThaKhaat, ascender.FinalX, ascender.FinalY, ascender.name, overrideExisting: true);
            }

            var descender = presetConfigs["descender_lower"];
            foreach (var consonant in descenderConsonants)
                foreach (var vowel in lowerVowels)
                    AddRule($"{consonant}+{vowel}", consonant, vowel, descender.FinalX, descender.FinalY, descender.name);
        }

        void GenerateConsonantUpperVowelPairs()
        {
            var config = presetConfigs["consonant_upper"];
            int added = 0;
            foreach (var consonant in allConsonants)
                foreach (var vowel in upperVowels)
                    if (AddRule($"{consonant}+{vowel}", consonant, vowel, config.FinalX, config.FinalY, config.name)) added++;

            FinishGeneration(added);
        }

        void GenerateConsonantToneMarkPairs()
        {
            var config = presetConfigs["consonant_tone"];
            int added = 0;
            foreach (var consonant in allConsonants)
                foreach (var tone in toneMarks)
                    if (AddRule($"{consonant}+{tone}", consonant, tone, config.FinalX, config.FinalY, config.name)) added++;

            FinishGeneration(added);
        }

        void GenerateConsonantThanThaKhaatPairs()
        {
            var config = presetConfigs["consonant_thanthakhaat"];
            int added = 0;
            foreach (var consonant in allConsonants)
                if (AddRule($"{consonant}+์", consonant, thanThaKhaat, config.FinalX, config.FinalY, config.name)) added++;

            FinishGeneration(added);
        }

        void GenerateUpperVowelToneMarkPairs()
        {
            var config = presetConfigs["upper_tone"];
            int added = 0;
            foreach (var vowel in upperVowels)
                foreach (var tone in toneMarks)
                    if (AddRule($"{vowel}+{tone}", vowel, tone, config.FinalX, config.FinalY, config.name)) added++;

            FinishGeneration(added);
        }

        void GenerateUpperVowelThanThaKhaatPairs()
        {
            var config = presetConfigs["upper_thanthakhaat"];
            int added = 0;
            foreach (var vowel in upperVowels)
                if (AddRule($"{vowel}+์", vowel, thanThaKhaat, config.FinalX, config.FinalY, config.name)) added++;

            FinishGeneration(added);
        }

        void GenerateSaraAmToneMarkPairs()
        {
            var config = presetConfigs["sara_am_tone"];
            int added = 0;
            foreach (var tone in toneMarks)
                if (AddRule($"{saraAm}+{tone}", saraAm, tone, config.FinalX, config.FinalY, config.name)) added++;

            FinishGeneration(added);
        }

        void GenerateAscenderUpperGlyphPairs()
        {
            var config = presetConfigs["ascender_upper"];
            int changed = 0;
            foreach (var consonant in ascenderConsonants)
            {
                // Override so the ascender offset wins even if a base template already
                // added these pairs with the weaker value.
                foreach (var vowel in upperVowels)
                    if (AddRule($"{consonant}+{vowel}", consonant, vowel, config.FinalX, config.FinalY, config.name, overrideExisting: true)) changed++;
                foreach (var tone in toneMarks)
                    if (AddRule($"{consonant}+{tone}", consonant, tone, config.FinalX, config.FinalY, config.name, overrideExisting: true)) changed++;
                if (AddRule($"{consonant}+์", consonant, thanThaKhaat, config.FinalX, config.FinalY, config.name, overrideExisting: true)) changed++;
            }

            FinishGeneration(changed);
        }

        void GenerateDescenderLowerVowelPairs()
        {
            var config = presetConfigs["descender_lower"];
            int added = 0;
            foreach (var consonant in descenderConsonants)
                foreach (var vowel in lowerVowels)
                    if (AddRule($"{consonant}+{vowel}", consonant, vowel, config.FinalX, config.FinalY, config.name)) added++;

            FinishGeneration(added);
        }

        /// <summary>Generates every template from the Templates tab, with a summary dialog.</summary>
        void GenerateAllPresets()
        {
            int beforeCount = adjustmentRules.Count;
            GenerateAllRules();
            int addedCount = adjustmentRules.Count - beforeCount;

            EditorUtility.DisplayDialog("Success",
                $"Generated all templates.\n\nAdded: {addedCount} new rules\nTotal: {adjustmentRules.Count} rules",
                "OK");

            selectedTab = 2;
            Repaint();
        }

        /// <summary>Shared post-generation UI: report count and jump to the Rules tab.</summary>
        void FinishGeneration(int count)
        {
            EditorUtility.DisplayDialog("Success", $"Added/updated {count} rules", "OK");
            selectedTab = 2;
            Repaint();
        }

        /// <summary>Detects problematic Thai pairs from the sample text and adds rules.</summary>
        void ScanAndDetectPairs()
        {
            if (string.IsNullOrEmpty(sampleText))
            {
                EditorUtility.DisplayDialog("Error", "Please enter sample text.", "OK");
                return;
            }

            var words = sampleText.Split(new[] { ' ', '\n', '\r', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            int added = 0;

            foreach (var word in words)
            {
                for (int i = 0; i < word.Length - 1; i++)
                {
                    char firstChar = word[i];
                    char secondChar = word[i + 1];

                    if (IsValidThaiPair(firstChar, secondChar))
                    {
                        if (AddRule($"{word}", firstChar, secondChar, 0f, -2f, "From Text"))
                            added++;
                    }
                }
            }

            if (added == 0)
            {
                EditorUtility.DisplayDialog("No Pairs Found", "No adjustable character pairs found in the given text.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Success", $"Added {added} new rules from the text.", "OK");
                selectedTab = 2;
                Repaint();
            }
        }

        /// <summary>True when the ordered pair is a Thai combination worth adjusting.</summary>
        bool IsValidThaiPair(char first, char second)
        {
            // Consonant + upper mark.
            if (IsThaiConsonant(first) && IsUpperGlyph(second)) return true;
            // Upper vowel + tone / thanthakhaat.
            if (upperVowels.Contains(first) && (toneMarks.Contains(second) || second == thanThaKhaat)) return true;
            // Consonant + lower vowel.
            if (IsThaiConsonant(first) && lowerVowels.Contains(second)) return true;

            return false;
        }

        /// <summary>
        /// Adds a rule, or (when <paramref name="overrideExisting"/> is true) updates the
        /// values of an existing rule with the same glyph pair. Returns true when the
        /// list was added to or an existing rule was changed.
        /// </summary>
        bool AddRule(string name, char first, char second, float x, float y, string category, bool overrideExisting = false)
        {
            var key = $"{first}_{second}";
            var existing = adjustmentRules.FirstOrDefault(r => r.Key == key);

            if (existing != null)
            {
                if (!overrideExisting) return false;

                existing.ruleName = name;
                existing.xPlacement = x;
                existing.yPlacement = y;
                existing.category = category;
                return true;
            }

            adjustmentRules.Add(new AdjustmentRule(name, first, second, x, y, category));
            return true;
        }

        /// <summary>
        /// Number of unique pairs "Generate All" produces. Ascender-consonant pairs are
        /// excluded here because they fully overlap the consonant templates and only
        /// override existing values (they do not add new pairs).
        /// </summary>
        int CalculateTotalPossiblePairs()
        {
            int total = 0;
            total += allConsonants.Length * upperVowels.Length;
            total += allConsonants.Length * toneMarks.Length;
            total += allConsonants.Length;                       // + ์
            total += upperVowels.Length * toneMarks.Length;
            total += upperVowels.Length;                         // + ์
            total += toneMarks.Length;                           // saraAm + tone
            total += descenderConsonants.Length * lowerVowels.Length;
            return total;
        }

        // ===================== Apply to Font Asset =====================

        /// <summary>Applies the selected rules to the font asset, with a summary dialog.</summary>
        void ApplyAdjustments()
        {
            if (fontAsset == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a Font Asset.", "OK");
                return;
            }

            Undo.RecordObject(fontAsset, "Apply Thai Vowel Adjustments");

            int addedCount = 0, updatedCount = 0, skippedCount = 0;

            foreach (var rule in adjustmentRules.Where(r => r.selected))
            {
                if (TryGetGlyphIndex(fontAsset, rule.firstChar, out uint firstGlyphIndex) &&
                    TryGetGlyphIndex(fontAsset, rule.secondChar, out uint secondGlyphIndex))
                {
                    bool isNew = AddOrUpdatePairAdjustment(firstGlyphIndex, secondGlyphIndex, rule.xPlacement, rule.yPlacement);
                    if (isNew) addedCount++; else updatedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }

            CommitFontAsset();
            RefreshTextMeshProComponents(fontAsset);

            string message = "Success!\n\n";
            if (addedCount > 0) message += $"Added: {addedCount}\n";
            if (updatedCount > 0) message += $"Updated: {updatedCount}\n";
            if (skippedCount > 0) message += $"Skipped (not in font): {skippedCount}\n";
            message += $"\nTotal in Font Asset: {fontAsset.fontFeatureTable.glyphPairAdjustmentRecords.Count}";

            EditorUtility.DisplayDialog("Success", message, "OK");
            Repaint();
        }

        /// <summary>Applies the selected rules without dialogs (used by the one-click flow).</summary>
        void ApplyAdjustmentsInternal()
        {
            if (fontAsset == null) return;

            Undo.RecordObject(fontAsset, "Apply Thai Vowel Adjustments");

            foreach (var rule in adjustmentRules.Where(r => r.selected))
            {
                if (TryGetGlyphIndex(fontAsset, rule.firstChar, out uint firstGlyphIndex) &&
                    TryGetGlyphIndex(fontAsset, rule.secondChar, out uint secondGlyphIndex))
                {
                    AddOrUpdatePairAdjustment(firstGlyphIndex, secondGlyphIndex, rule.xPlacement, rule.yPlacement);
                }
            }

            CommitFontAsset();
        }

        /// <summary>Flushes pending changes to the font asset and notifies TextMeshPro.</summary>
        void CommitFontAsset()
        {
            EditorUtility.SetDirty(fontAsset);
            fontAsset.ReadFontAssetDefinition();
            TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Adds a new glyph-pair adjustment record, or updates the second glyph's
        /// placement if the pair already exists. Returns true when a new record was added.
        /// </summary>
        bool AddOrUpdatePairAdjustment(uint firstGlyphIndex, uint secondGlyphIndex, float xPlacement, float yPlacement)
        {
            var adjustmentRecords = fontAsset.fontFeatureTable.glyphPairAdjustmentRecords;

            for (int i = 0; i < adjustmentRecords.Count; i++)
            {
                var record = adjustmentRecords[i];
                if (record.firstAdjustmentRecord.glyphIndex == firstGlyphIndex &&
                    record.secondAdjustmentRecord.glyphIndex == secondGlyphIndex)
                {
                    // Records are structs, so update means replacing the whole pair.
                    adjustmentRecords[i] = BuildPairRecord(firstGlyphIndex, secondGlyphIndex, xPlacement, yPlacement);
                    return false;
                }
            }

            adjustmentRecords.Add(BuildPairRecord(firstGlyphIndex, secondGlyphIndex, xPlacement, yPlacement));
            return true;
        }

        /// <summary>
        /// Builds a pair-adjustment record that offsets only the second glyph by the
        /// given X/Y placement (the first glyph is left untouched).
        /// </summary>
        static GlyphPairAdjustmentRecord BuildPairRecord(uint firstGlyphIndex, uint secondGlyphIndex, float xPlacement, float yPlacement)
        {
            var firstValue = new GlyphValueRecord { xPlacement = 0, yPlacement = 0, xAdvance = 0, yAdvance = 0 };
            var secondValue = new GlyphValueRecord { xPlacement = xPlacement, yPlacement = yPlacement, xAdvance = 0, yAdvance = 0 };

            var first = new GlyphAdjustmentRecord(firstGlyphIndex, firstValue);
            var second = new GlyphAdjustmentRecord(secondGlyphIndex, secondValue);
            return new GlyphPairAdjustmentRecord(first, second);
        }

        // ===================== Helpers =====================

        /// <summary>Counts how many base Thai consonants are present in the selected font.</summary>
        void CheckFontCoverage()
        {
            if (fontAsset == null) { thaiConsonantCoverage = -1; return; }

            int found = 0;
            foreach (var c in allConsonants)
                if (TryGetGlyphIndex(fontAsset, c, out _)) found++;
            thaiConsonantCoverage = found;
        }

        /// <summary>Restores the last font, sample text and advanced-options state.</summary>
        void LoadPreferences()
        {
            sampleText = EditorPrefs.GetString(PrefSampleText, sampleText);
            showAdvancedOptions = EditorPrefs.GetBool(PrefShowAdvanced, showAdvancedOptions);

            string path = EditorPrefs.GetString(PrefFont, "");
            if (!string.IsNullOrEmpty(path))
                fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        }

        /// <summary>Persists the current font, sample text and advanced-options state.</summary>
        void SavePreferences()
        {
            EditorPrefs.SetString(PrefSampleText, sampleText);
            EditorPrefs.SetBool(PrefShowAdvanced, showAdvancedOptions);
            EditorPrefs.SetString(PrefFont, fontAsset != null ? AssetDatabase.GetAssetPath(fontAsset) : "");
        }

        /// <summary>Looks up the glyph index for a character; false if not in the font.</summary>
        bool TryGetGlyphIndex(TMP_FontAsset fontAsset, char character, out uint glyphIndex)
        {
            glyphIndex = 0;
            var characterData = fontAsset.characterTable.FirstOrDefault(c => c.unicode == character);
            if (characterData != null)
            {
                glyphIndex = characterData.glyphIndex;
                return true;
            }
            return false;
        }

        /// <summary>True for the Thai consonant block (ก..ฮ).</summary>
        bool IsThaiConsonant(char c) => c >= 'ก' && c <= 'ฮ';

        /// <summary>True for any mark that sits above a base glyph (vowel, tone, thanthakhaat).</summary>
        bool IsUpperGlyph(char c) => upperVowels.Contains(c) || toneMarks.Contains(c) || c == thanThaKhaat;

        /// <summary>Forces every scene TextMeshPro that uses this font to rebuild its mesh.</summary>
        void RefreshTextMeshProComponents(TMP_FontAsset fontAsset)
        {
#if UNITY_2023_1_OR_NEWER
            var tmpTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
#else
            var tmpTexts = Object.FindObjectsOfType<TMP_Text>();
#endif
            foreach (var tmpText in tmpTexts)
            {
                if (tmpText.font == fontAsset)
                {
                    tmpText.ForceMeshUpdate(true, true);
                    EditorUtility.SetDirty(tmpText);
                }
            }
        }

        /// <summary>Draws a thin horizontal separator line.</summary>
        void DrawLine()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        }
    }
}
