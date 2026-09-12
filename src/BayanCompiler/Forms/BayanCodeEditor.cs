using System.Runtime.InteropServices;

namespace BayanCompiler.Forms;

/// <summary>
/// A modern code editor control for Bayan source code featuring line numbers,
/// debounced syntax highlighting, and an integrated status bar.
/// </summary>
public sealed class BayanCodeEditor : UserControl
{
    private readonly RichTextBox editor = new();
    private readonly Panel gutter = new();
    private readonly StatusStrip statusBar = new();
    private readonly ToolStripStatusLabel cursorStatusLabel = new();
    private readonly ToolStripStatusLabel statsLabel = new();
    private readonly ToolStripStatusLabel langLabel = new();
    private readonly System.Windows.Forms.Timer highlightDebounceTimer = new();

    public event EventHandler? ContentModified;
    public event Action<int, int>? CursorPositionChanged;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string SourceCode
    {
        get => editor.Text;
        set
        {
            editor.Text = value;
            ApplyHighlighting();
            UpdateStatus();
            gutter.Invalidate();
        }
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public RichTextBox InnerEditor => editor;

    public BayanCodeEditor()
    {
        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        RightToLeft = RightToLeft.No; // LTR for programming code to avoid inverted operators and braces
        Font = IdeTheme.UiFontRegular;

        // Debounce timer for syntax highlighting
        highlightDebounceTimer.Interval = 350;
        highlightDebounceTimer.Tick += (_, _) =>
        {
            highlightDebounceTimer.Stop();
            ApplyHighlighting();
        };

        InitializeComponents();
        ApplyTheme();
    }

    private void InitializeComponents()
    {
        // Gutter for line numbers
        gutter.Width = 48;
        gutter.Dock = DockStyle.Left; // Left side line numbers
        gutter.Paint += Gutter_Paint;

        // Code editor RichTextBox
        editor.Dock = DockStyle.Fill;
        editor.Font = IdeTheme.EditorFont;
        editor.AcceptsTab = true;
        editor.WordWrap = false;
        editor.RightToLeft = RightToLeft.No; // Ensure LTR for code
        editor.BorderStyle = BorderStyle.None;
        editor.ScrollBars = RichTextBoxScrollBars.Both;
        editor.DetectUrls = false;

        editor.TextChanged += (_, _) =>
        {
            highlightDebounceTimer.Stop();
            highlightDebounceTimer.Start();
            UpdateStatus();
            gutter.Invalidate();
            ContentModified?.Invoke(this, EventArgs.Empty);
        };

        editor.SelectionChanged += (_, _) => UpdateStatus();
        editor.VScroll += (_, _) => gutter.Invalidate();
        editor.Resize += (_, _) => gutter.Invalidate();

        // Editor layout panel
        var editorPanel = new Panel
        {
            Dock = DockStyle.Fill
        };
        editorPanel.Controls.Add(editor);
        editorPanel.Controls.Add(gutter);

        Controls.Add(editorPanel);
    }

    public void ApplyTheme()
    {
        BackColor = IdeTheme.Surface;
        editor.BackColor = IdeTheme.Surface;
        editor.ForeColor = IdeTheme.TextPrimary;

        gutter.BackColor = IdeTheme.GutterBackground;
        ApplyHighlighting();
        gutter.Invalidate();
    }

    private void ApplyHighlighting()
    {
        SyntaxHighlightingService.ApplyHighlighting(editor, IdeTheme.IsDarkMode);
    }

    private void UpdateStatus()
    {
        int charIndex = editor.SelectionStart;
        int line = editor.GetLineFromCharIndex(charIndex);
        int col = charIndex - editor.GetFirstCharIndexFromLine(line);

        CursorPositionChanged?.Invoke(line + 1, col + 1);
    }

    private void Gutter_Paint(object? sender, PaintEventArgs e)
    {
        e.Graphics.Clear(IdeTheme.GutterBackground);

        if (string.IsNullOrEmpty(editor.Text))
        {
            using var emptyFont = new Font("Cascadia Mono", 9F, FontStyle.Regular);
            using var emptyBrush = new SolidBrush(IdeTheme.GutterText);
            using var emptyFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            var emptyRect = new Rectangle(0, 4, gutter.Width, 20);
            e.Graphics.DrawString("1", emptyFont, emptyBrush, emptyRect, emptyFormat);
            using var emptyBorderPen = new Pen(IdeTheme.Border, 1);
            e.Graphics.DrawLine(emptyBorderPen, gutter.Width - 1, 0, gutter.Width - 1, gutter.Height);
            return;
        }

        using var font = new Font("Cascadia Mono", 9F, FontStyle.Regular);
        using var brush = new SolidBrush(IdeTheme.GutterText);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        // Get visible line range
        int firstChar = editor.GetCharIndexFromPosition(new Point(0, 0));
        int firstLine = editor.GetLineFromCharIndex(firstChar);

        int lastChar = editor.GetCharIndexFromPosition(new Point(0, editor.Height));
        int lastLine = editor.GetLineFromCharIndex(lastChar);

        for (int i = firstLine; i <= lastLine && i < editor.Lines.Length; i++)
        {
            int lineChar = editor.GetFirstCharIndexFromLine(i);
            Point pt = editor.GetPositionFromCharIndex(lineChar);

            var rect = new Rectangle(0, pt.Y, gutter.Width, 20);
            e.Graphics.DrawString((i + 1).ToString(), font, brush, rect, format);
        }

        // Draw dividing border line on the right edge of the gutter
        using var borderPen = new Pen(IdeTheme.Border, 1);
        e.Graphics.DrawLine(borderPen, gutter.Width - 1, 0, gutter.Width - 1, gutter.Height);
    }
}
