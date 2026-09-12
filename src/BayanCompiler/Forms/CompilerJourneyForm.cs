using System.Diagnostics;

namespace BayanCompiler.Forms;

/// <summary>
/// Professional, modern IDE surface for the Bayan programming language compiler.
/// Provides direct 9-stage navigation, dual-pane split view, syntax highlighting,
/// and integrated execution terminal for output.exe.
/// </summary>
public sealed class CompilerJourneyForm : Form
{
    // The exact 9 stages required by the official specifications in strict order
    private static readonly string[] StageNames =
    {
        "01 المصدر",
        "02 Tokens",
        "03 شجرة التحليل",
        "04 جدول الرموز",
        "05 نتائج الدلالة",
        "06 TAC / IR",
        "07 Assembly Code",
        "08 الأخطاء",
        "09 نتيجة التنفيذ"
    };

    private static readonly string[] StageSubtitles =
    {
        "محرر الكود المصدري بلغة بيان مع تلوين نحوي وترقيم الأسطر",
        "الرموز المعجمية المستخرجة بواسطة Lexer المستقل",
        "شجرة التحليل النحوي الرسمية الناتجة عن DoctorLl1Parser",
        "معرفات وأنواع وثوابت وإجراءات جدول الرموز",
        "نتائج الفحص الدلالي وقواعد الأنواع",
        "Three-Address Code (TAC) الوسيط",
        "كود الأسمبلي x86 التوثيقي (output.asm)",
        "التشخيصات والأخطاء النحوية والدلالية (Diagnostics)",
        "البرنامج التنفيذي الحقيقي (output.exe) عبر ilasm"
    };

    // Compiler and runtime execution clients
    private readonly CliCompilationClient compilerClient = new();
    private readonly WindowsExecutableRuntimeClient executableRuntimeClient = new();

    // Editor and viewer controls
    private readonly BayanCodeEditor sourceEditor = new();
    private readonly RichTextBox artifactViewer = new();
    private readonly BayanTerminalControl terminalControl = new();

    // Navigation and layout
    private readonly SplitContainer workspaceSplitter = new();
    private readonly Panel stageViewerHost = new();
    private readonly Label stageTitleLabel = new();
    private readonly Label stageSubtitleLabel = new();
    private readonly Button[] stageButtons = new Button[StageNames.Length];
    private readonly string[] stageArtifacts = new string[StageNames.Length];

    // Toolbar buttons
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

    // Status bar
    private readonly Label statusMessageLabel = new();
    private readonly Label currentTargetLabel = new();

    // State tracking
    private int activeStage = 0;
    private bool isSplitView = true;
    private bool isBusy = false;
    private string lastExecutablePath = string.Empty;
    private string currentSourcePath = string.Empty;

    public CompilerJourneyForm(string? initialSourcePath = null)
    {
        Text = "بيان  |  Bayan IDE — بيئة التطوير والمترجم الرسمي";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1080, 720);
        Size = new Size(1280, 840);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = false; // Never set to true in WinForms: prevents GDI coordinate mirroring bugs
        Font = IdeTheme.UiFontRegular;
        DoubleBuffered = true;
        KeyPreview = true;

        InitializeDefaultArtifacts();
        BuildLayout();
        PopulateOfficialSamples();
        ApplyActiveTheme();

        // Keyboard shortcuts
        KeyDown += Form_KeyDown;

        if (!string.IsNullOrWhiteSpace(initialSourcePath) && File.Exists(initialSourcePath))
        {
            LoadSourceFile(initialSourcePath);
        }
        else
        {
            sourceEditor.SourceCode = "برنامج تجربة;\nمتغير س : صحيح;\n{\n    س = 15;\n    اطبع(س);\n}.";
        }

        Shown += (_, _) =>
        {
            try
            {
                if (workspaceSplitter.Width > 200)
                {
                    workspaceSplitter.SplitterDistance = workspaceSplitter.Width / 2;
                }
            }
            catch { }
        };

