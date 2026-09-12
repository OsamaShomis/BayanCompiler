# Bayan Source Studio Implementation

## Adopted Direction

The editor now follows the approved **Bayan Source Studio** direction: an Arabic academic identity, warm paper-like surfaces, navy as the structural color, and one restrained copper accent. The editor remains a Windows Forms application and does not instantiate compiler services directly.

## Welcome Experience

`WelcomeForm` opens before the editor and provides three explicit entry paths:

| Action | Behavior |
| --- | --- |
| `برنامج جديد` | Opens the editor with the official starter source. |
| `فتح ملف .bayan` | Lets the user select an ASCII-safe Bayan source file. |
| `اختيار عينة المشروع` | Starts from the `samples` directory when the project tree is available. |

The welcome panel describes the genuine pipeline `CLI → LL(1) → Parse Tree → TAC → MIPS`. It does not display generated example output or fabricated diagnostics.

## Editor Changes

The editor toolbar now emphasizes **تحليل وترجمة** as the central command. Source editing remains on the right, while the output area uses a compact stage rail for the real persisted artifacts: Tokens/AST, LL(1) table, LL(1) trace, Parse Tree, Symbol Table, TAC/IR, MIPS, and diagnostics. Selecting a stage changes the same `TabControl` that receives artifacts from `CliCompilationClient`; it is only a navigation and presentation change.

`Program.cs` shows the welcome dialog first, then creates `MainForm` only after a user action. A selected `.bayan` source file is passed into the editor and loaded by a shared file-loading method. CLI remains an independent project and is still executed through `CliCompilationClient`.

## Required Windows Verification

The Linux environment cannot build Windows Forms because `Microsoft.NET.Sdk.WindowsDesktop.targets` is unavailable. On Windows, run **Clean Solution**, then **Rebuild Solution**, then check the following manually:

1. The welcome screen opens before the editor.
2. Each of the three welcome actions enters the editor correctly.
3. The stage rail displays real artifacts after translating a valid `.bayan` file.
4. A syntax-error sample selects the diagnostics stage.
5. `تحليل وترجمة` produces and saves real MIPS output through the independent CLI.
