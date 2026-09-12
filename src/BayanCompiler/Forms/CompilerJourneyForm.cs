using System.Diagnostics;
using System.Text.RegularExpressions;

namespace BayanCompiler.Forms;

/// <summary>
/// Professional, rock-solid IDE surface for the Bayan programming language compiler.
/// Implements the Golden Tri-Zone layout with a dedicated ToolStrip command bar,
/// large LTR code editor, tabbed Compiler Inspector (DataGridView tokens/symbols and TreeView AST),
/// full-width bottom Terminal and Error List with double-click line navigation.
/// </summary>
public sealed class CompilerJourneyForm : Form
{
    // Standalone compiler and execution clients
    private readonly CliCompilationClient compilerClient = new();
    private readonly WindowsExecutableRuntimeClient executableRuntimeClient = new();

    // Core workspace editor and terminal
    private readonly BayanCodeEditor sourceEditor = new();
    private readonly BayanTerminalControl terminalControl = new();

    // Layout containers
    private readonly SplitContainer mainSplitter = new();   // Top Workspace vs Bottom Output Panel
    private readonly SplitContainer upperSplitter = new();  // Code Editor (65%) vs Compiler Inspector (35%)
    private readonly TabControl inspectorTabs = new();      // Tokens, Parse Tree, Symbol Table, Semantics, TAC, Assembly
    private readonly TabControl bottomTabs = new();         // Terminal, Error List, Performance

    // Inspector components (Tab 1-6)
    private readonly DataGridView dgvTokens = new();
    private readonly TreeView tvParseTree = new();
    private readonly DataGridView dgvSymbols = new();
    private readonly RichTextBox txtSemantics = CreateMonospaceViewer(RightToLeft.Yes);
    private readonly RichTextBox txtTac = CreateMonospaceViewer(RightToLeft.No);
    private readonly RichTextBox txtAssembly = CreateMonospaceViewer(RightToLeft.No);

    // Bottom panel components (Tab 1-3)
    private readonly DataGridView dgvErrors = new();
    private readonly RichTextBox txtPerformance = CreateMonospaceViewer(RightToLeft.Yes);

    // Top Command Bar (ToolStrip for clean, overflow-safe layout)
    private readonly ToolStrip toolStrip = new();
    private readonly ToolStripLabel lblTitle = new();
    private readonly ToolStripButton btnNew = new();
    private readonly ToolStripButton btnOpen = new();
    private readonly ToolStripButton btnSave = new();
    private readonly ToolStripButton btnCompile = new();
    private readonly ToolStripButton btnRunExe = new();
    private readonly ToolStripComboBox cmbOfficialSamples = new();
    private readonly ToolStripButton btnToggleTheme = new();
    private readonly ToolStripButton btnToggleInspector = new();
    private readonly ToolStripButton btnToggleBottom = new();

    // Bottom Status Bar
    private readonly StatusStrip statusBar = new();
    private readonly ToolStripStatusLabel lblStatus = new();
    private readonly ToolStripStatusLabel lblCursor = new();
    private readonly ToolStripStatusLabel lblEncoding = new();
    private readonly ToolStripStatusLabel lblCompilerStatus = new();
    private readonly ToolStripStatusLabel lblTarget = new();
    private readonly ToolStripStatusLabel lblDotNet = new();

    // State tracking
    private bool isBusy = false;
    private bool isInspectorVisible = true;
    private bool isBottomVisible = true;
    private string lastExecutablePath = string.Empty;
    private string currentSourcePath = string.Empty;

    public CompilerJourneyForm(string? initialSourcePath = null)
    {
        Text = "بيان  |  Bayan IDE — بيئة التطوير ومترجم لغة بيان الرسمية";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1000, 680);
        Size = new Size(1280, 840);
        RightToLeft = RightToLeft.No; // Keep Form LTR to ensure stable GDI rendering and eliminate tab inversion glitches
        Font = IdeTheme.UiFontRegular;
        DoubleBuffered = true;
        KeyPreview = true;

        InitializeLayout();
        PopulateOfficialSamples();
        ApplyActiveTheme();

        // Wire up keyboard shortcuts
        KeyDown += Form_KeyDown;

        // Wire up cursor tracking from editor to status bar
        sourceEditor.CursorPositionChanged += (line, col) =>
        {
            lblCursor.Text = $"السطر: {line} | العمود: {col}";
        };

        // Wire up double click on error to navigate in code
        dgvErrors.CellDoubleClick += DgvErrors_CellDoubleClick;

        // Load initial program
        if (!string.IsNullOrWhiteSpace(initialSourcePath) && File.Exists(initialSourcePath))
        {
            LoadSourceFile(initialSourcePath);
        }
        else
        {
            sourceEditor.SourceCode =
                "برنامج تجربة;\n" +
                "متغير س : صحيح;\n" +
                "{\n" +
                "    س = 15;\n" +
                "    اطبع(س);\n" +
                "}.";
        }