        Resize += (_, _) =>
        {
            try
            {
                if (isSplitView && WindowState != FormWindowState.Minimized && workspaceSplitter.Width > 200)
                {
                    workspaceSplitter.SplitterDistance = workspaceSplitter.Width / 2;
                }
            }
            catch { }
        };

        SelectStage(0);
    }

    private void InitializeDefaultArtifacts()
    {
        stageArtifacts[1] = "لم تُشغَّل الترجمة بعد. اضغط «ترجمة ⚙ (F6)» لاستخراج Tokens.";
        stageArtifacts[2] = "ستظهر هنا شجرة التحليل النحوي (Parse Tree) بعد الترجمة.";
        stageArtifacts[3] = "سيظهر جدول الرموز (Symbol Table) الدلالي بعد الترجمة.";
        stageArtifacts[4] = "ستظهر نتائج التحليل الدلالي (Semantic Analysis) هنا.";
        stageArtifacts[5] = "سيظهر كود Three-Address Code (TAC) هنا.";
        stageArtifacts[6] = "سيظهر كود أسمبلي x86 التوثيقي (output.asm) هنا.";
        stageArtifacts[7] = "لا توجد أخطاء بعد. ستظهر تشخيصات CLI هنا في حال وجود أي خطأ نحوي أو دلالي.";
        stageArtifacts[8] = "لم يُنفّذ البرنامج بعد. اضغط «تشغيل ▶ (F5)» لتشغيل output.exe.";
    }

    private void BuildLayout()
    {
        var toolbar = CreateToolbar();     // Dock = DockStyle.Top, Height = 60
        var stageBar = CreateStageBar();   // Dock = DockStyle.Top, Height = 52
        var footer = CreateFooter();       // Dock = DockStyle.Bottom, Height = 36
        var workspace = CreateWorkspace(); // Dock = DockStyle.Fill

        // In Windows Forms docking:
        // Fill control is added first.
        // Then Bottom controls.
        // Then Top controls in reverse order (bottom-most Top first, then top-most Top).
        Controls.Add(workspace);
        Controls.Add(footer);
        Controls.Add(stageBar);
        Controls.Add(toolbar);
    }

    private Control CreateToolbar()
    {
        var toolbarPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            Padding = new Padding(12, 10, 12, 10),
            BackColor = IdeTheme.AppBackground
        };

        // Logo and Title on Right (start of RTL)
        var titleLabel = new Label
        {
            Text = "بيان  |  Bayan IDE",
            Font = IdeTheme.HeaderFont,
            AutoSize = true,
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(6, 4, 16, 4),
            ForeColor = IdeTheme.TextPrimary
        };

        // Toolbar Buttons Container
        var actionFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoScroll = false,
            Padding = new Padding(0, 2, 0, 2)
        };

        // File Buttons
        ConfigureToolButton(btnNew, "جديد", "إنشاء ملف جديد (Ctrl+N)", (_, _) => NewFile());
        ConfigureToolButton(btnOpen, "فتح", "فتح ملف .bayan محفوظ (Ctrl+O)", (_, _) => OpenFileDialog());
        ConfigureToolButton(btnSave, "حفظ", "حفظ الكود الحالي (Ctrl+S)", (_, _) => SaveFileDialog());

        // Compiler & Execution Buttons (Prominent)
        ConfigureToolButton(btnCompile, "ترجمة ⚙ (F6)", "تشغيل مترجم CLI حتى TAC وx86 Assembly وEXE (F6)", (_, _) => CompileSource(), true, IdeTheme.Dark.AccentPrimary);
        ConfigureToolButton(btnRunExe, "تشغيل ▶ (F5)", "تشغيل output.exe المترجم عبر ilasm (F5)", (_, _) => RunExecutable(), true, IdeTheme.Dark.AccentSuccess);
        btnRunExe.Enabled = false;

        // Split View Toggle
        ConfigureToolButton(btnToggleSplit, "عرض منقسم ◫", "التبديل بين العرض المزدوج وعرض التبويب الكامل", (_, _) => ToggleSplitView());

        // Theme Toggle
        ConfigureToolButton(btnToggleTheme, "السمة 🌙", "التبديل بين الوضع الداكن والوضع الفاتح", (_, _) => ToggleTheme());

        // Samples Dropdown
        cmbOfficialSamples.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbOfficialSamples.Width = 175;
        cmbOfficialSamples.Height = 32;
        cmbOfficialSamples.Font = IdeTheme.UiFontRegular;
        cmbOfficialSamples.Margin = new Padding(6, 4, 6, 4);
        toolTips.SetToolTip(cmbOfficialSamples, "اختر من العينات الرسمية للتجربة السريعة");
        cmbOfficialSamples.SelectedIndexChanged += CmbOfficialSamples_SelectedIndexChanged;

        // Compile Progress Bar
        compileProgress.Width = 100;
        compileProgress.Height = 18;
        compileProgress.Style = ProgressBarStyle.Marquee;
        compileProgress.MarqueeAnimationSpeed = 30;
        compileProgress.Visible = false;
        compileProgress.Margin = new Padding(8, 10, 8, 0);

        actionFlow.Controls.Add(btnNew);
        actionFlow.Controls.Add(btnOpen);
        actionFlow.Controls.Add(btnSave);
        actionFlow.Controls.Add(CreateToolbarSeparator());
        actionFlow.Controls.Add(btnCompile);
        actionFlow.Controls.Add(btnRunExe);
        actionFlow.Controls.Add(compileProgress);
        actionFlow.Controls.Add(CreateToolbarSeparator());
        actionFlow.Controls.Add(btnToggleSplit);
        actionFlow.Controls.Add(btnToggleTheme);
        actionFlow.Controls.Add(cmbOfficialSamples);

        toolbarPanel.Controls.Add(actionFlow);
        toolbarPanel.Controls.Add(titleLabel);
        return toolbarPanel;
    }

    private static Control CreateToolbarSeparator()
    {
        return new Label
        {
            Width = 1,
            Height = 28,
            BackColor = Color.FromArgb(70, 75, 100),
            Margin = new Padding(6, 6, 6, 6)
        };
    }

    private Control CreateStageBar()
    {
        var barPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(8, 4, 8, 4),
            BackColor = IdeTheme.SurfaceElevated
        };

        var stageTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = StageNames.Length,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.Transparent,
            RightToLeft = RightToLeft.Yes
        };
        stageTable.ColumnStyles.Clear();
        stageTable.RowStyles.Clear();
        stageTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        for (int i = 0; i < StageNames.Length; i++)
        {
            stageTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / StageNames.Length));

            int stageIndex = i;
            var button = new Button
            {
                Text = StageNames[i],
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(2, 0, 2, 0),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                AccessibleName = StageNames[i]
            };
            button.FlatAppearance.BorderSize = 1;
            button.Click += (_, _) => SelectStage(stageIndex);

            stageButtons[i] = button;
            stageTable.Controls.Add(button, i, 0);
        }

        barPanel.Controls.Add(stageTable);
        return barPanel;
    }

    private Control CreateWorkspace()
    {
        workspaceSplitter.Dock = DockStyle.Fill;
        workspaceSplitter.Orientation = Orientation.Vertical;
        workspaceSplitter.RightToLeft = RightToLeft.No; // Keep SplitContainer LTR to avoid WinForms RTL splitter bugs
        workspaceSplitter.SplitterWidth = 6;
        workspaceSplitter.BackColor = IdeTheme.Border;

        // In LTR layout:
        // Panel 1 is on the LEFT -> Stage Viewer (output, terminal, analysis)
        // Panel 2 is on the RIGHT -> Code Editor (source code editor)
        // This gives the exact expected Arabic flow: Editor on the Right, Output on the Left!

        stageViewerHost.Dock = DockStyle.Fill;
        stageViewerHost.Padding = new Padding(4);
        stageViewerHost.RightToLeft = RightToLeft.Yes;

        var stageHeaderPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 54,
            Padding = new Padding(12, 6, 12, 6)
        };

        stageTitleLabel.Font = IdeTheme.HeaderFont;
        stageTitleLabel.Dock = DockStyle.Top;
        stageTitleLabel.Height = 24;

        stageSubtitleLabel.Font = IdeTheme.UiFontRegular;
        stageSubtitleLabel.Dock = DockStyle.Bottom;
        stageSubtitleLabel.Height = 18;

        stageHeaderPanel.Controls.Add(stageTitleLabel);
        stageHeaderPanel.Controls.Add(stageSubtitleLabel);

        // Read-only Artifact Text Viewer
        artifactViewer.Dock = DockStyle.Fill;
        artifactViewer.ReadOnly = true;
        artifactViewer.BorderStyle = BorderStyle.None;
        artifactViewer.Font = IdeTheme.ViewerCodeFont;
        artifactViewer.ScrollBars = RichTextBoxScrollBars.Both;
        artifactViewer.Padding = new Padding(12);

        terminalControl.Dock = DockStyle.Fill;
        terminalControl.RerunRequested += (_, _) => RunExecutable();

        var viewerContainer = new Panel
        {
            Dock = DockStyle.Fill
        };
        viewerContainer.Controls.Add(artifactViewer);
        viewerContainer.Controls.Add(terminalControl);

        stageViewerHost.Controls.Add(viewerContainer);
        stageViewerHost.Controls.Add(stageHeaderPanel);

        workspaceSplitter.Panel1.Controls.Add(stageViewerHost);

        sourceEditor.Dock = DockStyle.Fill;
        workspaceSplitter.Panel2.Controls.Add(sourceEditor);

        return workspaceSplitter;
    }

    private Control CreateFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            Padding = new Padding(16, 6, 16, 6),
            BackColor = IdeTheme.SurfaceElevated
        };

        var footerTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.Transparent,
            RightToLeft = RightToLeft.Yes
        };
        footerTable.ColumnStyles.Clear();
        footerTable.RowStyles.Clear();
        footerTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
        footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

        statusMessageLabel.Text = "جاهز — اكتب الكود المصدري ثم اضغط «ترجمة ⚙ (F6)»";
        statusMessageLabel.Dock = DockStyle.Fill;
        statusMessageLabel.Font = IdeTheme.UiFontRegular;
        statusMessageLabel.TextAlign = ContentAlignment.MiddleRight;

        currentTargetLabel.Text = "الهدف: x86 Assembly + ilasm (output.exe)";
        currentTargetLabel.Dock = DockStyle.Fill;
        currentTargetLabel.Font = IdeTheme.UiFontBold;
        currentTargetLabel.TextAlign = ContentAlignment.MiddleLeft;

        footerTable.Controls.Add(statusMessageLabel, 0, 0);
        footerTable.Controls.Add(currentTargetLabel, 1, 0);
        footer.Controls.Add(footerTable);
        return footer;
    }

    private void ConfigureToolButton(Button btn, string text, string tooltip, EventHandler onClick, bool isPrimary = false, Color? primaryColor = null)
    {
        btn.Text = text;
        btn.AutoSize = true;
        btn.MinimumSize = new Size(64, 34);
        btn.Height = 34;
        btn.FlatStyle = FlatStyle.Flat;
        btn.Font = isPrimary ? IdeTheme.UiFontBold : IdeTheme.UiFontRegular;
        btn.Cursor = Cursors.Hand;
        btn.Margin = new Padding(3, 3, 3, 3);
        btn.Padding = new Padding(8, 4, 8, 4);
        toolTips.SetToolTip(btn, tooltip);
        btn.Click += onClick;

        if (isPrimary && primaryColor.HasValue)
        {
            btn.BackColor = primaryColor.Value;
            btn.ForeColor = Color.White;
            btn.FlatAppearance.BorderSize = 0;
        }
    }

    private void SelectStage(int stage)
    {
        if (stage < 0 || stage >= StageNames.Length)
        {
            return;
        }

        activeStage = stage;
        stageTitleLabel.Text = StageNames[stage];
        stageSubtitleLabel.Text = StageSubtitles[stage];

        // Highlight active tab
        for (int i = 0; i < stageButtons.Length; i++)
        {
            bool isCurrent = i == stage;
            stageButtons[i].BackColor = isCurrent ? IdeTheme.AccentPrimary : IdeTheme.Surface;
            stageButtons[i].ForeColor = isCurrent ? Color.White : IdeTheme.TextSecondary;
            stageButtons[i].FlatAppearance.BorderColor = isCurrent ? IdeTheme.AccentPrimary : IdeTheme.Border;
        }

        if (isSplitView)
        {
            workspaceSplitter.Panel1Collapsed = false;
            workspaceSplitter.Panel2Collapsed = false;
        }
        else
        {
            if (stage == 0)
            {
                workspaceSplitter.Panel1Collapsed = true;
                workspaceSplitter.Panel2Collapsed = false;
            }
            else
            {
                workspaceSplitter.Panel1Collapsed = false;
                workspaceSplitter.Panel2Collapsed = true;
            }
        }

        if (stage == 8) // Terminal for output.exe
        {
            artifactViewer.Visible = false;
            terminalControl.Visible = true;
        }
        else if (stage == 0) // Source Overview in Left pane, focus Editor on Right pane
        {
            terminalControl.Visible = false;
            artifactViewer.Visible = true;
            artifactViewer.Text = GetSourceSummary();
            artifactViewer.RightToLeft = RightToLeft.Yes;
            sourceEditor.InnerEditor.Focus();
        }
        else
        {
            terminalControl.Visible = false;
            artifactViewer.Visible = true;
            artifactViewer.Text = stageArtifacts[stage];

            // Right to left vs Left to Right based on artifact
            bool isCode = stage is 1 or 5 or 6;
            artifactViewer.RightToLeft = isCode ? RightToLeft.No : RightToLeft.Yes;
        }

        statusMessageLabel.Text = $"المرحلة الحالية: {StageNames[stage]}";
    }

    private string GetSourceSummary()
    {
        int lines = sourceEditor.InnerEditor.Lines.Length;
        int chars = sourceEditor.InnerEditor.TextLength;
        string file = string.IsNullOrEmpty(currentSourcePath) ? "ملف بيان جديد (غير محفوظ)" : Path.GetFileName(currentSourcePath);

        return $"=== نظرة عامة على الكود المصدر (المرحلة 01) ===\n\n" +
               $"• الملف الحالي: {file}\n" +
               $"• عدد الأسطر: {lines}\n" +
               $"• إجمالي الأحرف: {chars}\n\n" +
               $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
               $"دليل سير عمل المترجم:\n" +
               $"1. اكتب أو عدّل كود بيان في المحرر المقابل (جهة اليمين).\n" +
               $"2. اضغط «ترجمة ⚙ (F6)» لتشغيل مراحل المترجم بالترتيب واستخراج المخرجات.\n" +
               $"3. اضغط «تشغيل ▶ (F5)» لتشغيل output.exe فورياً عبر الطرفية التفاعلية.\n" +
               $"4. يمكنك النقر على أي مرحلة من المراحل التسع أعلاه لفحص نتائجها مباشرة دون إخفاء الكود المصدري.";
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

            stageArtifacts[1] = run.GetArtifact("tokens.txt");
            stageArtifacts[2] = run.GetArtifact("parse-tree.txt");
            stageArtifacts[3] = run.GetArtifact("symbol-table.txt");

            string diagnostics = run.GetArtifact("diagnostics.txt");
            stageArtifacts[4] = string.IsNullOrWhiteSpace(diagnostics) ? "تم التحليل الدلالي بنجاح بدون أخطاء." : diagnostics;
            stageArtifacts[5] = run.GetArtifact("three-address-code.txt");
            stageArtifacts[6] = run.GetArtifact("output.asm");
            stageArtifacts[7] = string.IsNullOrWhiteSpace(diagnostics) ? "لا توجد أي أخطاء نحوية أو دلالية. الكود سليم بنسبة 100%." : diagnostics;

            lastExecutablePath = run.Manifest?.WindowsExecutableAvailable == true
                ? run.Manifest.WindowsExecutablePath
                : string.Empty;

            bool succeeded = run.Manifest?.Success == true && run.ExitCode == 0;
            btnRunExe.Enabled = succeeded && !string.IsNullOrWhiteSpace(lastExecutablePath) && File.Exists(lastExecutablePath);

            if (succeeded)
            {
                statusMessageLabel.Text = $"اكتملت الترجمة بنجاح! تم بناء output.exe وoutput.asm في: {run.Manifest?.CompletedStage}";
                statusMessageLabel.ForeColor = IdeTheme.AccentSuccess;

                // Move to Assembly stage or Execution stage to inspect
                SelectStage(6); // Assembly Code
            }
            else
            {
                statusMessageLabel.Text = $"توقفت الترجمة عند مرحلة: {run.Manifest?.CompletedStage ?? "الأخطاء"}";
                statusMessageLabel.ForeColor = IdeTheme.AccentError;
                SelectStage(7); // Errors Stage
            }
        }
        catch (Exception ex)
        {
            stageArtifacts[7] = "[خطأ بيئة المحرر] " + ex.Message;
            statusMessageLabel.Text = "فشل تشغيل CLI المستقل: " + ex.Message;
            statusMessageLabel.ForeColor = IdeTheme.AccentError;
            SelectStage(7);
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
            MessageBox.Show(this, "يرجى تشغيل «تحليل وترجمة ⚙» أولاً لبناء ملف output.exe.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SelectStage(8); // Switch to Terminal Stage
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

    private void ToggleSplitView()
    {
        isSplitView = !isSplitView;
        btnToggleSplit.Text = isSplitView ? "عرض منقسم ◫" : "عرض عريض 🗖";

        if (isSplitView)
        {
            workspaceSplitter.Panel1Collapsed = false;
            workspaceSplitter.Panel2Collapsed = false;
            if (workspaceSplitter.Width > 200)
            {
                workspaceSplitter.SplitterDistance = workspaceSplitter.Width / 2;
            }
        }
        else
        {
            if (activeStage == 0)
            {
                workspaceSplitter.Panel1Collapsed = true;
                workspaceSplitter.Panel2Collapsed = false;
            }
            else
            {
                workspaceSplitter.Panel1Collapsed = false;
                workspaceSplitter.Panel2Collapsed = true;
            }
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

        sourceEditor.ApplyTheme();
        terminalControl.ApplyTheme();

        artifactViewer.BackColor = IdeTheme.Surface;
        artifactViewer.ForeColor = IdeTheme.TextPrimary;

        stageViewerHost.BackColor = IdeTheme.Surface;
        stageTitleLabel.ForeColor = IdeTheme.TextPrimary;
        stageSubtitleLabel.ForeColor = IdeTheme.TextSecondary;

        statusMessageLabel.ForeColor = IdeTheme.TextSecondary;
        currentTargetLabel.ForeColor = IdeTheme.AccentCyan;

        // Apply to toolbar buttons
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

        SelectStage(activeStage);
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
        sourceEditor.SourceCode = "برنامج تجربة_جديدة;\nمتغير س : صحيح;\n{\n    س = 100;\n    اطبع(س);\n}.";
        currentSourcePath = string.Empty;
        lastExecutablePath = string.Empty;
        btnRunExe.Enabled = false;
        InitializeDefaultArtifacts();
        SelectStage(0);
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
            SelectStage(0);
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
