# Easy Thai Font Adjustment - Technical Details

## Technical Information for Unity Asset Store

---

## 📋 Package Information

**Package Name:** Easy Thai Font Adjustment
**Version:** 1.0.0
**Publisher:** Poppod Studio
**Category:** Editor Extensions > Utilities
**License:** MIT License

---

## 🔧 Technical Specifications

### Unity Compatibility
- **Minimum Unity Version:** 2021.3 LTS
- **Tested Unity Versions:** 2021.3 LTS, 2022.3 LTS, 2023.x, Unity 6
- **Render Pipeline:** Universal (URP), High Definition (HDRP), Built-in — pipeline-agnostic (Editor tool)
- **Platform:** Editor Only (not included in builds, zero runtime overhead)

### System Requirements
- **Operating Systems:**
  - Windows 10/11 (64-bit)
  - macOS 10.15 or later
  - Linux (Ubuntu 20.04 or later)
- **Disk Space:** ~80KB
- **Dependencies:** TextMeshPro 3.0.6+ (`com.unity.textmeshpro`)

---

## 📦 Package Contents

```
Easy Thai Font Adjustment/
├── Editor/
│   ├── EasyThaiFontAdjustment.cs              (Main editor window + logic)
│   ├── EasyThaiFontAdjustment.Editor.asmdef   (Editor-only assembly definition)
│   └── EASY_THAI_FONT_ADJUSTMENT_GUIDE.md     (Full Thai user guide)
├── README.md
├── CHANGELOG.md
├── LICENSE
└── package.json
```

---

## ⚙️ How It Works

The tool writes **Glyph Pair Adjustment Records** into the selected `TMP_FontAsset`'s
`fontFeatureTable`. Each record offsets the *second* glyph of a pair by an X/Y placement,
which TextMeshPro applies automatically at render time — the same mechanism as kerning.

```
Pair = (firstGlyphIndex, secondGlyphIndex, xPlacement, yPlacement)
```

For every problematic Thai combination the tool:
1. **Generates** the pair list from 8 templates (de-duplicated).
2. **Calculates** X/Y from font metrics (point size × scale).
3. **Resolves** each character to its glyph index via the font's `characterTable`.
4. **Applies** the records (add or update) and flags the asset dirty.
5. **Refreshes** all scene `TMP_Text` components using that font.

### Adjustment formulas (auto-scaled by point size)

| Case | Formula | Example @ Point Size 100 |
|------|---------|--------------------------|
| Consonant + upper mark | `-pointSize × 0.02 × scale` | -2 |
| Stacked upper vowel + tone | `pointSize × 0.194 × scale` | 19.4 |
| Ascender consonant (ป ฝ ฟ ฬ) | `-pointSize × 0.035 × scale` | -3.5 |
| Descender consonant (ฎ ฏ) + lower vowel | fixed | +2 |

Ascender-consonant pairs **override** the base consonant values rather than adding new
pairs, ensuring ป ฝ ฟ ฬ receive their stronger offset.

---

## 🧮 Coverage

| Template | Pairs |
|----------|-------|
| Consonant + Upper Vowel | 264 |
| Consonant + Tone Mark | 176 |
| Consonant + Thanthakhat | 44 |
| Upper Vowel + Tone Mark | 24 |
| Upper Vowel + Thanthakhat | 6 |
| Sara Am + Tone Mark | 4 |
| Descender Consonant + Lower Vowel | 4 |
| Ascender Consonant + Mark | (44, override) |
| **Total unique** | **522** |

---

## 🛡️ Safety & Reliability

- **Non-destructive:** an in-memory backup is taken before each one-click run, restorable
  via "Undo Last Changes". All changes also integrate with Unity's native Undo (Ctrl/Cmd+Z).
- **Glyph-aware:** characters not present in the font are skipped and reported, never errored.
- **Coverage check:** the tool detects fonts with no/partial Thai glyphs and warns up front.
- **Editor-only:** lives in an `Editor` assembly — nothing ships in player builds.
- **Session persistence:** last font, sample text, and panel state are remembered via EditorPrefs.

---

## 🧩 Integration Points

- **Tools menu:** `Tools > Easy Thai Font Adjustment`
- **Context menu:** right-click a TMP Font Asset's Inspector header → `Easy Thai Font Adjustment`

---

## 📞 Support

- Email: support@poppod-studio.com
- GitHub: https://github.com/poppod56/EasyThaiFontAdjustment

© 2025 Poppod Studio — MIT License
