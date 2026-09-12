namespace BayanCompiler.Forms;

/// <summary>
/// A sleek IDE terminal control displaying program output and supporting interactive input for 'اقرأ'.
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

    private readonly Panel inputPanel = new();
    private readonly Label promptLabel = new();
    private readonly TextBox inputTextBox = new();
    private readonly Button sendInputButton = new();

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
        // 1. Header
        headerPanel.Dock = DockStyle.Top;
        headerPanel.Height = 42;
        headerPanel.Padding = new Padding(12, 6, 12, 6);

        titleLabel.Text = "طرفية بيان  |  output.exe";
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

        rerunButton.Text = "إعادة تشغيل ▶";
        rerunButton.Font = IdeTheme.BadgeFont;
        rerunButton.Dock = DockStyle.Left;
        rerunButton.Width = 100;
        rerunButton.FlatStyle = FlatStyle.Flat;
        rerunButton.FlatAppearance.BorderSize = 0;
        rerunButton.Cursor = Cursors.Hand;
        rerunButton.Click += (_, _) => RerunRequested?.Invoke(this, EventArgs.Empty);

        headerPanel.Controls.Add(rerunButton);
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
        inputTextBox.Font = IdeTheme.TerminalFont;
        inputTextBox.BorderStyle = BorderStyle.FixedSingle;
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

        // 3. Console Output
        consoleBox.Dock = DockStyle.Fill;
        consoleBox.ReadOnly = true;
        consoleBox.Font = IdeTheme.TerminalFont;
        consoleBox.BorderStyle = BorderStyle.None;
        consoleBox.ScrollBars = RichTextBoxScrollBars.Both;
        consoleBox.RightToLeft = RightToLeft.No; // Code output is LTR standard
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

    public void SetResult(string stdout, string? stderr, int exitCode, TimeSpan elapsed)
    {
        consoleBox.Clear();

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            AppendColoredText(stdout + Environment.NewLine, IdeTheme.TerminalText);
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            AppendColoredText(stderr + Environment.NewLine, IdeTheme.AccentError);
        }

        if (string.IsNullOrWhiteSpace(stdout) && string.IsNullOrWhiteSpace(stderr))
        {
            AppendColoredText("[اكتمل تنفيذ البرنامج دون مخرجات نصية]" + Environment.NewLine, IdeTheme.TextMuted);
        }

        bool success = exitCode == 0;
        statusBadge.Text = success ? $"نجاح (0)" : $"خطأ ({exitCode})";
        statusBadge.BackColor = success ? IdeTheme.AccentSuccess : IdeTheme.AccentError;
        statusBadge.ForeColor = Color.White;

        metricsLabel.Text = $"الزمن: {elapsed.TotalMilliseconds:F1} مللي ثانية";
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
        consoleBox.SelectionStart = consoleBox.TextLength;
        consoleBox.SelectionLength = 0;
        consoleBox.SelectionColor = color;
        consoleBox.AppendText(text);
        consoleBox.SelectionColor = consoleBox.ForeColor;
        consoleBox.ScrollToCaret();
    }

    private void SubmitInput()
    {
        string input = inputTextBox.Text;
        inputTextBox.Clear();
        AppendColoredText("> " + input + Environment.NewLine, IdeTheme.TerminalPrompt);
        InputSubmitted?.Invoke(this, input);
    }

    public string GetProvidedInput() => inputTextBox.Text;
}