        // Set responsive splitter distances safely after window is shown
        Shown += (_, _) => ApplyProportionalSplitters();
        Resize += (_, _) =>
        {
            if (WindowState != FormWindowState.Minimized)
            {
                ApplyProportionalSplitters();
            }
        };
    }

    private static RichTextBox CreateMonospaceViewer(RightToLeft rtl)
    {
        return new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            Font = IdeTheme.ViewerCodeFont,
            ScrollBars = RichTextBoxScrollBars.Both,
            WordWrap = false,
            RightToLeft = rtl,
            DetectUrls = false
        };
    }

    private void InitializeLayout()
    {
        Controls.Clear();

        BuildTopCommandBar();
        BuildBottomStatusBar();
        BuildWorkspace();

        Controls.Add(mainSplitter);
        Controls.Add(statusBar);
        Controls.Add(toolStrip);
    }

    private void BuildTopCommandBar()
    {
        toolStrip.Dock = DockStyle.Top;
        toolStrip.Height = 44;
        toolStrip.AutoSize = false;
        toolStrip.GripStyle = ToolStripGripStyle.Hidden;
        toolStrip.RenderMode = ToolStripRenderMode.Professional;
        toolStrip.Renderer = new DarkToolStripRenderer();
        toolStrip.BackColor = IdeTheme.SurfaceElevated;
        toolStrip.Padding = new Padding(8, 4, 8, 4);

        // Title Branding
        lblTitle.Text = " بيان | Bayan IDE ";
        lblTitle.Font = IdeTheme.HeaderFont;
        lblTitle.ForeColor = IdeTheme.AccentCyan;
        lblTitle.Margin = new Padding(0, 0, 8, 0);

        // File Buttons
        ConfigureToolStripButton(btnNew, "📄 جديد", "إنشاء ملف جديد (Ctrl+N)", (_, _) => NewFile());
        ConfigureToolStripButton(btnOpen, "📂 فتح", "فتح ملف .bayan محفوظ (Ctrl+O)", (_, _) => OpenFileDialog());
        ConfigureToolStripButton(btnSave, "💾 حفظ", "حفظ الكود الحالي (Ctrl+S)", (_, _) => SaveFileDialog());

        // Compiler & Execution Buttons
        ConfigureToolStripButton(btnCompile, "⚙ ترجمة (F6)", "ترجمة الكود بمترجم CLI المستقل والتحقق من القواعد (F6)", (_, _) => CompileSource(), true, IdeTheme.AccentPrimary);
        ConfigureToolStripButton(btnRunExe, "▶ تشغيل (F5)", "تشغيل output.exe فوراً في الطرفية التفاعلية (F5)", (_, _) => RunExecutable(), true, IdeTheme.AccentSuccess);
        btnRunExe.Enabled = false;

        // Samples Dropdown
        cmbOfficialSamples.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbOfficialSamples.Width = 190;
        cmbOfficialSamples.Font = IdeTheme.UiFontRegular;
        cmbOfficialSamples.ToolTipText = "اختر من العينات الرسمية المعتمدة للاختبار الفوري";
        cmbOfficialSamples.SelectedIndexChanged += CmbOfficialSamples_SelectedIndexChanged;

        var lblSamplesText = new ToolStripLabel("العينات:")
        {
            Font = IdeTheme.UiFontBold,
            ForeColor = IdeTheme.TextSecondary,
            Margin = new Padding(6, 0, 2, 0)
        };

        // Utility Buttons
        ConfigureToolStripButton(btnToggleTheme, "🌙 السمة", "التبديل بين الوضع الداكن والوضع الفاتح", (_, _) => ToggleTheme());
        ConfigureToolStripButton(btnToggleInspector, "◫ فاحص المترجم", "إظهار أو طي لوحة فاحص مراحل المترجم الجانبية", (_, _) => ToggleInspector());
        ConfigureToolStripButton(btnToggleBottom, "🗖 المخرجات", "إظهار أو طي لوحة الطرفية والأخطاء السفلية", (_, _) => ToggleBottomPanel());

        toolStrip.Items.Add(lblTitle);
        toolStrip.Items.Add(new ToolStripSeparator { Margin = new Padding(4, 0, 4, 0) });
        toolStrip.Items.Add(btnNew);
        toolStrip.Items.Add(btnOpen);
        toolStrip.Items.Add(btnSave);
        toolStrip.Items.Add(new ToolStripSeparator { Margin = new Padding(4, 0, 4, 0) });
        toolStrip.Items.Add(btnCompile);
        toolStrip.Items.Add(btnRunExe);
        toolStrip.Items.Add(new ToolStripSeparator { Margin = new Padding(4, 0, 4, 0) });
        toolStrip.Items.Add(lblSamplesText);
        toolStrip.Items.Add(cmbOfficialSamples);
        toolStrip.Items.Add(new ToolStripSeparator { Margin = new Padding(4, 0, 4, 0) });
        toolStrip.Items.Add(btnToggleTheme);
        toolStrip.Items.Add(btnToggleInspector);
        toolStrip.Items.Add(btnToggleBottom);
    }

    private void BuildBottomStatusBar()
    {
        statusBar.Dock = DockStyle.Bottom;
        statusBar.Height = 30;
        statusBar.SizingGrip = false;
        statusBar.BackColor = IdeTheme.SurfaceElevated;
        statusBar.ForeColor = IdeTheme.TextSecondary;
        statusBar.Font = IdeTheme.UiFontRegular;
        statusBar.Padding = new Padding(8, 2, 8, 2);

        lblStatus.Text = "جاهز — اكتب الكود المصدري ثم اضغط «ترجمة (F6)» أو «تشغيل (F5)»";
        lblStatus.Spring = true;
        lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        lblStatus.ForeColor = IdeTheme.TextPrimary;

        lblCursor.Text = "السطر: 1 | العمود: 1";
        lblCursor.TextAlign = ContentAlignment.MiddleCenter;
        lblCursor.ForeColor = IdeTheme.TextSecondary;

        lblEncoding.Text = "UTF-8";
        lblEncoding.TextAlign = ContentAlignment.MiddleCenter;
        lblEncoding.ForeColor = IdeTheme.TextSecondary;

        lblCompilerStatus.Text = "المترجم: جاهز";
        lblCompilerStatus.TextAlign = ContentAlignment.MiddleCenter;
        lblCompilerStatus.ForeColor = IdeTheme.AccentSuccess;

        lblTarget.Text = "الهدف: x86 EXE";
        lblTarget.TextAlign = ContentAlignment.MiddleCenter;
        lblTarget.ForeColor = IdeTheme.AccentCyan;

        lblDotNet.Text = ".NET 9";
        lblDotNet.TextAlign = ContentAlignment.MiddleCenter;
        lblDotNet.ForeColor = IdeTheme.TextMuted;

        statusBar.Items.Add(lblStatus);
        statusBar.Items.Add(new ToolStripSeparator());
        statusBar.Items.Add(lblCursor);
        statusBar.Items.Add(new ToolStripSeparator());
        statusBar.Items.Add(lblEncoding);
        statusBar.Items.Add(new ToolStripSeparator());
        statusBar.Items.Add(lblCompilerStatus);
        statusBar.Items.Add(new ToolStripSeparator());
        statusBar.Items.Add(lblTarget);
        statusBar.Items.Add(new ToolStripSeparator());
        statusBar.Items.Add(lblDotNet);
    }

    private void BuildWorkspace()
    {
        // Main Splitter: Top (Workspace) vs Bottom (Terminal & Errors)
        mainSplitter.Dock = DockStyle.Fill;
        mainSplitter.Orientation = Orientation.Horizontal;
        mainSplitter.SplitterWidth = 6;
        mainSplitter.BackColor = IdeTheme.Border;

        // Upper Splitter: Code Editor (65%) vs Compiler Inspector (35%)
        upperSplitter.Dock = DockStyle.Fill;
        upperSplitter.Orientation = Orientation.Vertical;
        upperSplitter.SplitterWidth = 6;
        upperSplitter.BackColor = IdeTheme.Border;

        // Code Editor in Panel 1
        sourceEditor.Dock = DockStyle.Fill;
        upperSplitter.Panel1.Controls.Add(sourceEditor);

        // Compiler Inspector in Panel 2
        BuildInspectorTabs();
        upperSplitter.Panel2.Controls.Add(inspectorTabs);

        mainSplitter.Panel1.Controls.Add(upperSplitter);

        // Bottom Output Panel in mainSplitter.Panel2
        BuildBottomTabs();
        mainSplitter.Panel2.Controls.Add(bottomTabs);
    }

    private void BuildInspectorTabs()
    {
        inspectorTabs.Dock = DockStyle.Fill;
        inspectorTabs.Font = IdeTheme.SubHeaderFont;
        inspectorTabs.ItemSize = new Size(115, 28);
        inspectorTabs.SizeMode = TabSizeMode.Normal;

        // 1. Tokens DataGridView
        SetupGrid(dgvTokens);
        dgvTokens.Columns.Add("Index", "#");
        dgvTokens.Columns.Add("Type", "النوع (Token Type)");
        dgvTokens.Columns.Add("Lexeme", "النص / الرمز (Lexeme)");
        dgvTokens.Columns.Add("Location", "الموقع (Location)");
        if (dgvTokens.Columns["Index"] is { } colIdx) colIdx.Width = 45;
        if (dgvTokens.Columns["Location"] is { } colLoc) colLoc.Width = 120;

        // 2. Parse Tree TreeView
        tvParseTree.Dock = DockStyle.Fill;
        tvParseTree.BorderStyle = BorderStyle.None;
        tvParseTree.Font = IdeTheme.ViewerCodeFont;
        tvParseTree.BackColor = IdeTheme.Surface;
        tvParseTree.ForeColor = IdeTheme.TextPrimary;
        tvParseTree.ShowLines = true;
        tvParseTree.ShowPlusMinus = true;
        tvParseTree.ShowRootLines = true;
        tvParseTree.Nodes.Add("لم تُشغَّل الترجمة بعد. اضغط «ترجمة (F6)» لعرض شجرة التحليل.");

        // 3. Symbol Table DataGridView
        SetupGrid(dgvSymbols);
        dgvSymbols.Columns.Add("Name", "الاسم");
        dgvSymbols.Columns.Add("Kind", "الفئة (Kind)");
        dgvSymbols.Columns.Add("Type", "النوع (Type)");
        dgvSymbols.Columns.Add("ReadOnly", "ثابت / متغير");
        dgvSymbols.Columns.Add("Line", "السطر");
        dgvSymbols.Columns.Add("Column", "العمود");
        if (dgvSymbols.Columns["Line"] is { } colLine) colLine.Width = 55;
        if (dgvSymbols.Columns["Column"] is { } colCol) colCol.Width = 55;

        // 4. Semantic Results Viewer
        txtSemantics.Text = "ستظهر هنا نتائج التحقق الدلالي وفحص الأنواع وقواعد النطاقات بعد الترجمة.";

        // 5. TAC Viewer
        txtTac.Text = "; Three-Address Code (TAC)\n; سيظهر الكود الوسيط هنا بعد الترجمة.";

        // 6. Assembly Viewer
        txtAssembly.Text = "; x86 Assembly Code (output.asm)\n; سيظهر كود لغة التجميع المولد هنا بعد الترجمة.";

        inspectorTabs.TabPages.Clear();
        inspectorTabs.TabPages.Add(CreateTabPage("الرموز (Tokens)", dgvTokens));
        inspectorTabs.TabPages.Add(CreateTabPage("شجرة الإعراب (Tree)", tvParseTree));
        inspectorTabs.TabPages.Add(CreateTabPage("جدول الرموز (Symbols)", dgvSymbols));
        inspectorTabs.TabPages.Add(CreateTabPage("التحليل الدلالي", txtSemantics));
        inspectorTabs.TabPages.Add(CreateTabPage("الكود الوسيط (TAC)", txtTac));
        inspectorTabs.TabPages.Add(CreateTabPage("Assembly (x86)", txtAssembly));
    }

    private void BuildBottomTabs()
    {
        bottomTabs.Dock = DockStyle.Fill;
        bottomTabs.Font = IdeTheme.SubHeaderFont;
        bottomTabs.ItemSize = new Size(160, 28);
        bottomTabs.SizeMode = TabSizeMode.Normal;

        terminalControl.Dock = DockStyle.Fill;
        terminalControl.RerunRequested += (_, _) => RunExecutable();

        // Errors DataGridView
        SetupGrid(dgvErrors);
        dgvErrors.Columns.Add("Severity", "النوع");
        dgvErrors.Columns.Add("Line", "السطر");
        dgvErrors.Columns.Add("Column", "العمود");
        dgvErrors.Columns.Add("Message", "رسالة الخطأ / التشخيص");
        if (dgvErrors.Columns["Severity"] is { } colSev) colSev.Width = 140;
        if (dgvErrors.Columns["Line"] is { } colErrLine) colErrLine.Width = 65;
        if (dgvErrors.Columns["Column"] is { } colErrCol) colErrCol.Width = 65;

        // Performance Viewer
        txtPerformance.Text = GetInitialPerformanceText();

        bottomTabs.TabPages.Clear();
        bottomTabs.TabPages.Add(CreateTabPage("🖥️ طرفية التشغيل (Terminal)", terminalControl));
        bottomTabs.TabPages.Add(CreateTabPage("⚠️ قائمة الأخطاء (Errors)", dgvErrors));
        bottomTabs.TabPages.Add(CreateTabPage("📊 تقرير الأداء (Performance)", txtPerformance));
    }

    private static void SetupGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.BackgroundColor = IdeTheme.Surface;
        grid.Font = IdeTheme.UiFontRegular;
        grid.EnableHeadersVisualStyles = false;
    }

    private static TabPage CreateTabPage(string title, Control innerControl)
    {
        var page = new TabPage(title)
        {
            Padding = new Padding(2),
            BackColor = IdeTheme.Surface,
            UseVisualStyleBackColor = false
        };
        innerControl.Dock = DockStyle.Fill;
        page.Controls.Add(innerControl);
        return page;
    }

    private static void ConfigureToolStripButton(ToolStripButton btn, string text, string tooltip, EventHandler onClick, bool isAccent = false, Color? accentColor = null)
    {
        btn.Text = text;
        btn.ToolTipText = tooltip;
        btn.DisplayStyle = ToolStripItemDisplayStyle.Text;
        btn.Font = isAccent ? IdeTheme.UiFontBold : IdeTheme.UiFontRegular;
        btn.Margin = new Padding(3, 2, 3, 2);
        btn.Padding = new Padding(6, 4, 6, 4);
        btn.Click += onClick;

        if (isAccent && accentColor.HasValue)
        {
            btn.BackColor = accentColor.Value;
            btn.ForeColor = Color.White;
        }
        else
        {
            btn.ForeColor = IdeTheme.TextPrimary;
        }
    }

    private void ApplyProportionalSplitters()
    {
        try
        {
            // Vertical split: Upper Workspace (65%) vs Bottom Output (35%)
            if (mainSplitter.Height > 320 && isBottomVisible)
            {
                int targetHeight = (int)(mainSplitter.Height * 0.65);
                int min1 = Math.Max(200, mainSplitter.Panel1MinSize);
                int min2 = Math.Max(120, mainSplitter.Panel2MinSize);
                if (targetHeight >= min1 && targetHeight <= (mainSplitter.Height - min2))
                {
                    mainSplitter.SplitterDistance = targetHeight;
                }
            }

            // Horizontal split: Code Editor (65%) vs Inspector (35%)
            if (upperSplitter.Width > 400 && isInspectorVisible)
            {
                int targetWidth = (int)(upperSplitter.Width * 0.65);
                int min1 = Math.Max(300, upperSplitter.Panel1MinSize);
                int min2 = Math.Max(220, upperSplitter.Panel2MinSize);
                if (targetWidth >= min1 && targetWidth <= (upperSplitter.Width - min2))
                {
                    upperSplitter.SplitterDistance = targetWidth;
                }
            }
        }
        catch { }
    }

    private async void CompileSource()
    {
        if (isBusy)
        {
            return;
        }

        SetBusy(true, "يجري تشغيل مترجم CLI المستقل والتحقق من القواعد...");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            CliCompilationRun run = await compilerClient.CompileAsync(sourceEditor.SourceCode);
            stopwatch.Stop();

            // 1. Populate Tokens DataGridView
            string tokensText = run.GetArtifact("tokens.txt");
            PopulateTokensGrid(tokensText);

            // 2. Populate Parse Tree TreeView
            string parseTreeText = run.GetArtifact("parse-tree.txt");
            PopulateParseTreeView(parseTreeText);

            // 3. Populate Symbol Table DataGridView
            string symbolsText = run.GetArtifact("symbol-table.txt");
            PopulateSymbolTableGrid(symbolsText);

            // 4. Populate Semantic Results
            string diagnostics = run.GetArtifact("diagnostics.txt");
            txtSemantics.Text = string.IsNullOrWhiteSpace(diagnostics)
                ? "تم التحقق الدلالي وفحص الأنواع بنجاح بدون أخطاء."
                : diagnostics;

            // 5. Populate TAC
            txtTac.Text = run.GetArtifact("three-address-code.txt");

            // 6. Populate Assembly
            txtAssembly.Text = run.GetArtifact("output.asm");

            // 7. Populate Error List DataGridView
            PopulateErrorsGrid(diagnostics);

            lastExecutablePath = run.Manifest?.WindowsExecutableAvailable == true
                ? run.Manifest.WindowsExecutablePath
                : string.Empty;

            bool succeeded = run.Manifest?.Success == true && run.ExitCode == 0;
            btnRunExe.Enabled = succeeded && !string.IsNullOrWhiteSpace(lastExecutablePath) && File.Exists(lastExecutablePath);

            // 8. Performance Summary
            txtPerformance.Text =
                $"=== تقرير تشخيص وأداء المترجم ===\n\n" +
                $"• حالة الترجمة: {(succeeded ? "نجاح الترجمة الكاملة بنجاح ✔" : "توقفت الترجمة عند وجود أخطاء ✖")}\n" +
                $"• آخر مرحلة تم الوصول إليها: {run.Manifest?.CompletedStage ?? "غير محدد"}\n" +
                $"• مدة معالجة المترجم: {stopwatch.ElapsedMilliseconds} ms\n" +
                $"• عدد الأخطاء / التشخيصات: {run.Manifest?.DiagnosticCount ?? 0}\n" +
                $"• توفر ملف التنفيذ (output.exe): {(btnRunExe.Enabled ? "نعم — جاهز للتشغيل" : "لا")}\n" +
                $"• مسار الملف التنفيذي: {lastExecutablePath}\n" +
                $"• رمز الخروج للـ CLI: {run.ExitCode}\n\n" +
                $"مخرجات سطر أوامر المترجم:\n{run.StandardOutput}\n{run.StandardError}";

            if (succeeded)
            {
                lblStatus.Text = $"اكتملت الترجمة بنجاح في {stopwatch.ElapsedMilliseconds}ms! تم بناء output.exe و output.asm";
                lblStatus.ForeColor = IdeTheme.AccentSuccess;
                lblCompilerStatus.Text = "المترجم: تم البناء بنجاح";
                lblCompilerStatus.ForeColor = IdeTheme.AccentSuccess;

                // Select Assembly tab to inspect the generated code
                inspectorTabs.SelectedIndex = 5;
            }
            else
            {
                lblStatus.Text = $"توقفت الترجمة عند مرحلة: {run.Manifest?.CompletedStage ?? "الأخطاء"} ({run.Manifest?.DiagnosticCount ?? 0} خطأ)";
                lblStatus.ForeColor = IdeTheme.AccentError;
                lblCompilerStatus.Text = "المترجم: توجد أخطاء";
                lblCompilerStatus.ForeColor = IdeTheme.AccentError;

                // Bring Error List tab into view
                bottomTabs.SelectedIndex = 1;
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            PopulateErrorsGrid($"[خطأ بيئة المحرر] {ex.Message}");
            lblStatus.Text = "تعذر استدعاء المترجم: " + ex.Message;
            lblStatus.ForeColor = IdeTheme.AccentError;
            lblCompilerStatus.Text = "المترجم: فشل الاستدعاء";
            lblCompilerStatus.ForeColor = IdeTheme.AccentError;
            bottomTabs.SelectedIndex = 1;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void PopulateTokensGrid(string tokensText)
    {
        dgvTokens.Rows.Clear();
        if (string.IsNullOrWhiteSpace(tokensText))
        {
            return;
        }

        using var reader = new StringReader(tokensText);
        string? line;
        int index = 1;
        bool headerPassed = false;

        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("---"))
            {
                headerPassed = true;
                continue;
            }

            var parts = line.Split('|');
            if (parts.Length < 3)
            {
                continue;
            }

            if (!headerPassed && (parts[0].Contains("النوع") || parts[0].Contains("Type")))
            {
                headerPassed = true;
                continue;
            }

            string type = parts[0].Trim();
            string lexeme = parts[1].Trim();
            string location = parts[2].Trim();

            dgvTokens.Rows.Add(index++, type, lexeme, location);
        }
    }

    private void PopulateParseTreeView(string parseTreeText)
    {
        tvParseTree.BeginUpdate();
        tvParseTree.Nodes.Clear();

        if (string.IsNullOrWhiteSpace(parseTreeText))
        {
            tvParseTree.Nodes.Add("لم يتم إنتاج شجرة إعراب.");
            tvParseTree.EndUpdate();
            return;
        }

        var stack = new Stack<(int level, TreeNode node)>();
        using var reader = new StringReader(parseTreeText);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            int leadingSpaces = 0;
            while (leadingSpaces < line.Length && line[leadingSpaces] == ' ')
            {
                leadingSpaces++;
            }

            int level = leadingSpaces / 2;
            string text = line.Trim();
            var node = new TreeNode(text);

            while (stack.Count > 0 && stack.Peek().level >= level)
            {
                stack.Pop();
            }

            if (stack.Count == 0)
            {
                tvParseTree.Nodes.Add(node);
            }
            else
            {
                stack.Peek().node.Nodes.Add(node);
            }

            stack.Push((level, node));
        }

        if (tvParseTree.Nodes.Count > 0)
        {
            tvParseTree.Nodes[0].Expand();
            if (tvParseTree.Nodes[0].Nodes.Count > 0)
            {
                tvParseTree.Nodes[0].Nodes[0].Expand();
            }
        }

        tvParseTree.EndUpdate();
    }

    private void PopulateSymbolTableGrid(string symbolsText)
    {
        dgvSymbols.Rows.Clear();
        if (string.IsNullOrWhiteSpace(symbolsText))
        {
            return;
        }

        using var reader = new StringReader(symbolsText);
        string? line;
        bool headerPassed = false;

        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("---"))
            {
                if (line.StartsWith("---"))
                {
                    headerPassed = true;
                }
                continue;
            }

            var parts = line.Split('|');
            if (parts.Length < 4)
            {
                continue;
            }

            if (!headerPassed && parts[0].Trim().Equals("Name", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string name = parts[0].Trim();
            string kind = parts.Length > 1 ? parts[1].Trim() : "";
            string type = parts.Length > 2 ? parts[2].Trim() : "";
            string readOnly = parts.Length > 3 ? parts[3].Trim() : "";
            string lineNum = parts.Length > 4 ? parts[4].Trim() : "";
            string colNum = parts.Length > 5 ? parts[5].Trim() : "";

            dgvSymbols.Rows.Add(name, kind, type, readOnly, lineNum, colNum);
        }
    }

    private void PopulateErrorsGrid(string diagnosticsText)
    {
        dgvErrors.Rows.Clear();
        if (string.IsNullOrWhiteSpace(diagnosticsText))
        {
            return;
        }

        using var reader = new StringReader(diagnosticsText);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string severity = "خطأ (Error)";
            int lineNum = 1;
            int colNum = 1;
            string message = line;

            var match = Regex.Match(line, @"(?:السطر|line)\s*:?\s*(\d+)(?:[^\d]+(?:العمود|column|col)\s*:?\s*(\d+))?", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                int.TryParse(match.Groups[1].Value, out lineNum);
                if (match.Groups[2].Success)
                {
                    int.TryParse(match.Groups[2].Value, out colNum);
                }
            }

            if (line.Contains("تحذير") || line.Contains("Warning", StringComparison.OrdinalIgnoreCase))
            {
                severity = "تحذير (Warning)";
            }
            else if (line.Contains("دلالي") || line.Contains("Semantic", StringComparison.OrdinalIgnoreCase))
            {
                severity = "خطأ دلالي (Semantic)";
            }
            else if (line.Contains("نحوي") || line.Contains("Syntax", StringComparison.OrdinalIgnoreCase) || line.Contains("Parse", StringComparison.OrdinalIgnoreCase))
            {
                severity = "خطأ نحوي (Syntax)";
            }

            dgvErrors.Rows.Add(severity, lineNum, colNum, message);
        }
    }

    private void DgvErrors_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0 && e.RowIndex < dgvErrors.Rows.Count)
        {
            var row = dgvErrors.Rows[e.RowIndex];
            if (row.Cells["Line"].Value is int lineNum && lineNum > 0)
            {
                JumpToEditorLine(lineNum);
            }
            else if (int.TryParse(row.Cells["Line"].Value?.ToString(), out int parsedLine) && parsedLine > 0)
            {
                JumpToEditorLine(parsedLine);
            }
        }
    }

    private void JumpToEditorLine(int targetLine)
    {
        if (targetLine <= 0)
        {
            return;
        }

        var rtb = sourceEditor.InnerEditor;
        if (rtb.Lines.Length == 0)
        {
            return;
        }

        int targetIndex = Math.Min(targetLine - 1, rtb.Lines.Length - 1);
        int charIndex = rtb.GetFirstCharIndexFromLine(targetIndex);
        if (charIndex >= 0)
        {
            rtb.SelectionStart = charIndex;
            rtb.SelectionLength = rtb.Lines[targetIndex].Length;
            rtb.ScrollToCaret();
            rtb.Focus();
            lblStatus.Text = $"تم الانتقال إلى السطر {targetLine}";
        }
    }

    private async void RunExecutable()
    {
        if (isBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(lastExecutablePath) || !File.Exists(lastExecutablePath))
        {
            MessageBox.Show(this, "يرجى تشغيل «ترجمة (F6)» أولاً لبناء ملف output.exe.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Switch to terminal tab in bottom panel and ensure bottom panel is visible
        if (!isBottomVisible)
        {
            ToggleBottomPanel();
        }
        bottomTabs.SelectedIndex = 0;
        terminalControl.SetWaiting();
        SetBusy(true, "يجري تشغيل output.exe في الطرفية التفاعلية...");

        var stopwatch = Stopwatch.StartNew();
        try
        {
            string input = terminalControl.GetProvidedInput();
            WindowsExecutableRun run = await executableRuntimeClient.ExecuteAsync(lastExecutablePath, input);
            stopwatch.Stop();

            terminalControl.SetResult(run.StandardOutput, run.StandardError, run.ExitCode, stopwatch.Elapsed);
            lblStatus.Text = run.ExitCode == 0 ? $"اكتمل تنفيذ output.exe بنجاح (كود 0) في {stopwatch.ElapsedMilliseconds}ms." : $"انتهى البرنامج برمز خروج ({run.ExitCode}).";
            lblStatus.ForeColor = run.ExitCode == 0 ? IdeTheme.AccentSuccess : IdeTheme.AccentError;
        }
        catch (TimeoutException)
        {
            stopwatch.Stop();
            terminalControl.SetResult("", "[تجاوز وقت التنفيذ] استغرق البرنامج وقتاً أطول من المسموح.", -1, stopwatch.Elapsed);
            lblStatus.Text = "تجاوز البرنامج الحد الزمني المسموح به.";
            lblStatus.ForeColor = IdeTheme.AccentError;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            terminalControl.SetResult("", "[خطأ تشغيل] " + ex.Message, -1, stopwatch.Elapsed);
            lblStatus.Text = "تعذر تشغيل الملف التنفيذي: " + ex.Message;
            lblStatus.ForeColor = IdeTheme.AccentError;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ToggleInspector()
    {
        isInspectorVisible = !isInspectorVisible;
        upperSplitter.Panel2Collapsed = !isInspectorVisible;
        btnToggleInspector.Text = isInspectorVisible ? "◫ فاحص المترجم" : "🗖 إظهار الفاحص";
        if (isInspectorVisible)
        {
            ApplyProportionalSplitters();
        }
    }

    private void ToggleBottomPanel()
    {
        isBottomVisible = !isBottomVisible;
        mainSplitter.Panel2Collapsed = !isBottomVisible;
        btnToggleBottom.Text = isBottomVisible ? "🗖 المخرجات" : "🗖 إظهار المخرجات";
        if (isBottomVisible)
        {
            ApplyProportionalSplitters();
        }
    }

    private void ToggleTheme()
    {
        IdeTheme.IsDarkMode = !IdeTheme.IsDarkMode;
        btnToggleTheme.Text = IdeTheme.IsDarkMode ? "🌙 السمة" : "☀️ السمة";
        ApplyActiveTheme();
    }

    private void ApplyActiveTheme()
    {
        BackColor = IdeTheme.AppBackground;
        ForeColor = IdeTheme.TextPrimary;

        toolStrip.BackColor = IdeTheme.SurfaceElevated;
        statusBar.BackColor = IdeTheme.SurfaceElevated;
        statusBar.ForeColor = IdeTheme.TextSecondary;

        sourceEditor.ApplyTheme();
        terminalControl.ApplyTheme();

        // Style viewers
        Color viewBg = IdeTheme.Surface;
        Color viewFg = IdeTheme.TextPrimary;

        txtSemantics.BackColor = viewBg;
        txtSemantics.ForeColor = viewFg;

        txtTac.BackColor = viewBg;
        txtTac.ForeColor = viewFg;

        txtAssembly.BackColor = viewBg;
        txtAssembly.ForeColor = IdeTheme.IsDarkMode ? Color.FromArgb(147, 197, 253) : Color.FromArgb(30, 64, 175);

        txtPerformance.BackColor = viewBg;
        txtPerformance.ForeColor = viewFg;

        tvParseTree.BackColor = viewBg;
        tvParseTree.ForeColor = viewFg;

        ApplyGridTheme(dgvTokens);
        ApplyGridTheme(dgvSymbols);
        ApplyGridTheme(dgvErrors);

        // Style tabs
        foreach (TabPage page in inspectorTabs.TabPages)
        {
            page.BackColor = IdeTheme.Surface;
        }

        foreach (TabPage page in bottomTabs.TabPages)
        {
            page.BackColor = IdeTheme.Surface;
        }

        cmbOfficialSamples.BackColor = IdeTheme.SurfaceElevated;
        cmbOfficialSamples.ForeColor = IdeTheme.TextPrimary;
    }

    private static void ApplyGridTheme(DataGridView grid)
    {
        grid.BackgroundColor = IdeTheme.Surface;
        grid.DefaultCellStyle.BackColor = IdeTheme.Surface;
        grid.DefaultCellStyle.ForeColor = IdeTheme.TextPrimary;
        grid.DefaultCellStyle.SelectionBackColor = IdeTheme.AccentPrimary;
        grid.DefaultCellStyle.SelectionForeColor = Color.White;

        grid.ColumnHeadersDefaultCellStyle.BackColor = IdeTheme.SurfaceElevated;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = IdeTheme.TextPrimary;
        grid.GridColor = IdeTheme.Border;
    }

    private void SetBusy(bool busy, string message = "")
    {
        isBusy = busy;
        UseWaitCursor = busy;

        btnCompile.Enabled = !busy;
        btnRunExe.Enabled = !busy && !string.IsNullOrWhiteSpace(lastExecutablePath) && File.Exists(lastExecutablePath);

        if (!string.IsNullOrEmpty(message))
        {
            lblStatus.Text = message;
        }
    }

    private void NewFile()
    {
        sourceEditor.SourceCode =
            "برنامج تجربة_جديدة;\n" +
            "متغير س : صحيح;\n" +
            "{\n" +
            "    س = 100;\n" +
            "    اطبع(س);\n" +
            "}.";
        currentSourcePath = string.Empty;
        lastExecutablePath = string.Empty;
        btnRunExe.Enabled = false;
        dgvTokens.Rows.Clear();
        tvParseTree.Nodes.Clear();
        tvParseTree.Nodes.Add("برنامج جديد — اضغط «ترجمة (F6)» لبناء شجرة التحليل.");
        dgvSymbols.Rows.Clear();
        dgvErrors.Rows.Clear();
        txtTac.Clear();
        txtAssembly.Clear();
        lblStatus.Text = "تم إنشاء برنامج جديد.";
    }

    private void OpenFileDialog()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Bayan source files (*.bayan)|*.bayan|All files (*.*)|*.*",
            Title = "فتح ملف كود بيان"
        };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            LoadSourceFile(dialog.FileName);
        }
    }

    private void LoadSourceFile(string path)
    {
        try
        {
            sourceEditor.SourceCode = File.ReadAllText(path);
            currentSourcePath = path;
            lblStatus.Text = $"تم فتح الملف: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر قراءة الملف: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveFileDialog()
    {
        if (!string.IsNullOrEmpty(currentSourcePath))
        {
            File.WriteAllText(currentSourcePath, sourceEditor.SourceCode);
            lblStatus.Text = $"تم حفظ الملف: {Path.GetFileName(currentSourcePath)}";
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Bayan source files (*.bayan)|*.bayan|All files (*.*)|*.*",
            DefaultExt = "bayan",
            Title = "حفظ برنامج بيان"
        };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            File.WriteAllText(dialog.FileName, sourceEditor.SourceCode);
            currentSourcePath = dialog.FileName;
            lblStatus.Text = $"تم حفظ الملف: {Path.GetFileName(currentSourcePath)}";
        }
    }

    private void PopulateOfficialSamples()
    {
        cmbOfficialSamples.Items.Clear();
        cmbOfficialSamples.Items.Add("— اختر عينة رسمية للتجربة —");

        string samplesDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "samples");
        if (!Directory.Exists(samplesDir))
        {
            samplesDir = Path.Combine(Environment.CurrentDirectory, "samples");
        }

        if (Directory.Exists(samplesDir))
        {
            string[] sampleFiles = Directory.GetFiles(samplesDir, "*.bayan");
            Array.Sort(sampleFiles);
            foreach (string file in sampleFiles)
            {
                cmbOfficialSamples.Items.Add(Path.GetFileName(file));
            }
        }

        if (cmbOfficialSamples.Items.Count > 0)
        {
            cmbOfficialSamples.SelectedIndex = 0;
        }
    }

    private void CmbOfficialSamples_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (cmbOfficialSamples.SelectedIndex <= 0)
        {
            return;
        }

        string selectedFileName = cmbOfficialSamples.SelectedItem?.ToString() ?? "";
        if (string.IsNullOrEmpty(selectedFileName))
        {
            return;
        }

        string samplesDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "samples");
        if (!Directory.Exists(samplesDir))
        {
            samplesDir = Path.Combine(Environment.CurrentDirectory, "samples");
        }

        string fullPath = Path.Combine(samplesDir, selectedFileName);
        if (File.Exists(fullPath))
        {
            LoadSourceFile(fullPath);
        }
    }

    private void Form_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.N)
        {
            e.SuppressKeyPress = true;
            NewFile();
        }
        else if (e.Control && e.KeyCode == Keys.O)
        {
            e.SuppressKeyPress = true;
            OpenFileDialog();
        }
        else if (e.Control && e.KeyCode == Keys.S)
        {
            e.SuppressKeyPress = true;
            SaveFileDialog();
        }
        else if (e.KeyCode == Keys.F6)
        {
            e.SuppressKeyPress = true;
            CompileSource();
        }
        else if (e.KeyCode == Keys.F5)
        {
            e.SuppressKeyPress = true;
            RunExecutable();
        }
    }

    private static string GetInitialPerformanceText()
    {
        return "=== تقرير أداء وسير خط ترجمة بيان (Bayan Compiler Pipeline) ===\n\n" +
               "المراحل الـ 9 الرسمية:\n" +
               "  [01] محرر الكود المصدري (Source Editor)\n" +
               "  [02] الرموز المعجمية (Tokens)\n" +
               "  [03] شجرة التحليل النحوي (Doctor LL(1) Parse Tree)\n" +
               "  [04] جدول الرموز (Symbol Table)\n" +
               "  [05] نتائج التحليل الدلالي (Semantic Rules & Types)\n" +
               "  [06] الكود الوسيط (Three-Address Code / TAC)\n" +
               "  [07] كود لغة التجميع (x86 Assembly / output.asm)\n" +
               "  [08] قائمة الأخطاء والتشخيصات (Diagnostics)\n" +
               "  [09] التنفيذ الحقيقي (output.exe) في الطرفية التفاعلية\n\n" +
               "اضغط «ترجمة (F6)» لتشغيل مراحل المترجم واستخراج النتائج الحقيقية.";
    }

    /// <summary>
    /// Professional flat ToolStrip renderer matching the modern dark theme.
    /// </summary>
    private sealed class DarkToolStripRenderer : ToolStripProfessionalRenderer
    {
        public DarkToolStripRenderer() : base(new DarkColorTable()) { }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
    }

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => IdeTheme.SurfaceElevated;
        public override Color ImageMarginGradientBegin => IdeTheme.SurfaceElevated;
        public override Color ImageMarginGradientMiddle => IdeTheme.SurfaceElevated;
        public override Color ImageMarginGradientEnd => IdeTheme.SurfaceElevated;
        public override Color MenuBorder => IdeTheme.Border;
        public override Color MenuItemBorder => IdeTheme.Border;
        public override Color MenuItemSelected => IdeTheme.SurfaceHover;
        public override Color MenuStripGradientBegin => IdeTheme.SurfaceElevated;
        public override Color MenuStripGradientEnd => IdeTheme.SurfaceElevated;
        public override Color ToolStripBorder => IdeTheme.Border;
        public override Color ButtonSelectedHighlight => IdeTheme.SurfaceHover;
        public override Color ButtonPressedHighlight => IdeTheme.SurfaceActive;
        public override Color SeparatorDark => IdeTheme.Border;
        public override Color SeparatorLight => IdeTheme.Border;
    }
}
