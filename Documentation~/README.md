# Easy Thai Font Adjustment - Documentation

## Table of Contents
1. [Installation](#installation)
2. [Quick Start](#quick-start)
3. [Features](#features)
4. [Advanced Usage](#advanced-usage)
5. [API Reference](#api-reference)
6. [Troubleshooting](#troubleshooting)

## Installation

### Unity Package Manager
```
1. Window → Package Manager
2. + → Add package from git URL
3. https://github.com/poppod56/EasyThaiFontAdjustment.git
```

### Manual Install
```
Copy Assets/EasyThaiFontAdjustment/Editor/EasyThaiFontAdjustment.cs to your Assets/ folder
```

## Quick Start

```
Tools → Easy Thai Font Adjustment
→ Select Font Asset
→ Click "Fix Everything Automatically"
→ Done!
```

## Features

### One-Click Fix
Automatically fixes all 522 Thai character pair combinations in seconds.

### Smart Calculation
Kerning values are calculated based on:
- Font point size
- Ascender/descender height  
- Scale factor

### Undo Support
- Automatic backup before changes
- One-click undo
- Unity Undo integration (Ctrl/Cmd + Z)

### Templates System
7 pre-configured templates covering:
- Consonants + vowels
- Vowels + tone marks
- Special cases (ascenders/descenders)

## Advanced Usage

### Custom Text Analysis
Analyze specific text to find problematic pairs:
```
1. Advanced Options → Custom Tab
2. Enter sample text
3. Click "Analyze and Add Rules"
```

### Fine-Tuning
Adjust individual character pairs:
```
1. Advanced Options → Rules Tab
2. Edit X/Y values
3. Select/deselect pairs
4. Apply changes
```

### Template Customization
Modify default values:
```
1. Advanced Options → Templates Tab
2. Select template
3. Adjust default X/Y
4. Add offset values
5. Add rules
```

## API Reference

### Public Methods

```csharp
// Open the tool window
[MenuItem("Tools/Easy Thai Font Adjustment")]
public static void ShowWindow()

// Calculate adjustment values (automatic)
private float CalculateBaseAdjustment()
private float CalculateUpperToneAdjustment()
private float CalculateAscenderAdjustment()
```

### Data Structures

```csharp
// Adjustment rule for a character pair
private class AdjustmentRule
{
    public char firstChar;
    public char secondChar;
    public float xPlacement;
    public float yPlacement;
    public string category;
}

// Template configuration
private class PresetConfig
{
    public float defaultX;
    public float defaultY;
    public float offsetX;
    public float offsetY;
}
```

## Troubleshooting

### Changes Not Visible
```
Solution: Reimport Font Asset
1. Select Font Asset
2. Right-click → Reimport
```

### Incorrect Values
```
Solution: Use auto-calculation
1. Don't manually set values
2. Let system calculate based on font size
3. Use offset for fine-tuning only
```

### Performance Issues
```
Solution: Apply only needed pairs
1. Advanced Options → Rules Tab
2. Deselect unused pairs
3. Apply selected only
```

### Missing Characters
```
Solution: Check font coverage
1. Font Asset → Character Table
2. Ensure Thai Unicode range included
3. Regenerate font if needed
```

## Best Practices

1. **Always backup** Font Assets before applying
2. **Test with real text** after applying changes
3. **Use one-click** for general fixes
4. **Use advanced options** only for special cases
5. **Document custom values** for future reference

## Version History

### 1.0.0 (2025-11-07)
- Initial release
- One-click auto fix
- 522 character pairs
- Undo support

## Support

- Email: support@poppod-studio.com
- GitHub Issues: Report bugs and request features
- GitHub Discussions: Ask questions and share tips
- Documentation: This file and inline comments

## License

MIT License - Free for commercial and personal use

---

© 2025 Poppod Studio
