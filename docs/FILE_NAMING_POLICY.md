# File Naming Policy for Windows Delivery

## Purpose

All file and folder names shipped in the project archive use **ASCII English characters only**. This avoids filename corruption when a ZIP archive is extracted by Windows Explorer or by an extractor that assumes a legacy code page.

## Rules

| Asset | Rule | Example |
| --- | --- | --- |
| Bayan source program | Lowercase English `.bayan` extension | `12_official_basic_execution.bayan` |
| Sample file prefix | Two-digit ordering prefix | `25_symbolic_string_input.bayan` |
| Documentation | Lowercase English `.md` extension and ASCII filename | `ll1_visual_studio_testing_guide.md` |
| Delivery archive | ASCII English name | `BayanCompiler_Windows_Compatible_2026-08-24.zip` |
| Source contents | May remain Arabic UTF-8 | Arabic keywords and examples are unaffected |

The names of all 25 samples and all documentation files in the delivery folder have been migrated to this policy. The CLI usage text, editor open/save dialogs, temporary editor input, tests, README, and technical references now use `.bayan`. For official compiler testing, use samples `11` through `25`; samples `01` through `10` are historical archive material and are not acceptance cases for the official grammar.

> This policy changes file paths only. It does not alter the official Arabic language grammar, tokens, source encoding, or compiler output.
