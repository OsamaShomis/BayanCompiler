using System.Diagnostics;
using System.Text.RegularExpressions;

namespace BayanCompiler.Forms;

/// <summary>
/// Modern, rock-solid IDE surface for the Bayan programming language compiler.
/// Implements the Golden Tri-Zone layout (Editor + Inspector + Full-width Terminal),
/// completely eliminating visual overlap while providing instantaneous access to all 9 compiler stages.
/// </summary>
public sealed class CompilerJourneyForm : Form
{
    // Compiler and runtime execution clients
    private readonly CliCompilationClient compilerClient = new();
    private readonly WindowsExecutableRuntimeClient executableRuntimeClient = new();

    // Core workspace components
    private readonly BayanCodeEditor sourceEditor = new();
    private readonly BayanTerminalControl terminalControl = new();

    // Layout containers
    private readonly SplitContainer mainSplitter = new();   // Top (Workspace) vs Bottom (Terminal & Errors)
    private readonly SplitContainer upperSplitter = new();  // Editor (60%) vs Inspector Tabs (40%)
    private readonly TabControl inspectorTabs = new();      // Tokens, AST, Symbols, TAC, Assembly, Semantics
    private readonly TabControl bottomTabs = new();         // Terminal, Errors/Diagnostics, Pipeline Report

    // Inspector tab content viewers (Dedicated per tab: zero overlap guaranteed)
    private readonly RichTextBox txtTokens = CreateMonospaceViewer();
    private readonly RichTextBox txtParseTree = CreateMonospaceViewer();
    private readonly RichTextBox txtSymbolTable = CreateMonospaceViewer();
    private readonly RichTextBox txtTac = CreateMonospaceViewer();
    private readonly RichTextBox txtAssembly = CreateMonospaceViewer();
    private readonly RichTextBox txtSemantics = CreateMonospaceViewer();

    // Bottom tab content viewers
    private readonly RichTextBox txtDiagnostics = CreateMonospaceViewer();
    private readonly RichTextBox txtPipelineReport = CreateMonospaceViewer();

    // Top Command Bar controls
    private readonly Panel commandBar = new();
    private readonly Button btnNew = new();
    private readonly Button btnOpen = new();
    private readonly Button btnSave = new();
    private readonly Button btnCompile = new();
    private readonly Button btnRunExe = new();
    private readonly Button btnToggleSplit = new();
    private readonly Button btnToggleTheme = new();
    private readonly ComboBox cmbOfficialSamples = new();
    private readonly ProgressBar compileProgress = new();
    private readonly ToolTip toolTips = new();

    // Footer status bar controls
    private readonly Panel footerPanel = new();
    private readonly Label statusMessageLabel = new();
    private readonly Label currentTargetLabel = new();
    private readonly Label runtimeInfoLabel = new();

    // State tracking
    private bool isBusy = false;
    private bool isSplitView = true;
    private string lastExecutablePath = string.Empty;
    private string currentSourcePath = string.Empty;

    public CompilerJourneyForm(string? initialSourcePath = null)
    {
        Text = "بيان  |  Bayan IDE — بيئة التطوير ومترجم لغة بيان الرسمية";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1000, 680);
        Size = new Size(1280, 840);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = false; // Never flip coordinate engine to avoid WinForms RTL glitches
        Font = IdeTheme.UiFontRegular;
        DoubleBuffered = true;
        KeyPreview = true;

        InitializeDefaultArtifacts();
        BuildLayout();
        PopulateOfficialSamples();
        ApplyActiveTheme();

        // Keyboard shortcuts
        KeyDown += Form_KeyDown;

        // Diagnostic click-to-navigate in code
        txtDiagnostics.DoubleClick += TxtDiagnostics_DoubleClick;

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

