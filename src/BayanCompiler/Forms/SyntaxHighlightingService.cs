using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace BayanCompiler.Forms;

/// <summary>
/// Provides real-time, flicker-free syntax highlighting for Arabic Bayan language code in RichTextBox.
/// </summary>
public static class SyntaxHighlightingService
{
    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    private const int WM_SETREDRAW = 0x000B;

    // Language keywords
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "برنامج", "متغير", "ثابت", "نوع", "إجراء", "دالة",
        "صحيح", "حقيقي", "منطقي", "حرفي", "خيط", "سجل", "قائمة",
        "إذا", "وإلا", "إلا", "كرر", "إلى", "أضف", "طالما", "استمر", "أعد", "حتى",
        "اقرأ", "اطبع", "مرجع", "قيمة", "نعم", "لا", "صواب", "خطأ", "استدعاء", "إرجاع"
    };

    // Regex patterns for highlighting
    private static readonly Regex TokenRegex = new(
        @"(//[^\r\n]*)|(/\*[\s\S]*?\*/)|(""(?:\\.|[^""\\])*"")|('(?:\\.|[^'\\])')|(\b\d+(?:\.\d+)?\b)|([\p{L}_\u0621-\u064A][\p{L}\p{Nd}_\u0621-\u064A]*)|([+\-*/%^=<>!&|:;,.()\[\]{}]+)",
        RegexOptions.Compiled);

    public static void ApplyHighlighting(RichTextBox box, bool isDark)
    {
        if (box.IsDisposed || !box.IsHandleCreated || string.IsNullOrEmpty(box.Text))
        {
            return;
        }

        // Freeze redraw
        SendMessage(box.Handle, WM_SETREDRAW, 0, 0);

        int originalSelectionStart = box.SelectionStart;
        int originalSelectionLength = box.SelectionLength;

        Color defaultTextColor = isDark ? IdeTheme.Dark.TextPrimary : IdeTheme.Light.TextPrimary;
        Color keywordColor = isDark ? Color.FromArgb(122, 162, 247) : Color.FromArgb(37, 99, 235);    // Soft Blue
        Color stringColor = isDark ? Color.FromArgb(158, 206, 106) : Color.FromArgb(16, 149, 106);    // Emerald
        Color numberColor = isDark ? Color.FromArgb(255, 158, 59) : Color.FromArgb(217, 119, 6);      // Amber/Gold
        Color commentColor = isDark ? Color.FromArgb(108, 117, 145) : Color.FromArgb(140, 149, 163);  // Muted Slate
        Color symbolColor = isDark ? Color.FromArgb(137, 221, 255) : Color.FromArgb(8, 145, 178);     // Cyan

        // Reset full text color
        box.SelectAll();
        box.SelectionColor = defaultTextColor;

        string text = box.Text;
        MatchCollection matches = TokenRegex.Matches(text);

        foreach (Match match in matches)
        {
            // Comment
            if (match.Groups[1].Success || match.Groups[2].Success)
            {
                box.Select(match.Index, match.Length);
                box.SelectionColor = commentColor;
            }
            // String literal
            else if (match.Groups[3].Success || match.Groups[4].Success)
            {
                box.Select(match.Index, match.Length);
                box.SelectionColor = stringColor;
            }
            // Number
            else if (match.Groups[5].Success)
            {
                box.Select(match.Index, match.Length);
                box.SelectionColor = numberColor;
            }
            // Word / Identifier / Keyword
            else if (match.Groups[6].Success)
            {
                string word = match.Value;
                if (Keywords.Contains(word))
                {
                    box.Select(match.Index, match.Length);
                    box.SelectionColor = keywordColor;
                }
            }
            // Symbols / Operators
            else if (match.Groups[7].Success)
            {
                box.Select(match.Index, match.Length);
                box.SelectionColor = symbolColor;
            }
        }

        // Restore selection and unfreeze redraw
        box.Select(originalSelectionStart, originalSelectionLength);
        SendMessage(box.Handle, WM_SETREDRAW, 1, 0);
        box.Invalidate();
    }
}
