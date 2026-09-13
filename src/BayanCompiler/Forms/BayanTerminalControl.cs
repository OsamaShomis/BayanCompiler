using System.Text;
using System.Text.RegularExpressions;

namespace BayanCompiler.Forms;

/// <summary>
/// A sleek, Arabic-first IDE terminal control displaying program output cleanly from Right-to-Left (RTL),
/// preventing bidirectional scrambling of numbers, brackets, colons, and labels (like أ and ب).
/// </summary>
public sealed class BayanTerminalControl : UserControl
{
    private readonly RichTextBox consoleBox = new();
    private readonly Panel headerPanel = new();
    private readonly Label titleLabel = new();
    private readonly Label statusBadge = new();
    private readonly Label metricsLabel = new();
    private readonly Button clearButton = new();
    private readonly Button rerunButton = new();
    private readonly Button directionButton = new();

    private readonly Panel inputPanel = new();
    private readonly Label promptLabel = new();
    private readonly TextBox inputTextBox = new();
    private readonly Button sendInputButton = new();

    private bool isRtl = true;

    public event EventHandler? RerunRequested;
    public event EventHandler<string>? InputSubmitted;

    public BayanTerminalControl()
    {
        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        RightToLeft = RightToLeft.Yes;
        Font = IdeTheme.UiFontRegular;

        InitializeComponents();
        ApplyTheme();
    }