        // Set optimal proportional split distances after form is shown and layout dimensions are known
        Shown += (_, _) => ApplyProportionalSplitters();
        Resize += (_, _) =>
        {
            if (WindowState != FormWindowState.Minimized)
            {
                ApplyProportionalSplitters();
            }
        };
    }

    private static RichTextBox CreateMonospaceViewer()
    {
        return new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            Font = IdeTheme.ViewerCodeFont,
            ScrollBars = RichTextBoxScrollBars.Both,
            WordWrap = false,
            RightToLeft = RightToLeft.No,
            DetectUrls = false
        };
    }

    private void InitializeDefaultArtifacts()
    {
        txtTokens.Text = "لم تُشغَّل الترجمة بعد. اضغط «ترجمة ⚙ (F6)» لاستخراج الرموز المعجمية (Tokens).";
        txtParseTree.Text = "ستظهر هنا شجرة التحليل النحوي الرسمية (Parse Tree) الناتجة عن LL(1) Parser.";
        txtSymbolTable.Text = "سيظهر هنا جدول الرموز الدلالي (Symbol Table) للمتغيرات والأنواع والنطاقات.";
        txtTac.Text = "سيظهر هنا كود Three-Address Code (TAC) الوسيط للبرنامج.";
        txtAssembly.Text = "سيظهر هنا كود لغة التجميع x86 المستهدف (output.asm) المولد.";
        txtSemantics.Text = "نتائج الفحص الدلالي وقواعد الأنواع ستظهر هنا بعد الترجمة.";
        txtDiagnostics.Text = "لا توجد أخطاء حالياً. عند وجود أخطاء نحوية أو دلالية، ستُعرض هنا مع أرقام الأسطر.\n(يمكنك النقر المزدوج على أي سطر خطأ للانتقال مباشرة إليه في المحرر).";
        txtPipelineReport.Text = GetInitialReportText();
    }

    private static string GetInitialReportText()
    {
        return "=== تقرير خط سير مراحل مترجم بيان (Compiler Pipeline) ===\n\n" +
               "المراحل الرسمية المعتمدة في المشروع:\n" +
               "  [01] محرر الكود المصدري بلغة بيان (Source Editor)\n" +
               "  [02] الرموز المعجمية (Tokens)\n" +
               "  [03] شجرة التحليل النحوي (Doctor LL(1) Parse Tree)\n" +
               "  [04] جدول الرموز الدلالي (Symbol Table)\n" +
               "  [05] نتائج التحليل الدلالي (Semantic Rules & Types)\n" +
               "  [06] الكود الوسيط (Three-Address Code / TAC)\n" +
               "  [07] كود لغة التجميع (x86 Assembly / output.asm)\n" +
               "  [08] قائمة الأخطاء والتشخيصات (Diagnostics)\n" +
               "  [09] التنفيذ الحقيقي (output.exe) في الطرفية التفاعلية\n\n" +
               "اضغط «ترجمة ⚙ (F6)» لبدء معالجة الكود.";
    }

    private void BuildLayout()
    {
        Controls.Clear();

        var topBar = CreateCommandBar();
        var footer = CreateFooter();
        var workspace = CreateWorkspace();

        Controls.Add(workspace);
        Controls.Add(footer);
        Controls.Add(topBar);
    }

    private Control CreateCommandBar()
    {
        commandBar.Dock = DockStyle.Top;
        commandBar.Height = 46;
        commandBar.Padding = new Padding(10, 6, 10, 6);
        commandBar.BackColor = IdeTheme.SurfaceElevated;

        // Branding Logo and Title
        var titleBadge = new Label
        {
            Text = "بيان  |  Bayan IDE",
            Font = IdeTheme.HeaderFont,
            AutoSize = true,
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(4, 2, 12, 2),
            ForeColor = IdeTheme.AccentCyan
        };

        // Actions container using FlowLayoutPanel
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoScroll = false,
            Padding = new Padding(0, 0, 0, 0),
            Margin = new Padding(0)
        };

        // File Operations
        ConfigureToolButton(btnNew, "جديد", "إنشاء ملف جديد (Ctrl+N)", (_, _) => NewFile());
        ConfigureToolButton(btnOpen, "فتح", "فتح ملف .bayan محفوظ (Ctrl+O)", (_, _) => OpenFileDialog());
        ConfigureToolButton(btnSave, "حفظ", "حفظ الكود الحالي (Ctrl+S)", (_, _) => SaveFileDialog());

        // Compiler & Runner Operations (Highlighted Accents)
        ConfigureToolButton(btnCompile, "ترجمة ⚙ (F6)", "تشغيل مراحل المترجم كاملة وإنتاج TAC وx86 Assembly وEXE", (_, _) => CompileSource(), true, IdeTheme.AccentPrimary);
        ConfigureToolButton(btnRunExe, "تشغيل ▶ (F5)", "تشغيل output.exe فوراً في الطرفية التفاعلية", (_, _) => RunExecutable(), true, IdeTheme.AccentSuccess);
        btnRunExe.Enabled = false;

        // Samples Dropdown
        cmbOfficialSamples.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbOfficialSamples.Width = 190;
        cmbOfficialSamples.Height = 32;
        cmbOfficialSamples.Font = IdeTheme.UiFontRegular;
        cmbOfficialSamples.Margin = new Padding(6, 3, 6, 3);
        toolTips.SetToolTip(cmbOfficialSamples, "اختر من العينات الرسمية المعتمدة للاختبار الفوري");
        cmbOfficialSamples.SelectedIndexChanged += CmbOfficialSamples_SelectedIndexChanged;

        // View and Theme Toggles
        ConfigureToolButton(btnToggleSplit, "فاحص المترجم ◫", "إظهار أو طي لوحة فاحص مراحل المترجم الجانبية", (_, _) => ToggleSplitView());
        ConfigureToolButton(btnToggleTheme, "السمة 🌙", "التبديل بين الوضع الداكن والوضع الفاتح", (_, _) => ToggleTheme());

        // Progress bar
        compileProgress.Width = 90;
        compileProgress.Height = 16;
        compileProgress.Style = ProgressBarStyle.Marquee;
        compileProgress.MarqueeAnimationSpeed = 30;
        compileProgress.Visible = false;
        compileProgress.Margin = new Padding(8, 8, 8, 0);

        flow.Controls.Add(btnNew);
        flow.Controls.Add(btnOpen);
        flow.Controls.Add(btnSave);
        flow.Controls.Add(CreateToolbarSeparator());
        flow.Controls.Add(btnCompile);
        flow.Controls.Add(btnRunExe);
        flow.Controls.Add(compileProgress);
        flow.Controls.Add(CreateToolbarSeparator());
        flow.Controls.Add(cmbOfficialSamples);
        flow.Controls.Add(CreateToolbarSeparator());
        flow.Controls.Add(btnToggleSplit);
        flow.Controls.Add(btnToggleTheme);

        commandBar.Controls.Add(flow);
        commandBar.Controls.Add(titleBadge);
        return commandBar;
    }

    private static Control CreateToolbarSeparator()
    {
        return new Label
        {
            Width = 1,
            Height = 26,
            BackColor = IdeTheme.Border,
            Margin = new Padding(6, 4, 6, 4)
        };
    }

    private Control CreateWorkspace()
    {
        // 1. Main Splitter: Top (Workspace) vs Bottom (Terminal & Errors)
        mainSplitter.Dock = DockStyle.Fill;
        mainSplitter.Orientation = Orientation.Horizontal;
        mainSplitter.SplitterWidth = 6;
        mainSplitter.BackColor = IdeTheme.Border;
        mainSplitter.Panel1MinSize = 200;
        mainSplitter.Panel2MinSize = 140;

        // 2. Upper Splitter: Editor (60%) vs Inspector Tabs (40%)
        upperSplitter.Dock = DockStyle.Fill;
        upperSplitter.Orientation = Orientation.Vertical;
        upperSplitter.RightToLeft = RightToLeft.No; // Stable LTR docking for internal split
        upperSplitter.SplitterWidth = 6;
        upperSplitter.BackColor = IdeTheme.Border;
        upperSplitter.Panel1MinSize = 300;
        upperSplitter.Panel2MinSize = 250;

        // Place Editor in Panel 1 (Left), Inspector Tabs in Panel 2 (Right)
        sourceEditor.Dock = DockStyle.Fill;
        upperSplitter.Panel1.Controls.Add(sourceEditor);

        BuildInspectorTabs();
        upperSplitter.Panel2.Controls.Add(inspectorTabs);

        mainSplitter.Panel1.Controls.Add(upperSplitter);

        // 3. Build Bottom Tabs (Terminal across full width + Errors + Pipeline report)
        BuildBottomTabs();
        mainSplitter.Panel2.Controls.Add(bottomTabs);

        return mainSplitter;
    }

    private void BuildInspectorTabs()
    {
        inspectorTabs.Dock = DockStyle.Fill;
        inspectorTabs.Font = IdeTheme.SubHeaderFont;
        inspectorTabs.RightToLeft = RightToLeft.Yes;
        inspectorTabs.ItemSize = new Size(110, 30);
        inspectorTabs.SizeMode = TabSizeMode.Normal;

        inspectorTabs.TabPages.Clear();
        inspectorTabs.TabPages.Add(CreateTabPage("🔤 الرموز (Tokens)", txtTokens));
        inspectorTabs.TabPages.Add(CreateTabPage("🌳 شجرة الإعراب (Tree)", txtParseTree));
        inspectorTabs.TabPages.Add(CreateTabPage("📋 جدول الرموز (Symbols)", txtSymbolTable));
        inspectorTabs.TabPages.Add(CreateTabPage("⚡ TAC / IR", txtTac));
        inspectorTabs.TabPages.Add(CreateTabPage("⚙ Assembly x86", txtAssembly));
        inspectorTabs.TabPages.Add(CreateTabPage("🔬 التحليل الدلالي", txtSemantics));
    }

    private void BuildBottomTabs()
    {
        bottomTabs.Dock = DockStyle.Fill;
        bottomTabs.Font = IdeTheme.SubHeaderFont;
        bottomTabs.RightToLeft = RightToLeft.Yes;
        bottomTabs.ItemSize = new Size(150, 30);
        bottomTabs.SizeMode = TabSizeMode.Normal;

        terminalControl.Dock = DockStyle.Fill;
        terminalControl.RerunRequested += (_, _) => RunExecutable();

        bottomTabs.TabPages.Clear();
        bottomTabs.TabPages.Add(CreateTabPage("🖥️ طرفية التشغيل (Terminal)", terminalControl));
        bottomTabs.TabPages.Add(CreateTabPage("⚠️ الأخطاء والتشخيص (Diagnostics)", txtDiagnostics));
        bottomTabs.TabPages.Add(CreateTabPage("📊 تقرير سير المترجم (Report)", txtPipelineReport));
    }

    private static TabPage CreateTabPage(string title, Control innerControl)
    {
        var page = new TabPage(title)
        {
            Padding = new Padding(4),
            BackColor = IdeTheme.Surface,
            UseVisualStyleBackColor = false
        };
        innerControl.Dock = DockStyle.Fill;
        page.Controls.Add(innerControl);
        return page;
    }

    private Control CreateFooter()
    {
        footerPanel.Dock = DockStyle.Bottom;
        footerPanel.Height = 32;
        footerPanel.Padding = new Padding(12, 4, 12, 4);
        footerPanel.BackColor = IdeTheme.SurfaceElevated;

        var footerTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.Transparent,
            RightToLeft = RightToLeft.Yes
        };
        footerTable.ColumnStyles.Clear();
        footerTable.RowStyles.Clear();
        footerTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
        footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

        statusMessageLabel.Text = "جاهز — اكتب الكود المصدري ثم اضغط «ترجمة ⚙ (F6)» أو «تشغيل ▶ (F5)»";
        statusMessageLabel.Dock = DockStyle.Fill;
        statusMessageLabel.Font = IdeTheme.UiFontRegular;
        statusMessageLabel.TextAlign = ContentAlignment.MiddleRight;

        currentTargetLabel.Text = "الهدف: x86 Assembly + ilasm (output.exe)";
        currentTargetLabel.Dock = DockStyle.Fill;
        currentTargetLabel.Font = IdeTheme.UiFontBold;
        currentTargetLabel.TextAlign = ContentAlignment.MiddleCenter;
        currentTargetLabel.ForeColor = IdeTheme.AccentCyan;

        runtimeInfoLabel.Text = "لغة بيان — .NET 9";
        runtimeInfoLabel.Dock = DockStyle.Fill;
        runtimeInfoLabel.Font = IdeTheme.BadgeFont;
        runtimeInfoLabel.TextAlign = ContentAlignment.MiddleLeft;
        runtimeInfoLabel.ForeColor = IdeTheme.TextSecondary;

        footerTable.Controls.Add(statusMessageLabel, 0, 0);
        footerTable.Controls.Add(currentTargetLabel, 1, 0);
        footerTable.Controls.Add(runtimeInfoLabel, 2, 0);
        footerPanel.Controls.Add(footerTable);
        return footerPanel;
    }

    private void ApplyProportionalSplitters()
    {
        try
        {
            if (mainSplitter.Height > 350)
            {
                int targetHeight = (int)(mainSplitter.Height * 0.65);
                if (targetHeight >= mainSplitter.Panel1MinSize &&
                    (mainSplitter.Height - targetHeight) >= mainSplitter.Panel2MinSize)
                {
                    mainSplitter.SplitterDistance = targetHeight;
                }
            }

            if (upperSplitter.Width > 400 && isSplitView)
            {
                int targetWidth = (int)(upperSplitter.Width * 0.58);
                if (targetWidth >= upperSplitter.Panel1MinSize &&
                    (upperSplitter.Width - targetWidth) >= upperSplitter.Panel2MinSize)
                {
                    upperSplitter.SplitterDistance = targetWidth;
                }
            }
        }
        catch
        {
            // SplitContainer calculation guard on sudden resizing
        }
    }

    private void ConfigureToolButton(Button btn, string text, string tooltip, EventHandler onClick, bool isPrimary = false, Color? primaryColor = null)
    {
        btn.Text = text;
        btn.AutoSize = true;
        btn.MinimumSize = new Size(64, 32);
        btn.Height = 32;
        btn.FlatStyle = FlatStyle.Flat;
        btn.Font = isPrimary ? IdeTheme.UiFontBold : IdeTheme.UiFontRegular;
        btn.Cursor = Cursors.Hand;
        btn.Margin = new Padding(3, 2, 3, 2);
        btn.Padding = new Padding(8, 2, 8, 2);
        toolTips.SetToolTip(btn, tooltip);
        btn.Click += onClick;

        if (isPrimary && primaryColor.HasValue)
        {
            btn.BackColor = primaryColor.Value;
            btn.ForeColor = Color.White;
            btn.FlatAppearance.BorderSize = 0;
        }
        else
        {
            btn.BackColor = IdeTheme.SurfaceElevated;
            btn.ForeColor = IdeTheme.TextPrimary;
            btn.FlatAppearance.BorderColor = IdeTheme.Border;
            btn.FlatAppearance.BorderSize = 1;
        }
    }

    private async void CompileSource()
    {
        if (isBusy)
        {
            return;
        }

        SetBusy(true, "يجري تشغيل مترجم CLI المستقل والتحقق من القواعد...");
        try
        {
            CliCompilationRun run = await compilerClient.CompileAsync(sourceEditor.SourceCode);

            txtTokens.Text = run.GetArtifact("tokens.txt");
            txtParseTree.Text = run.GetArtifact("parse-tree.txt");
            txtSymbolTable.Text = run.GetArtifact("symbol-table.txt");

            string diagnostics = run.GetArtifact("diagnostics.txt");
            txtSemantics.Text = string.IsNullOrWhiteSpace(diagnostics) ? "تم التحقق الدلالي وفحص الأنواع بنجاح بدون أخطاء." : diagnostics;
            txtTac.Text = run.GetArtifact("three-address-code.txt");
            txtAssembly.Text = run.GetArtifact("output.asm");
            txtDiagnostics.Text = string.IsNullOrWhiteSpace(diagnostics) ? "لا توجد أي أخطاء نحوية أو دلالية. الكود سليم بنسبة 100%." : diagnostics;

            lastExecutablePath = run.Manifest?.WindowsExecutableAvailable == true
                ? run.Manifest.WindowsExecutablePath
                : string.Empty;

            bool succeeded = run.Manifest?.Success == true && run.ExitCode == 0;
            btnRunExe.Enabled = succeeded && !string.IsNullOrWhiteSpace(lastExecutablePath) && File.Exists(lastExecutablePath);

            // Update Pipeline report
            txtPipelineReport.Text =
                $"=== نتيجة معالجة مترجم بيان ===\n\n" +
                $"• الحالة: {(succeeded ? "نجاح الترجمة الكاملة ✔" : "توقفت الترجمة لوجود أخطاء ✖")}\n" +
                $"• آخر مرحلة مكتملة: {run.Manifest?.CompletedStage ?? "غير محدد"}\n" +
                $"• توفر ملف التنفيذ (output.exe): {(btnRunExe.Enabled ? "نعم — جاهز للتشغيل في الطرفية" : "لا")}\n" +
                $"• مسار الملف التنفيذي: {lastExecutablePath}\n" +
                $"• رمز الخروج للـ CLI: {run.ExitCode}\n\n" +
                $"التفاصيل:\n{run.StandardOutput}\n{run.StandardError}";

            if (succeeded)
            {
                statusMessageLabel.Text = $"اكتملت الترجمة بنجاح! تم بناء output.exe وoutput.asm في: {run.Manifest?.CompletedStage}";
                statusMessageLabel.ForeColor = IdeTheme.AccentSuccess;

                // Select Assembly tab in inspector to let the user admire the generated x86 code
                inspectorTabs.SelectedIndex = 4;
            }
            else
            {
                statusMessageLabel.Text = $"توقفت الترجمة عند مرحلة: {run.Manifest?.CompletedStage ?? "الأخطاء"}";
                statusMessageLabel.ForeColor = IdeTheme.AccentError;

                // Automatically bring bottom Diagnostics tab into view
                bottomTabs.SelectedIndex = 1;
            }
        }
        catch (Exception ex)
        {
            txtDiagnostics.Text = "[خطأ في بيئة تشغيل المحرر] " + ex.Message;
            statusMessageLabel.Text = "فشل تشغيل المترجم: " + ex.Message;
            statusMessageLabel.ForeColor = IdeTheme.AccentError;
            bottomTabs.SelectedIndex = 1;
        }
        finally
        {
            SetBusy(false);
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
            MessageBox.Show(this, "يرجى تشغيل «ترجمة ⚙ (F6)» أولاً لبناء ملف output.exe.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Switch to terminal tab in bottom panel
        bottomTabs.SelectedIndex = 0;
        terminalControl.SetWaiting();
        SetBusy(true, "يجري تشغيل output.exe...");

        var stopwatch = Stopwatch.StartNew();
        try
        {
            string input = terminalControl.GetProvidedInput();
            WindowsExecutableRun run = await executableRuntimeClient.ExecuteAsync(lastExecutablePath, input);
            stopwatch.Stop();

            terminalControl.SetResult(run.StandardOutput, run.StandardError, run.ExitCode, stopwatch.Elapsed);
            statusMessageLabel.Text = run.ExitCode == 0 ? "اكتمل تشغيل البرنامج بنجاح (كود 0)." : $"انتهى البرنامج برمز خروج ({run.ExitCode}).";
            statusMessageLabel.ForeColor = run.ExitCode == 0 ? IdeTheme.AccentSuccess : IdeTheme.AccentError;
        }
        catch (TimeoutException)
        {
            stopwatch.Stop();
            terminalControl.SetResult("", "[تجاوز وقت التنفيذ] استغرق البرنامج وقتاً أطول من المسموح.", -1, stopwatch.Elapsed);
            statusMessageLabel.Text = "تجاوز البرنامج الحد الزمني المسموح.";
            statusMessageLabel.ForeColor = IdeTheme.AccentError;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            terminalControl.SetResult("", "[خطأ تشغيل] " + ex.Message, -1, stopwatch.Elapsed);
            statusMessageLabel.Text = "تعذر تشغيل الملف التنفيذي.";
            statusMessageLabel.ForeColor = IdeTheme.AccentError;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void TxtDiagnostics_DoubleClick(object? sender, EventArgs e)
    {
        // Smart jump-to-line feature: extracts line number from diagnostic message
        string currentLine = txtDiagnostics.SelectedText;
        if (string.IsNullOrWhiteSpace(currentLine))
        {
            int lineIndex = txtDiagnostics.GetLineFromCharIndex(txtDiagnostics.SelectionStart);
            if (lineIndex >= 0 && lineIndex < txtDiagnostics.Lines.Length)
            {
                currentLine = txtDiagnostics.Lines[lineIndex];
            }
        }

        if (string.IsNullOrWhiteSpace(currentLine))
        {
            return;
        }

        var match = Regex.Match(currentLine, @"(?:السطر|Line)\s*:?\s*(\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int targetLine))
        {
            JumpToEditorLine(targetLine);
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
            statusMessageLabel.Text = $"تم الانتقال إلى السطر {targetLine}";
        }
    }

    private void ToggleSplitView()
    {
        isSplitView = !isSplitView;
        btnToggleSplit.Text = isSplitView ? "فاحص المترجم ◫" : "محرر كامل 🗖";
        upperSplitter.Panel2Collapsed = !isSplitView;

        if (isSplitView)
        {
            ApplyProportionalSplitters();
        }
    }

    private void ToggleTheme()
    {
        IdeTheme.IsDarkMode = !IdeTheme.IsDarkMode;
        btnToggleTheme.Text = IdeTheme.IsDarkMode ? "السمة 🌙" : "السمة ☀️";
        ApplyActiveTheme();
    }

    private void ApplyActiveTheme()
    {
        BackColor = IdeTheme.AppBackground;
        ForeColor = IdeTheme.TextPrimary;

        commandBar.BackColor = IdeTheme.SurfaceElevated;
        footerPanel.BackColor = IdeTheme.SurfaceElevated;

        sourceEditor.ApplyTheme();
        terminalControl.ApplyTheme();

        // Style viewers
        Color viewBg = IdeTheme.Surface;
        Color viewFg = IdeTheme.TextPrimary;

        txtTokens.BackColor = viewBg;
        txtTokens.ForeColor = viewFg;

        txtParseTree.BackColor = viewBg;
        txtParseTree.ForeColor = viewFg;

        txtSymbolTable.BackColor = viewBg;
        txtSymbolTable.ForeColor = viewFg;

        txtTac.BackColor = viewBg;
        txtTac.ForeColor = viewFg;

        txtAssembly.BackColor = viewBg;
        txtAssembly.ForeColor = IdeTheme.IsDarkMode ? Color.FromArgb(147, 197, 253) : Color.FromArgb(30, 64, 175);

        txtSemantics.BackColor = viewBg;
        txtSemantics.ForeColor = viewFg;

        txtDiagnostics.BackColor = viewBg;
        txtDiagnostics.ForeColor = viewFg;

        txtPipelineReport.BackColor = viewBg;
        txtPipelineReport.ForeColor = viewFg;

        // Tab backgrounds
        foreach (TabPage page in inspectorTabs.TabPages)
        {
            page.BackColor = IdeTheme.Surface;
        }

        foreach (TabPage page in bottomTabs.TabPages)
        {
            page.BackColor = IdeTheme.Surface;
        }

        // Toolbar buttons styling
        btnNew.BackColor = IdeTheme.SurfaceElevated;
        btnNew.ForeColor = IdeTheme.TextPrimary;
        btnNew.FlatAppearance.BorderColor = IdeTheme.Border;

        btnOpen.BackColor = IdeTheme.SurfaceElevated;
        btnOpen.ForeColor = IdeTheme.TextPrimary;
        btnOpen.FlatAppearance.BorderColor = IdeTheme.Border;

        btnSave.BackColor = IdeTheme.SurfaceElevated;
        btnSave.ForeColor = IdeTheme.TextPrimary;
        btnSave.FlatAppearance.BorderColor = IdeTheme.Border;

        btnToggleSplit.BackColor = IdeTheme.SurfaceElevated;
        btnToggleSplit.ForeColor = IdeTheme.TextPrimary;
        btnToggleSplit.FlatAppearance.BorderColor = IdeTheme.Border;

        btnToggleTheme.BackColor = IdeTheme.SurfaceElevated;
        btnToggleTheme.ForeColor = IdeTheme.TextPrimary;
        btnToggleTheme.FlatAppearance.BorderColor = IdeTheme.Border;

        cmbOfficialSamples.BackColor = IdeTheme.SurfaceElevated;
        cmbOfficialSamples.ForeColor = IdeTheme.TextPrimary;

        statusMessageLabel.ForeColor = IdeTheme.TextSecondary;
        currentTargetLabel.ForeColor = IdeTheme.AccentCyan;
        runtimeInfoLabel.ForeColor = IdeTheme.TextMuted;
    }

    private void SetBusy(bool busy, string message = "")
    {
        isBusy = busy;
        compileProgress.Visible = busy;
        UseWaitCursor = busy;

        btnCompile.Enabled = !busy;
        btnRunExe.Enabled = !busy && !string.IsNullOrWhiteSpace(lastExecutablePath) && File.Exists(lastExecutablePath);

        if (!string.IsNullOrEmpty(message))
        {
            statusMessageLabel.Text = message;
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
        InitializeDefaultArtifacts();
        statusMessageLabel.Text = "برنامج جديد جاهز للتحرير والتنفيذ.";
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
            statusMessageLabel.Text = $"تم فتح الملف: {Path.GetFileName(path)}";
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
            statusMessageLabel.Text = $"تم حفظ الملف: {Path.GetFileName(currentSourcePath)}";
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
            statusMessageLabel.Text = $"تم حفظ الملف: {Path.GetFileName(currentSourcePath)}";
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
}
