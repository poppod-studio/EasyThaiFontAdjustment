# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-11-07

### Added
- One-click auto fix for all Thai character positioning issues
- 522 character pairs covering all Thai combinations
- Smart kerning calculation based on font metrics
- Backup and undo system
- Advanced editing mode with 3 tabs (Templates, Custom, Rules)
- 7 pre-configured templates:
  - Consonants + Upper Vowels (264 pairs)
  - Consonants + Tone Marks (176 pairs)
  - Consonants + Thanthakhat (44 pairs)
  - Upper Vowels + Tone Marks (24 pairs)
  - Upper Vowels + Thanthakhat (6 pairs)
  - Sara Am + Tone Marks (4 pairs)
  - Ascender Consonants (44 pairs)
  - Descender Consonants (4 pairs)
- Progress bar for long operations
- Custom text analysis for finding problematic pairs
- Individual pair editing in Rules tab
- Dynamic value calculation based on font size
- Unity Undo/Redo integration
- Bilingual UI: English labels with Thai terms in parentheses
- Thai-glyph coverage check that warns on fonts missing Thai characters
- Session persistence (last font, sample text, panel state) via EditorPrefs
- Context-menu entry on TMP Font Assets (right-click the Inspector header)
- Editor assembly definition for clean UPM / Asset Store distribution

### Changed
- N/A (Initial release)

### Deprecated
- N/A (Initial release)

### Removed
- N/A (Initial release)

### Fixed
- N/A (Initial release)

### Security
- N/A (Initial release)

## [Unreleased]

### Planned
- Batch processing for multiple fonts
- Preset save/load system
- Export/import rules as JSON
- Visual preview before applying
- Font comparison tool
- Performance optimization for large fonts
- Additional language support (Lao, Khmer)

---

## Version History Summary

- **1.0.0** - Initial release with one-click fix and 522 character pairs