    private void InitializeComponents()
    {
        // 1. Header Toolbar
        headerPanel.Dock = DockStyle.Top;
        headerPanel.Height = 42;
        headerPanel.Padding = new Padding(12, 6, 12, 6);

        titleLabel.Text = "طرفية تشغيل بيان  |  output.exe";
        titleLabel.Font = IdeTheme.SubHeaderFont;
        titleLabel.AutoSize = true;
        titleLabel.Dock = DockStyle.Right;
        titleLabel.TextAlign = ContentAlignment.MiddleCenter;

        statusBadge.Text = "جاهز";
        statusBadge.Font = IdeTheme.BadgeFont;
        statusBadge.AutoSize = true;
        statusBadge.Dock = DockStyle.Right;
        statusBadge.Padding = new Padding(10, 4, 10, 4);
        statusBadge.Margin = new Padding(8, 2, 8, 2);
        statusBadge.TextAlign = ContentAlignment.MiddleCenter;

        metricsLabel.Text = "";
        metricsLabel.Font = IdeTheme.UiFontRegular;
        metricsLabel.AutoSize = true;
        metricsLabel.Dock = DockStyle.Right;
        metricsLabel.Padding = new Padding(8, 4, 8, 4);
        metricsLabel.TextAlign = ContentAlignment.MiddleCenter;

        clearButton.Text = "مسح";
        clearButton.Font = IdeTheme.BadgeFont;
        clearButton.Dock = DockStyle.Left;
        clearButton.Width = 60;
        clearButton.FlatStyle = FlatStyle.Flat;
        clearButton.FlatAppearance.BorderSize = 0;
        clearButton.Cursor = Cursors.Hand;
        clearButton.Click += (_, _) => consoleBox.Clear();

        directionButton.Text = "⮂ اتجاه العرض: عربي (RTL)";
        directionButton.Font = IdeTheme.BadgeFont;
        directionButton.Dock = DockStyle.Left;
        directionButton.Width = 160;
        directionButton.FlatStyle = FlatStyle.Flat;
        directionButton.FlatAppearance.BorderSize = 0;
        directionButton.Cursor = Cursors.Hand;
        directionButton.Click += (_, _) => ToggleDirection();

        rerunButton.Text = "إعادة تشغيل ▶";
        rerunButton.Font = IdeTheme.BadgeFont;
        rerunButton.Dock = DockStyle.Left;
        rerunButton.Width = 100;
        rerunButton.FlatStyle = FlatStyle.Flat;
        rerunButton.FlatAppearance.BorderSize = 0;
        rerunButton.Cursor = Cursors.Hand;
        rerunButton.Click += (_, _) => RerunRequested?.Invoke(this, EventArgs.Empty);

        headerPanel.Controls.Add(rerunButton);
        headerPanel.Controls.Add(directionButton);
        headerPanel.Controls.Add(clearButton);
        headerPanel.Controls.Add(metricsLabel);
        headerPanel.Controls.Add(statusBadge);
        headerPanel.Controls.Add(titleLabel);

        // 2. Input Panel (for interactive stdin)
        inputPanel.Dock = DockStyle.Bottom;
        inputPanel.Height = 44;
        inputPanel.Padding = new Padding(10, 6, 10, 6);

        promptLabel.Text = "مدخلات اقرأ :";
        promptLabel.Font = IdeTheme.UiFontBold;
        promptLabel.AutoSize = true;
        promptLabel.Dock = DockStyle.Right;
        promptLabel.TextAlign = ContentAlignment.MiddleCenter;

        sendInputButton.Text = "إرسال ↵";
        sendInputButton.Font = IdeTheme.BadgeFont;
        sendInputButton.Dock = DockStyle.Left;
        sendInputButton.Width = 72;
        sendInputButton.FlatStyle = FlatStyle.Flat;
        sendInputButton.FlatAppearance.BorderSize = 0;
        sendInputButton.Cursor = Cursors.Hand;
        sendInputButton.Click += (_, _) => SubmitInput();

        inputTextBox.Dock = DockStyle.Fill;
        inputTextBox.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
        inputTextBox.BorderStyle = BorderStyle.FixedSingle;
        inputTextBox.RightToLeft = RightToLeft.Yes;
        inputTextBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SubmitInput();
            }
        };

        inputPanel.Controls.Add(inputTextBox);
        inputPanel.Controls.Add(promptLabel);
        inputPanel.Controls.Add(sendInputButton);

        // 3. Console Output Viewer (Arabic RTL configured by default)
        consoleBox.Dock = DockStyle.Fill;
        consoleBox.ReadOnly = true;
        consoleBox.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
        consoleBox.BorderStyle = BorderStyle.None;
        consoleBox.ScrollBars = RichTextBoxScrollBars.Both;
        consoleBox.RightToLeft = RightToLeft.Yes; // Native Arabic Right-to-Left
        consoleBox.Padding = new Padding(12);

        Controls.Add(consoleBox);
        Controls.Add(inputPanel);
        Controls.Add(headerPanel);
    }

    public void ApplyTheme()
    {
        BackColor = IdeTheme.TerminalBg;
        headerPanel.BackColor = IdeTheme.SurfaceElevated;
        titleLabel.ForeColor = IdeTheme.TextPrimary;

        statusBadge.BackColor = IdeTheme.Surface;
        statusBadge.ForeColor = IdeTheme.TextSecondary;
        metricsLabel.ForeColor = IdeTheme.TextMuted;

        clearButton.BackColor = IdeTheme.SurfaceHover;
        clearButton.ForeColor = IdeTheme.TextSecondary;

        directionButton.BackColor = IdeTheme.SurfaceHover;
        directionButton.ForeColor = IdeTheme.AccentCyan;

        rerunButton.BackColor = IdeTheme.AccentSuccess;
        rerunButton.ForeColor = Color.White;

        consoleBox.BackColor = IdeTheme.TerminalBg;
        consoleBox.ForeColor = IdeTheme.TerminalText;

        inputPanel.BackColor = IdeTheme.SurfaceElevated;
        promptLabel.ForeColor = IdeTheme.AccentCyan;
        inputTextBox.BackColor = IdeTheme.TerminalBg;
        inputTextBox.ForeColor = IdeTheme.TextPrimary;
        sendInputButton.BackColor = IdeTheme.AccentPrimary;
        sendInputButton.ForeColor = Color.White;
    }

    private void ToggleDirection()
    {
        isRtl = !isRtl;
        consoleBox.RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;
        directionButton.Text = isRtl ? "⮂ اتجاه العرض: عربي (RTL)" : "⮀ اتجاه العرض: إنجليزي (LTR)";
        
        consoleBox.SelectAll();
        consoleBox.SelectionAlignment = isRtl ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        consoleBox.DeselectAll();
    }

    public void SetResult(string stdout, string? stderr, int exitCode, TimeSpan elapsed)
    {
        consoleBox.Clear();
        consoleBox.RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;
        consoleBox.SelectionAlignment = isRtl ? HorizontalAlignment.Right : HorizontalAlignment.Left;

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            AppendFormattedOutput(stdout);
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            AppendColoredText("\n[تنبيهات / أخطاء التشغيل]:\n" + stderr + Environment.NewLine, IdeTheme.AccentError);
        }

        if (string.IsNullOrWhiteSpace(stdout) && string.IsNullOrWhiteSpace(stderr))
        {
            AppendColoredText("[اكتمل تنفيذ البرنامج دون مخرجات نصية]" + Environment.NewLine, IdeTheme.TextMuted);
        }

        bool success = exitCode == 0;
        statusBadge.Text = success ? "نجاح (0)" : $"خطأ ({exitCode})";
        statusBadge.BackColor = success ? IdeTheme.AccentSuccess : IdeTheme.AccentError;
        statusBadge.ForeColor = Color.White;

        metricsLabel.Text = $"زمن التنفيذ: {elapsed.TotalMilliseconds:F1} م.ث";
    }

    private void AppendFormattedOutput(string stdout)
    {
        string[] rawLines = stdout.Replace("\r\n", "\n").Split('\n');

        foreach (string rawLine in rawLines)
        {
            string line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                AppendColoredText(Environment.NewLine, IdeTheme.TerminalText);
                continue;
            }

            // Detect section headers (e.g. === ... === or --- ... ---)
            if (line.StartsWith("===") || line.StartsWith("---"))
            {
                AppendColoredText(line + Environment.NewLine, IdeTheme.AccentCyan);
                continue;
            }

            // Normal output line
            AppendColoredText(line + Environment.NewLine, IdeTheme.TerminalText);
        }
    }

    public void SetWaiting()
    {
        statusBadge.Text = "جاري التنفيذ...";
        statusBadge.BackColor = IdeTheme.AccentWarning;
        statusBadge.ForeColor = Color.White;
        metricsLabel.Text = "";
    }

    public void AppendText(string text)
    {
        AppendColoredText(text, IdeTheme.TerminalText);
    }

    private void AppendColoredText(string text, Color color)
    {
        string formatted = isRtl ? FormatArabicBidiLine(text) : text;

        consoleBox.SelectionStart = consoleBox.TextLength;
        consoleBox.SelectionLength = 0;
        consoleBox.SelectionColor = color;
        consoleBox.SelectionAlignment = isRtl ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        consoleBox.AppendText(formatted);
        consoleBox.SelectionColor = consoleBox.ForeColor;
        consoleBox.ScrollToCaret();
    }

    /// <summary>
    /// Formats lines with Right-To-Left Mark (RLM \u200F) at start and end so that English characters (like [A] or [B]),
    /// Arabic labels (like [أ] or [ب]), numbers, colons, and operators are rendered in proper RTL reading order without scrambling.
    /// </summary>
    private static string FormatArabicBidiLine(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        string trimmed = text.TrimEnd('\r', '\n');
        string eol = text.EndsWith(Environment.NewLine) ? Environment.NewLine : (text.EndsWith("\n") ? "\n" : "");

        if (string.IsNullOrEmpty(trimmed))
        {
            return eol;
        }

        // Anchor with RLM (\u200F) at both ends of the line content
        return "\u200F" + trimmed + "\u200F" + eol;
    }

    private void SubmitInput()
    {
        string input = inputTextBox.Text;
        inputTextBox.Clear();
        AppendColoredText("> " + input + Environment.NewLine, IdeTheme.TerminalPrompt);
        InputSubmitted?.Invoke(this, input);
    }

    public string GetProvidedInput() => inputTextBox.Text;

    public void ClearAndReset()
    {
        consoleBox.Clear();
        statusBadge.Text = "جاهز";
        statusBadge.BackColor = IdeTheme.Surface;
        statusBadge.ForeColor = IdeTheme.TextSecondary;
        metricsLabel.Text = "";
    }
}
