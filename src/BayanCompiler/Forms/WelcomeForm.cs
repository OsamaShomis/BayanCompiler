namespace BayanCompiler.Forms;

/// <summary>
/// Modern, elegant welcome surface for the Bayan IDE providing quick actions
/// to start a new project, open files, or explore official language samples.
/// </summary>
public sealed class WelcomeForm : Form
{
    /// <summary>
    /// Gets the optional source path selected by the user before entering the editor.
    /// </summary>
    public string? SelectedSourcePath { get; private set; }

    public WelcomeForm()
    {
        Text = "بيان — Bayan IDE";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(880, 640);
        Size = new Size(1040, 720);
        BackColor = IdeTheme.Dark.AppBackground;
        ForeColor = IdeTheme.Dark.TextPrimary;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font = IdeTheme.UiFontRegular;
        AutoScaleMode = AutoScaleMode.Font;
        DoubleBuffered = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = IdeTheme.Dark.AppBackground,
            Padding = new Padding(32, 28, 32, 28)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 180F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

        root.Controls.Add(CreateModernHero(), 0, 0);
        root.Controls.Add(CreateActionArea(), 0, 1);
        root.Controls.Add(CreateFooter(), 0, 2);
        Controls.Add(root);
    }

    private static Control CreateModernHero()
    {
        var hero = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = IdeTheme.Dark.SurfaceElevated,
            Padding = new Padding(24, 20, 24, 20),
            RightToLeft = RightToLeft.Yes
        };
        hero.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84F));
        hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        hero.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        hero.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));

        var mark = new Label
        {
            Text = "◈",
            Dock = DockStyle.Fill,
            BackColor = IdeTheme.Dark.AccentPrimary,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 36F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            AccessibleName = "شعار بيان"
        };
        hero.SetRowSpan(mark, 2);

        var title = new Label
        {
            Text = "بيان  |  Bayan IDE",
            Dock = DockStyle.Fill,
            ForeColor = IdeTheme.Dark.TextPrimary,
            Font = new Font("Segoe UI", 24F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };

        var workflow = new Label
        {
            Text = "بيئة تطوير ومترجم متكامل للغة العربية — x86 Assembly + إخراج تنفيذي حقيقي (output.exe)",
            Dock = DockStyle.Fill,
            ForeColor = IdeTheme.Dark.TextSecondary,
            Font = new Font("Segoe UI", 11F),
            TextAlign = ContentAlignment.MiddleRight
        };

        hero.Controls.Add(mark, 0, 0);
        hero.Controls.Add(title, 1, 0);
        hero.Controls.Add(workflow, 1, 1);
        return hero;
    }

    private Control CreateActionArea()
    {
        var area = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = IdeTheme.Dark.AppBackground,
            Padding = new Padding(0, 24, 0, 16),
            RightToLeft = RightToLeft.Yes
        };
        area.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
        area.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));

        // Left Panel: Quick Actions
        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = IdeTheme.Dark.AppBackground,
            Padding = new Padding(0, 0, 16, 0),
            RightToLeft = RightToLeft.Yes
        };
        actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34F));

        var heading = new Label
        {
            Text = "ابدأ العمل في المحرر",
            Dock = DockStyle.Fill,
            ForeColor = IdeTheme.Dark.TextPrimary,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        var description = new Label
        {
            Text = "اختر نقطة البداية المناسبة للدخول إلى بيئة التطوير واستعراض مراحل الترجمة.",
            Dock = DockStyle.Fill,
            ForeColor = IdeTheme.Dark.TextMuted,
            Font = new Font("Segoe UI", 9.5F),
            TextAlign = ContentAlignment.MiddleRight
        };

        actions.Controls.Add(heading, 0, 0);
        actions.Controls.Add(description, 0, 1);
        actions.Controls.Add(CreateActionCard("برنامج بيان جديد 📄", "فتح محرر الكود مع قالب برنامج جديد جاهز للتعديل.", IdeTheme.Dark.AccentPrimary, (_, _) => EnterEditor(null)), 0, 2);
        actions.Controls.Add(CreateActionCard("فتح ملف كود (.bayan) 📂", "استعراض ملف برنامج بيان محفوظ مسبقاً على جهازك.", IdeTheme.Dark.SurfaceElevated, (_, _) => SelectSourceFile(false)), 0, 3);
        actions.Controls.Add(CreateActionCard("اختيار عينة رسمية للمشروع 📋", "استعراض واختبار إحدى العينات الرسمية المعتمدة للغة.", IdeTheme.Dark.SurfaceElevated, (_, _) => SelectSourceFile(true)), 0, 4);

        // Right Panel: Architectural Specs Card
        var pipelineCard = CreatePipelineCard();

        area.Controls.Add(actions, 0, 0);
        area.Controls.Add(pipelineCard, 1, 0);
        return area;
    }

    private Control CreateActionCard(string title, string description, Color bg, EventHandler clickHandler)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = bg,
            Padding = new Padding(16, 12, 16, 12),
            Margin = new Padding(0, 6, 0, 6),
            Cursor = Cursors.Hand
        };

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 26,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleRight,
            Cursor = Cursors.Hand
        };

        var descLabel = new Label
        {
            Text = description,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = IdeTheme.Dark.TextSecondary,
            TextAlign = ContentAlignment.MiddleRight,
            Cursor = Cursors.Hand
        };

        panel.Click += clickHandler;
        titleLabel.Click += clickHandler;
        descLabel.Click += clickHandler;

        panel.Controls.Add(descLabel);
        panel.Controls.Add(titleLabel);
        return panel;
    }

    private static Control CreatePipelineCard()
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = IdeTheme.Dark.SurfaceElevated,
            Padding = new Padding(20),
            Margin = new Padding(12, 6, 0, 6)
        };

        var title = new Label
        {
            Text = "مراحل خط الأنابيب (Compiler Pipeline)",
            Dock = DockStyle.Top,
            Height = 32,
            ForeColor = IdeTheme.Dark.AccentCyan,
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };

        var steps = new Label
        {
            Text = "01. المصدر (Source Editor)\n" +
                   "02. الرموز المعجمية (Tokens)\n" +
                   "03. شجرة التحليل (Parse Tree)\n" +
                   "04. جدول الرموز (Symbol Table)\n" +
                   "05. الفحص الدلالي (Semantic Analysis)\n" +
                   "06. الكود الوسيط (TAC / IR)\n" +
                   "07. كود التجميع (x86 Assembly)\n" +
                   "08. قائمة التشخيصات والأخطاء (Diagnostics)\n" +
                   "09. التنفيذ والطرفية (output.exe via ilasm)",
            Dock = DockStyle.Fill,
            ForeColor = IdeTheme.Dark.TextSecondary,
            Font = new Font("Cascadia Mono", 9.5F),
            TextAlign = ContentAlignment.TopRight,
            Padding = new Padding(0, 10, 0, 0)
        };

        card.Controls.Add(steps);
        card.Controls.Add(title);
        return card;
    }

    private void SelectSourceFile(bool preferSamples)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Bayan source files (*.bayan)|*.bayan|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = preferSamples ? "اختيار عينة بيان" : "فتح برنامج بيان",
            InitialDirectory = preferSamples ? FindSamplesDirectory() : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            EnterEditor(dialog.FileName);
        }
    }

    private static string FindSamplesDirectory()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            string candidate = Path.Combine(current.FullName, "samples");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private void EnterEditor(string? sourcePath)
    {
        SelectedSourcePath = sourcePath;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static Control CreateFooter()
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = "بيئة التطوير المستقلة — المترجم التنفيذي: Bayan.Compiler.Cli — الهدف التنفيذي: output.exe",
            ForeColor = IdeTheme.Dark.TextMuted,
            Font = new Font("Segoe UI", 9F),
            TextAlign = ContentAlignment.MiddleCenter
        };
    }
}
