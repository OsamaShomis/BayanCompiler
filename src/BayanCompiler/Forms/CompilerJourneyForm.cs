using System.Diagnostics;
using System.Text.RegularExpressions;

namespace BayanCompiler.Forms;

/// <summary>
/// Arabic-First Visual Studio Code interface for the Bayan programming language compiler.
/// Fully customized for the Arabic language (Right-to-Left architecture):
/// 1. Right-Docked Activity Bar (48px): Explorer, Compiler Stages, Run & Debug, Theme, Settings
/// 2. Right-Docked SideBar (260px): Open Editors, Official Samples TreeView, 9 Compiler Stages
/// 3. Arabic Top Menu Bar (ملف, تحرير, عرض, تشغيل, عينات بيان, مساعدة)
/// 4. Command Center Bar with Arabic Quick Actions (F6 ترجمة, F5 تشغيل, عينات سريعة)
/// 5. Right-to-Left Multi-Tab Editor Workspace (Active #007ACC top indicator line, Breadcrumbs)
/// 6. Left-Docked Stage Inspector (Tokens, AST, Symbols, TAC, Assembly, Semantics)
/// 7. Right-to-Left Bottom Panel (Terminal, Problems with line jump, Pipeline Report)
/// 8. Signature #007ACC Blue Status Bar
/// </summary>
public sealed class CompilerJourneyForm : Form
{
    // Execution clients
    private readonly CliCompilationClient compilerClient = new();
    private readonly WindowsExecutableRuntimeClient executableRuntimeClient = new();

    // Document Management (Multi-tab support)
    private sealed class BayanDocument
    {
        public string FileName { get; set; } = "main.bayan";
        public string FilePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsModified { get; set; } = false;
    }

    private readonly List<BayanDocument> openDocuments = new();
    private int activeDocumentIndex = 0;

    // Core workspace components
    private readonly BayanCodeEditor sourceEditor = new();
    private readonly BayanTerminalControl terminalControl = new();

    // Structural containers
    private readonly MenuStrip vsCodeMenuBar = new();
    private readonly Panel commandCenterBar = new();
    private readonly Panel activityBar = new();
    private readonly Panel sideBar = new();
    private readonly Panel sideBarHeader = new();
    private readonly Label lblSideBarTitle = new();
    private readonly Panel sideBarContent = new();
    private readonly TreeView tvExplorer = new();
    private readonly ListBox lstStages = new();
    private readonly Panel runSideBarPanel = new();

    // Editor Area Containers
    private readonly Panel editorWorkspace = new();
    private readonly Panel editorTabBar = new();
    private readonly FlowLayoutPanel tabsFlowPanel = new();
    private readonly Button btnNewTab = new();
    private readonly Panel breadcrumbsBar = new();
    private readonly Label lblBreadcrumbs = new();

    // Splitters
    private readonly SplitContainer mainSplitter = new();   // Workspace (Top) vs Bottom Panel (Bottom)
    private readonly SplitContainer upperSplitter = new();  // Editor (Right 60%) vs Stage Inspector (Left 40%)
    private readonly TabControl inspectorTabs = new();
    private readonly TabControl bottomTabs = new();

    // Inspector tab content viewers (monospaced, zero overlap)
    private readonly RichTextBox txtTokens = CreateMonospaceViewer();
    private readonly RichTextBox txtParseTree = CreateMonospaceViewer();
    private readonly RichTextBox txtSymbolTable = CreateMonospaceViewer();
    private readonly RichTextBox txtTac = CreateMonospaceViewer();
    private readonly RichTextBox txtAssembly = CreateMonospaceViewer();
    private readonly RichTextBox txtSemantics = CreateMonospaceViewer();

    // Bottom tab content viewers
    private readonly RichTextBox txtDiagnostics = CreateMonospaceViewer();
    private readonly RichTextBox txtPipelineReport = CreateMonospaceViewer();

    // Activity Bar Buttons
    private readonly Button btnActExplorer = new();
    private readonly Button btnActStages = new();
    private readonly Button btnActRun = new();
    private readonly Button btnActTheme = new();
    private readonly Button btnActSettings = new();

    // Command Center Controls
    private readonly Button btnQuickCompile = new();
    private readonly Button btnQuickRun = new();
    private readonly Button btnToggleInspector = new();
    private readonly Button btnToggleBottomPanel = new();
    private readonly ComboBox cmbQuickSamples = new();
    private readonly ProgressBar compileProgress = new();
    private readonly ToolTip toolTips = new();

    // VS Code Blue Status Bar
    private readonly Panel vsCodeStatusBar = new();
    private readonly Label statusGitLabel = new();
    private readonly Label statusProblemsLabel = new();
    private readonly Label statusMessageLabel = new();
    private readonly Label statusCursorLabel = new();
    private readonly Label statusEncodingLabel = new();
    private readonly Label statusLangLabel = new();
    private readonly Label statusTargetLabel = new();

    // State tracking
    private bool isBusy = false;
    private bool isSideBarOpen = true;
    private bool isInspectorOpen = true;
    private string activeSideBarView = "EXPLORER"; // "EXPLORER", "STAGES", "RUN"
    private string lastExecutablePath = string.Empty;
    private int errorCount = 0;
    private int warningCount = 0;

    public CompilerJourneyForm(string? initialSourcePath = null)
    {
        Text = "بيان  |  Bayan IDE — بيئة التطوير المتكاملة ومترجم لغة بيان الرسمية";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1150, 750);
        Size = new Size(1380, 890);
        RightToLeft = RightToLeft.Yes; // Native Arabic RTL flow across menus, tabs, and trees
        RightToLeftLayout = false;     // Maintain stable coordinate rendering
        Font = IdeTheme.UiFontRegular;
        DoubleBuffered = true;
        KeyPreview = true;

        // Hide internal status strip of editor in favor of main status bar
        sourceEditor.ShowInternalStatusBar = false;

        InitializeDefaultArtifacts();
        InitializeInitialDocument(initialSourcePath);
        BuildArabicVsCodeLayout();
        PopulateExplorerTree();
        PopulateQuickSamplesDropdown();
        ApplyActiveTheme();

        // Global Keyboard shortcuts
        KeyDown += Form_KeyDown;

        // Diagnostic click-to-navigate in code
        txtDiagnostics.DoubleClick += TxtDiagnostics_DoubleClick;

        // Editor cursor position sync with VS Code status bar
        sourceEditor.CursorPositionChanged += (_, pos) =>
        {
            statusCursorLabel.Text = $"السطر {pos.Line} ، العمود {pos.Column}";
        };

        // Mark document as modified when edited
        sourceEditor.ContentModified += (_, _) =>
        {
            if (activeDocumentIndex >= 0 && activeDocumentIndex < openDocuments.Count)
            {
                openDocuments[activeDocumentIndex].IsModified = true;
                openDocuments[activeDocumentIndex].Content = sourceEditor.SourceCode;
            }
        };

        Shown += (_, _) => ApplyProportionalSplitters();
        Resize += (_, _) =>
        {
            if (WindowState != FormWindowState.Minimized)
            {
                ApplyProportionalSplitters();
            }
        };
    }

    private void InitializeInitialDocument(string? initialSourcePath)
    {
        openDocuments.Clear();
        if (!string.IsNullOrWhiteSpace(initialSourcePath) && File.Exists(initialSourcePath))
        {
            try
            {
                string content = File.ReadAllText(initialSourcePath);
                openDocuments.Add(new BayanDocument
                {
                    FileName = Path.GetFileName(initialSourcePath),
                    FilePath = initialSourcePath,
                    Content = content
                });
            }
            catch
            {
                openDocuments.Add(CreateDefaultDocument());
            }
        }
        else
        {
            openDocuments.Add(CreateDefaultDocument());
        }

        activeDocumentIndex = 0;
        sourceEditor.SourceCode = openDocuments[0].Content;
    }

    private static BayanDocument CreateDefaultDocument()
    {
        return new BayanDocument
        {
            FileName = "main.bayan",
            FilePath = string.Empty,
            Content =
                "برنامج تجربة;\n" +
                "متغير س : صحيح;\n" +
                "{\n" +
                "    س = 15;\n" +
                "    اطبع(س);\n" +
                "}."
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

    #region Arabic-First Layout Construction

    private void BuildArabicVsCodeLayout()
    {
        Controls.Clear();

        // 1. VS Code Menu Bar (Topmost)
        BuildArabicMenuBar();

        // 2. Command Center Bar (Below Menu Bar)
        BuildArabicCommandCenterBar();

        // 3. Status Bar (Very bottom)
        BuildArabicStatusBar();

        // 4. Activity Bar (Rightmost 48px in Arabic)
        BuildArabicActivityBar();

        // 5. SideBar (Right 260px next to Activity Bar)
        BuildArabicSideBar();

        // 6. Editor Workspace Area (Fills the rest on the left)
        BuildArabicEditorWorkspace();

        // Order of addition in WinForms determines docking priority
        Controls.Add(editorWorkspace);
        Controls.Add(sideBar);
        Controls.Add(activityBar);
        Controls.Add(vsCodeStatusBar);
        Controls.Add(commandCenterBar);
        Controls.Add(vsCodeMenuBar);
    }

    private void BuildArabicMenuBar()
    {
        vsCodeMenuBar.Dock = DockStyle.Top;
        vsCodeMenuBar.Font = IdeTheme.UiFontRegular;
        vsCodeMenuBar.BackColor = IdeTheme.TitleBar;
        vsCodeMenuBar.ForeColor = IdeTheme.TextPrimary;
        vsCodeMenuBar.RightToLeft = RightToLeft.Yes;
        vsCodeMenuBar.RenderMode = ToolStripRenderMode.System;

        // 1. ملف (File)
        var mnuFile = new ToolStripMenuItem("ملف (&F)");
        mnuFile.DropDownItems.Add("ملف جديد (&N)\tCtrl+N", null, (_, _) => NewFile());
        mnuFile.DropDownItems.Add("فتح ملف (&O)...\tCtrl+O", null, (_, _) => OpenFileDialog());
        mnuFile.DropDownItems.Add("حفظ (&S)\tCtrl+S", null, (_, _) => SaveCurrentDocument());
        mnuFile.DropDownItems.Add("حفظ باسم (&A)...", null, (_, _) => SaveAsDialog());
        mnuFile.DropDownItems.Add(new ToolStripSeparator());
        mnuFile.DropDownItems.Add("إغلاق التبويب الحالي\tCtrl+W", null, (_, _) => CloseCurrentTab());
        mnuFile.DropDownItems.Add(new ToolStripSeparator());
        mnuFile.DropDownItems.Add("خروج (&X)\tAlt+F4", null, (_, _) => Close());

        // 2. تحرير (Edit)
        var mnuEdit = new ToolStripMenuItem("تحرير (&E)");
        mnuEdit.DropDownItems.Add("تراجع (&U)\tCtrl+Z", null, (_, _) => sourceEditor.InnerEditor.Undo());
        mnuEdit.DropDownItems.Add("إعادة (&R)\tCtrl+Y", null, (_, _) => sourceEditor.InnerEditor.Redo());
        mnuEdit.DropDownItems.Add(new ToolStripSeparator());
        mnuEdit.DropDownItems.Add("قص (&T)\tCtrl+X", null, (_, _) => sourceEditor.InnerEditor.Cut());
        mnuEdit.DropDownItems.Add("نسخ (&C)\tCtrl+C", null, (_, _) => sourceEditor.InnerEditor.Copy());
        mnuEdit.DropDownItems.Add("لصق (&P)\tCtrl+V", null, (_, _) => sourceEditor.InnerEditor.Paste());
        mnuEdit.DropDownItems.Add(new ToolStripSeparator());
        mnuEdit.DropDownItems.Add("تحديد الكل (&A)\tCtrl+A", null, (_, _) => sourceEditor.InnerEditor.SelectAll());

        // 3. عرض (View)
        var mnuView = new ToolStripMenuItem("عرض (&V)");
        mnuView.DropDownItems.Add("تبديل الشريط الجانبي\tCtrl+B", null, (_, _) => ToggleSideBar());
        mnuView.DropDownItems.Add("تبديل الطرفية واللوحة السفلية\tCtrl+J", null, (_, _) => ToggleBottomPanel());
        mnuView.DropDownItems.Add("فاحص المراحل الجانبي\tCtrl+\\", null, (_, _) => ToggleInspectorView());
        mnuView.DropDownItems.Add(new ToolStripSeparator());
        mnuView.DropDownItems.Add("تبديل السمة (Dark / Light)\tF7", null, (_, _) => ToggleTheme());

        // 4. تشغيل (Run)
        var mnuRun = new ToolStripMenuItem("تشغيل (&R)");
        mnuRun.DropDownItems.Add("ترجمة وبناء المشروع (&B)\tF6", null, (_, _) => CompileSource());
        mnuRun.DropDownItems.Add("تشغيل في الطرفية (&R)\tF5", null, (_, _) => RunExecutable());
        mnuRun.DropDownItems.Add(new ToolStripSeparator());
        mnuRun.DropDownItems.Add("إعادة تشغيل الطرفية", null, (_, _) => terminalControl.ClearAndReset());

        // 5. عينات بيان (Samples)
        var mnuSamples = new ToolStripMenuItem("عينات بيان (&S)");
        PopulateSamplesMenu(mnuSamples);

        // 6. مساعدة (Help)
        var mnuHelp = new ToolStripMenuItem("مساعدة (&H)");
        mnuHelp.DropDownItems.Add("عن مترجم لغة بيان...", null, (_, _) => ShowAboutDialog());
        mnuHelp.DropDownItems.Add("دليل قواعد لغة بيان الرسمية", null, (_, _) => ShowGrammarGuide());

        vsCodeMenuBar.Items.Add(mnuFile);
        vsCodeMenuBar.Items.Add(mnuEdit);
        vsCodeMenuBar.Items.Add(mnuView);
        vsCodeMenuBar.Items.Add(mnuRun);
        vsCodeMenuBar.Items.Add(mnuSamples);
        vsCodeMenuBar.Items.Add(mnuHelp);
    }

    private void PopulateSamplesMenu(ToolStripMenuItem parentMenu)
    {
        parentMenu.DropDownItems.Clear();
        string samplesDir = FindSamplesDirectory();
        if (Directory.Exists(samplesDir))
        {
            string[] files = Directory.GetFiles(samplesDir, "*.bayan");
            Array.Sort(files);
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                parentMenu.DropDownItems.Add(fileName, null, (_, _) => LoadFileIntoNewTab(file));
            }
        }
        else
        {
            parentMenu.DropDownItems.Add("لم يتم العثور على مجلد العينات");
        }
    }

    private void BuildArabicCommandCenterBar()
    {
        commandCenterBar.Dock = DockStyle.Top;
        commandCenterBar.Height = 44;
        commandCenterBar.Padding = new Padding(10, 4, 10, 4);
        commandCenterBar.BackColor = IdeTheme.TitleBar;
        commandCenterBar.RightToLeft = RightToLeft.Yes;

        // Right Branding / Logo (Arabic start edge)
        var lblLogo = new Label
        {
            Text = "⚡ محرر بيان  |  Bayan IDE",
            Font = IdeTheme.HeaderFont,
            ForeColor = IdeTheme.AccentCyan,
            AutoSize = true,
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(16, 4, 4, 4)
        };

        // Left Action Buttons Container
        var leftActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = new Padding(0, 2, 0, 0)
        };

        // 1. Compile Button (F6)
        ConfigureActionBtn(btnQuickCompile, "ترجمة ⚙ (F6)", "تشغيل مراحل المترجم كاملة وإنتاج TAC وx86 Assembly وEXE (F6)", (_, _) => CompileSource(), isPrimary: true, primaryColor: IdeTheme.AccentPrimary);

        // 2. Run Button (F5)
        ConfigureActionBtn(btnQuickRun, "تشغيل ▶ (F5)", "تشغيل output.exe فوراً في الطرفية التفاعلية (F5)", (_, _) => RunExecutable(), isPrimary: true, primaryColor: IdeTheme.AccentSuccess);
        btnQuickRun.Enabled = false;

        // 3. Toggle Inspector Button
        ConfigureActionBtn(btnToggleInspector, "◫ فاحص المراحل", "إظهار أو طي لوحة فاحص مراحل المترجم الجانبية (Ctrl+\\)", (_, _) => ToggleInspectorView());

        // 4. Toggle Bottom Panel Button
        ConfigureActionBtn(btnToggleBottomPanel, "▤ الطرفية والتقرير", "إظهار أو طي اللوحة السفلية للطرفية والأخطاء (Ctrl+J)", (_, _) => ToggleBottomPanel());

        // 5. Quick Samples Dropdown
        cmbQuickSamples.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbQuickSamples.Width = 200;
        cmbQuickSamples.Height = 30;
        cmbQuickSamples.Font = IdeTheme.UiFontRegular;
        cmbQuickSamples.Margin = new Padding(6, 4, 6, 0);
        toolTips.SetToolTip(cmbQuickSamples, "اختر من عينات بيان الرسمية للفتح المباشر");
        cmbQuickSamples.SelectedIndexChanged += CmbQuickSamples_SelectedIndexChanged;

        // Progress bar
        compileProgress.Width = 80;
        compileProgress.Height = 16;
        compileProgress.Style = ProgressBarStyle.Marquee;
        compileProgress.MarqueeAnimationSpeed = 30;
        compileProgress.Visible = false;
        compileProgress.Margin = new Padding(6, 10, 6, 0);

        leftActions.Controls.Add(btnQuickCompile);
        leftActions.Controls.Add(btnQuickRun);
        leftActions.Controls.Add(compileProgress);
        leftActions.Controls.Add(btnToggleInspector);
        leftActions.Controls.Add(btnToggleBottomPanel);
        leftActions.Controls.Add(cmbQuickSamples);

        // Center Search / Command Box
        var centerBox = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(30, 2, 30, 2)
        };

        var searchPill = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = IdeTheme.CommandCenter,
            Cursor = Cursors.Hand
        };
        searchPill.Click += (_, _) => OpenFileDialog();

        var lblSearch = new Label
        {
            Text = "🔍 محرر بيان — اضغط F6 للترجمة ، F5 للتشغيل ، Ctrl+O لفتح ملف",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = IdeTheme.UiFontRegular,
            ForeColor = IdeTheme.TextSecondary,
            Cursor = Cursors.Hand
        };
        lblSearch.Click += (_, _) => OpenFileDialog();
        searchPill.Controls.Add(lblSearch);
        centerBox.Controls.Add(searchPill);

        commandCenterBar.Controls.Add(centerBox);
        commandCenterBar.Controls.Add(leftActions);
        commandCenterBar.Controls.Add(lblLogo);
    }

    private void BuildArabicActivityBar()
    {
        activityBar.Dock = DockStyle.Right; // Right-docked for natural Arabic entry point
        activityBar.Width = 48;
        activityBar.Padding = new Padding(0, 4, 0, 8);
        activityBar.BackColor = IdeTheme.ActivityBar;
        activityBar.RightToLeft = RightToLeft.No;

        // Activity Bar Buttons
        ConfigureActivityButton(btnActExplorer, "📁", "مستكشف الملفات والمشروع (Ctrl+B)", () => SwitchSideBarView("EXPLORER"), "EXPLORER");
        ConfigureActivityButton(btnActStages, "⚡", "مراحل المترجم التسع الرسمية", () => SwitchSideBarView("STAGES"), "STAGES");
        ConfigureActivityButton(btnActRun, "▶", "التنفيذ والتصحيح (F5 / F6)", () => SwitchSideBarView("RUN"), "RUN");
        ConfigureActivityButton(btnActTheme, "🌙", "تبديل السمة (داكن / فاتح) (F7)", () => ToggleTheme(), "THEME");
        ConfigureActivityButton(btnActSettings, "⚙", "عن لغة بيان والإعدادات", () => ShowAboutDialog(), "SETTINGS");

        var topStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        topStack.Controls.Add(btnActExplorer);
        topStack.Controls.Add(btnActStages);
        topStack.Controls.Add(btnActRun);

        var bottomStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.BottomUp,
            AutoSize = true,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        bottomStack.Controls.Add(btnActSettings);
        bottomStack.Controls.Add(btnActTheme);

        activityBar.Controls.Add(topStack);
        activityBar.Controls.Add(bottomStack);
    }

    private void ConfigureActivityButton(Button btn, string icon, string tooltip, Action onClick, string viewTag)
    {
        btn.Text = icon;
        btn.Tag = viewTag;
        btn.Size = new Size(48, 48);
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.Font = IdeTheme.ActivityIconFont;
        btn.ForeColor = IdeTheme.TextSecondary;
        btn.BackColor = IdeTheme.ActivityBar;
        btn.Cursor = Cursors.Hand;
        btn.Margin = Padding.Empty;
        btn.TextAlign = ContentAlignment.MiddleCenter;
        toolTips.SetToolTip(btn, tooltip);
        btn.Click += (_, _) => onClick();

        // Paint indicator line on the RIGHT edge of active button
        btn.Paint += (s, e) =>
        {
            if (s is Button b && b.Tag is string tag && tag == activeSideBarView && isSideBarOpen)
            {
                using var pen = new Pen(IdeTheme.ActivityBarActive, 3);
                e.Graphics.DrawLine(pen, b.Width - 2, 0, b.Width - 2, b.Height);
            }
        };
    }

    private void BuildArabicSideBar()
    {
        sideBar.Dock = DockStyle.Right; // Next to Activity Bar on the right
        sideBar.Width = 260;
        sideBar.Padding = Padding.Empty;
        sideBar.BackColor = IdeTheme.SideBar;
        sideBar.RightToLeft = RightToLeft.Yes;

        // SideBar Header
        sideBarHeader.Dock = DockStyle.Top;
        sideBarHeader.Height = 36;
        sideBarHeader.Padding = new Padding(8, 4, 12, 4);
        sideBarHeader.BackColor = IdeTheme.SideBarHeader;

        lblSideBarTitle.Text = "المستكشف : بيئة بيان";
        lblSideBarTitle.Font = IdeTheme.BadgeFont;
        lblSideBarTitle.ForeColor = IdeTheme.TextSecondary;
        lblSideBarTitle.Dock = DockStyle.Right;
        lblSideBarTitle.TextAlign = ContentAlignment.MiddleRight;
        lblSideBarTitle.AutoSize = true;

        var headerActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        var btnNew = CreateHeaderActionBtn("📄+", "ملف جديد (Ctrl+N)", (_, _) => NewFile());
        var btnOpen = CreateHeaderActionBtn("📂", "فتح ملف (Ctrl+O)", (_, _) => OpenFileDialog());
        var btnRefresh = CreateHeaderActionBtn("↻", "تحديث قائمة العينات", (_, _) => PopulateExplorerTree());

        headerActions.Controls.Add(btnRefresh);
        headerActions.Controls.Add(btnOpen);
        headerActions.Controls.Add(btnNew);

        sideBarHeader.Controls.Add(lblSideBarTitle);
        sideBarHeader.Controls.Add(headerActions);

        // SideBar Content Area
        sideBarContent.Dock = DockStyle.Fill;
        sideBarContent.Padding = new Padding(2);

        // 1. Explorer View: TreeView
        tvExplorer.Dock = DockStyle.Fill;
        tvExplorer.BorderStyle = BorderStyle.None;
        tvExplorer.Font = IdeTheme.UiFontRegular;
        tvExplorer.RightToLeft = RightToLeft.Yes;
        tvExplorer.FullRowSelect = true;
        tvExplorer.ShowLines = true;
        tvExplorer.ShowPlusMinus = true;
        tvExplorer.ShowRootLines = true;
        tvExplorer.Cursor = Cursors.Hand;
        tvExplorer.NodeMouseClick += TvExplorer_NodeMouseClick;

        // 2. Stages View: ListBox
        lstStages.Dock = DockStyle.Fill;
        lstStages.BorderStyle = BorderStyle.None;
        lstStages.Font = IdeTheme.UiFontRegular;
        lstStages.RightToLeft = RightToLeft.Yes;
        lstStages.ItemHeight = 30;
        lstStages.Cursor = Cursors.Hand;
        lstStages.SelectedIndexChanged += LstStages_SelectedIndexChanged;
        PopulateStagesList();

        // 3. Run & Debug View
        BuildArabicRunSideBarPanel();

        sideBarContent.Controls.Add(tvExplorer);

        sideBar.Controls.Add(sideBarContent);
        sideBar.Controls.Add(sideBarHeader);
    }

    private Button CreateHeaderActionBtn(string text, string tooltip, EventHandler onClick)
    {
        var btn = new Button
        {
            Text = text,
            Size = new Size(26, 26),
            FlatStyle = FlatStyle.Flat,
            Font = IdeTheme.BadgeFont,
            ForeColor = IdeTheme.TextSecondary,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Margin = new Padding(2)
        };
        btn.FlatAppearance.BorderSize = 0;
        toolTips.SetToolTip(btn, tooltip);
        btn.Click += onClick;
        return btn;
    }

    private void PopulateStagesList()
    {
        lstStages.Items.Clear();
        lstStages.Items.Add(" [01] 📝 محرر الكود (Source Editor)");
        lstStages.Items.Add(" [02] 🔤 الرموز المعجمية (Tokens)");
        lstStages.Items.Add(" [03] 🌳 شجرة الإعراب (Parse Tree)");
        lstStages.Items.Add(" [04] 📋 جدول الرموز (Symbol Table)");
        lstStages.Items.Add(" [05] 🔬 فحص الأنواع (Semantics)");
        lstStages.Items.Add(" [06] ⚡ الكود الوسيط (TAC / IR)");
        lstStages.Items.Add(" [07] ⚙ لغة التجميع (x86 Assembly)");
        lstStages.Items.Add(" [08] ⚠️ الأخطاء والتشخيصات (Diagnostics)");
        lstStages.Items.Add(" [09] 🖥️ الطرفية والتنفيذ (Terminal / EXE)");
    }

    private void BuildArabicRunSideBarPanel()
    {
        runSideBarPanel.Dock = DockStyle.Fill;
        runSideBarPanel.Padding = new Padding(12);
        runSideBarPanel.RightToLeft = RightToLeft.Yes;

        var btnRun = new Button
        {
            Text = "▶  تشغيل البرنامج (F5)",
            Dock = DockStyle.Top,
            Height = 38,
            FlatStyle = FlatStyle.Flat,
            Font = IdeTheme.UiFontBold,
            BackColor = IdeTheme.AccentSuccess,
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        btnRun.FlatAppearance.BorderSize = 0;
        btnRun.Click += (_, _) => RunExecutable();

        var btnComp = new Button
        {
            Text = "⚙  ترجمة وبناء المشروع (F6)",
            Dock = DockStyle.Top,
            Height = 38,
            FlatStyle = FlatStyle.Flat,
            Font = IdeTheme.UiFontBold,
            BackColor = IdeTheme.AccentPrimary,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 10, 0, 0)
        };
        btnComp.FlatAppearance.BorderSize = 0;
        btnComp.Click += (_, _) => CompileSource();

        var lblInfo = new Label
        {
            Text = "المعمارية المعتمدة للمترجم:\n• محلل نحوي: LL(1) Doctor Specification\n• كود وسيط: Three-Address Code (TAC)\n• لغة التجميع: x86 Assembly (output.asm)\n• ملف تنفيذي: output.exe عبر ilasm\n\nاضغط F6 للترجمة ثم F5 للتشغيل في الطرفية التفاعلية.",
            Dock = DockStyle.Bottom,
            Height = 180,
            Font = IdeTheme.UiFontRegular,
            ForeColor = IdeTheme.TextSecondary,
            TextAlign = ContentAlignment.TopRight
        };

        runSideBarPanel.Controls.Add(lblInfo);
        runSideBarPanel.Controls.Add(btnRun);
        runSideBarPanel.Controls.Add(btnComp);
    }

    private void BuildArabicEditorWorkspace()
    {
        editorWorkspace.Dock = DockStyle.Fill;
        editorWorkspace.BackColor = IdeTheme.EditorBackground;

        // 1. VS Code Multi-Tab Bar (Right-to-Left tab flow)
        editorTabBar.Dock = DockStyle.Top;
        editorTabBar.Height = 36;
        editorTabBar.Padding = Padding.Empty;
        editorTabBar.BackColor = IdeTheme.SurfaceElevated;
        editorTabBar.RightToLeft = RightToLeft.Yes;

        tabsFlowPanel.Dock = DockStyle.Fill;
        tabsFlowPanel.FlowDirection = FlowDirection.RightToLeft;
        tabsFlowPanel.WrapContents = false;
        tabsFlowPanel.AutoScroll = true;
        tabsFlowPanel.Margin = Padding.Empty;
        tabsFlowPanel.Padding = Padding.Empty;

        btnNewTab.Text = "+";
        btnNewTab.Size = new Size(30, 32);
        btnNewTab.FlatStyle = FlatStyle.Flat;
        btnNewTab.FlatAppearance.BorderSize = 0;
        btnNewTab.Font = IdeTheme.HeaderFont;
        btnNewTab.ForeColor = IdeTheme.TextSecondary;
        btnNewTab.Cursor = Cursors.Hand;
        btnNewTab.Margin = new Padding(2, 2, 2, 2);
        toolTips.SetToolTip(btnNewTab, "تبويب جديد (Ctrl+N)");
        btnNewTab.Click += (_, _) => NewFile();

        editorTabBar.Controls.Add(tabsFlowPanel);
        RenderTabStrip();

        // 2. Breadcrumbs Bar (Arabic aligned right)
        breadcrumbsBar.Dock = DockStyle.Top;
        breadcrumbsBar.Height = 24;
        breadcrumbsBar.Padding = new Padding(12, 2, 12, 2);
        breadcrumbsBar.BackColor = IdeTheme.BreadcrumbsBg;
        breadcrumbsBar.RightToLeft = RightToLeft.Yes;

        lblBreadcrumbs.Text = $"بيئة بيان  ›  عينات  ›  📄 {openDocuments[0].FileName}";
        lblBreadcrumbs.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
        lblBreadcrumbs.ForeColor = IdeTheme.TextMuted;
        lblBreadcrumbs.Dock = DockStyle.Fill;
        lblBreadcrumbs.TextAlign = ContentAlignment.MiddleRight;

        breadcrumbsBar.Controls.Add(lblBreadcrumbs);

        // 3. Main Splitter: Workspace (Top) vs Bottom Panel (Bottom)
        mainSplitter.Dock = DockStyle.Fill;
        mainSplitter.Orientation = Orientation.Horizontal;
        mainSplitter.SplitterWidth = 5;

        // 4. Upper Splitter: Editor on Right (Panel1), Inspector on Left (Panel2)
        upperSplitter.Dock = DockStyle.Fill;
        upperSplitter.Orientation = Orientation.Vertical;
        upperSplitter.RightToLeft = RightToLeft.Yes; // Panel1 is on the Right!
        upperSplitter.SplitterWidth = 5;

        sourceEditor.Dock = DockStyle.Fill;
        upperSplitter.Panel1.Controls.Add(sourceEditor);

        BuildInspectorTabs();
        upperSplitter.Panel2.Controls.Add(inspectorTabs);

        mainSplitter.Panel1.Controls.Add(upperSplitter);

        // 5. Build Bottom Panel
        BuildBottomPanel();
        mainSplitter.Panel2.Controls.Add(bottomTabs);

        // Assembly
        var editorInnerPanel = new Panel { Dock = DockStyle.Fill };
        editorInnerPanel.Controls.Add(mainSplitter);
        editorInnerPanel.Controls.Add(breadcrumbsBar);
        editorInnerPanel.Controls.Add(editorTabBar);

        editorWorkspace.Controls.Add(editorInnerPanel);
    }

    private void RenderTabStrip()
    {
        tabsFlowPanel.Controls.Clear();

        for (int i = 0; i < openDocuments.Count; i++)
        {
            int index = i;
            BayanDocument doc = openDocuments[i];
            bool isActive = (index == activeDocumentIndex);

            var tabPill = new Panel
            {
                Height = 35,
                Width = Math.Max(140, Math.Min(220, doc.FileName.Length * 10 + 50)),
                BackColor = isActive ? IdeTheme.EditorBackground : IdeTheme.SurfaceElevated,
                Margin = new Padding(0, 0, 1, 0),
                Cursor = Cursors.Hand
            };

            var btnClose = new Label
            {
                Text = "✕",
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = IdeTheme.TextMuted,
                Dock = DockStyle.Left,
                Width = 22,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            btnClose.Click += (_, _) => CloseTab(index);

            var lblTitle = new Label
            {
                Text = "📄 " + doc.FileName + (doc.IsModified ? " •" : ""),
                Font = isActive ? IdeTheme.UiFontBold : IdeTheme.UiFontRegular,
                ForeColor = isActive ? IdeTheme.TextPrimary : IdeTheme.TextSecondary,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            lblTitle.Click += (_, _) => SwitchToDocument(index);

            // Active blue indicator top border
            if (isActive)
            {
                tabPill.Paint += (s, e) =>
                {
                    using var pen = new Pen(IdeTheme.EditorTabIndicator, 2);
                    e.Graphics.DrawLine(pen, 0, 0, tabPill.Width, 0);
                };
            }

            tabPill.Controls.Add(lblTitle);
            tabPill.Controls.Add(btnClose);
            tabPill.Click += (_, _) => SwitchToDocument(index);

            tabsFlowPanel.Controls.Add(tabPill);
        }

        tabsFlowPanel.Controls.Add(btnNewTab);
    }

    private void SwitchToDocument(int index)
    {
        if (index < 0 || index >= openDocuments.Count) return;

        // Save active document state
        if (activeDocumentIndex >= 0 && activeDocumentIndex < openDocuments.Count)
        {
            openDocuments[activeDocumentIndex].Content = sourceEditor.SourceCode;
        }

        activeDocumentIndex = index;
        BayanDocument doc = openDocuments[index];
        sourceEditor.SourceCode = doc.Content;

        lblBreadcrumbs.Text = string.IsNullOrEmpty(doc.FilePath)
            ? $"بيئة بيان  ›  {doc.FileName}"
            : $"بيئة بيان  ›  عينات  ›  📄 {doc.FileName}";

        RenderTabStrip();
        statusMessageLabel.Text = $"الملف النشط: {doc.FileName}";
    }

    private void CloseTab(int index)
    {
        if (index < 0 || index >= openDocuments.Count) return;

        if (openDocuments.Count == 1)
        {
            // If closing last tab, reset to default document
            openDocuments[0] = CreateDefaultDocument();
            activeDocumentIndex = 0;
            sourceEditor.SourceCode = openDocuments[0].Content;
            SwitchToDocument(0);
            return;
        }

        openDocuments.RemoveAt(index);
        if (activeDocumentIndex >= openDocuments.Count)
        {
            activeDocumentIndex = openDocuments.Count - 1;
        }
        SwitchToDocument(activeDocumentIndex);
    }

    private void CloseCurrentTab()
    {
        CloseTab(activeDocumentIndex);
    }

    private void BuildInspectorTabs()
    {
        inspectorTabs.Dock = DockStyle.Fill;
        inspectorTabs.Font = IdeTheme.SubHeaderFont;
        inspectorTabs.RightToLeft = RightToLeft.Yes;
        inspectorTabs.ItemSize = new Size(115, 28);
        inspectorTabs.SizeMode = TabSizeMode.Normal;
        inspectorTabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        inspectorTabs.DrawItem += TabControl_DrawItem;

        inspectorTabs.TabPages.Clear();
        inspectorTabs.TabPages.Add(CreateTabPage("🔤 الرموز (Tokens)", txtTokens));
        inspectorTabs.TabPages.Add(CreateTabPage("🌳 شجرة الإعراب (Tree)", txtParseTree));
        inspectorTabs.TabPages.Add(CreateTabPage("📋 جدول الرموز (Symbols)", txtSymbolTable));
        inspectorTabs.TabPages.Add(CreateTabPage("⚡ TAC / IR", txtTac));
        inspectorTabs.TabPages.Add(CreateTabPage("⚙ Assembly x86", txtAssembly));
        inspectorTabs.TabPages.Add(CreateTabPage("🔬 التحليل الدلالي", txtSemantics));
    }

    private void BuildBottomPanel()
    {
        bottomTabs.Dock = DockStyle.Fill;
        bottomTabs.Font = IdeTheme.SubHeaderFont;
        bottomTabs.RightToLeft = RightToLeft.Yes;
        bottomTabs.ItemSize = new Size(160, 28);
        bottomTabs.SizeMode = TabSizeMode.Normal;
        bottomTabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        bottomTabs.DrawItem += TabControl_DrawItem;

        terminalControl.Dock = DockStyle.Fill;
        terminalControl.RerunRequested += (_, _) => RunExecutable();

        bottomTabs.TabPages.Clear();
        bottomTabs.TabPages.Add(CreateTabPage("🖥️ طرفية التشغيل (Terminal)", terminalControl));
        bottomTabs.TabPages.Add(CreateTabPage("⚠️ الأخطاء والتشخيص (Problems)", txtDiagnostics));
        bottomTabs.TabPages.Add(CreateTabPage("📊 تقرير المترجم (Output)", txtPipelineReport));
    }

    private void TabControl_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tc || e.Index < 0 || e.Index >= tc.TabPages.Count)
        {
            return;
        }

        TabPage page = tc.TabPages[e.Index];
        bool isSelected = (tc.SelectedIndex == e.Index);

        Color bg = isSelected ? IdeTheme.EditorBackground : IdeTheme.SurfaceElevated;
        Color fg = isSelected ? IdeTheme.TextPrimary : IdeTheme.TextSecondary;

        using var brushBg = new SolidBrush(bg);
        e.Graphics.FillRectangle(brushBg, e.Bounds);

        if (isSelected)
        {
            using var penIndicator = new Pen(IdeTheme.AccentPrimary, 2);
            e.Graphics.DrawLine(penIndicator, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        using var brushFg = new SolidBrush(fg);
        using var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        e.Graphics.DrawString(page.Text, tc.Font, brushFg, e.Bounds, sf);
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

    private void BuildArabicStatusBar()
    {
        vsCodeStatusBar.Dock = DockStyle.Bottom;
        vsCodeStatusBar.Height = 24;
        vsCodeStatusBar.Padding = new Padding(10, 0, 10, 0);
        vsCodeStatusBar.BackColor = IdeTheme.StatusBar;
        vsCodeStatusBar.ForeColor = IdeTheme.StatusBarForeground;
        vsCodeStatusBar.RightToLeft = RightToLeft.Yes;

        // Right items (Arabic start): Git Branch, Problems
        var rightGroup = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        statusGitLabel.Text = "⎇ main*";
        statusGitLabel.Font = IdeTheme.BadgeFont;
        statusGitLabel.AutoSize = true;
        statusGitLabel.Padding = new Padding(4, 3, 6, 3);
        statusGitLabel.ForeColor = IdeTheme.StatusBarForeground;
        statusGitLabel.Cursor = Cursors.Hand;
        toolTips.SetToolTip(statusGitLabel, "مستودع كود بيان (Git)");

        statusProblemsLabel.Text = "⊗ 0  ⚠ 0";
        statusProblemsLabel.Font = IdeTheme.BadgeFont;
        statusProblemsLabel.AutoSize = true;
        statusProblemsLabel.Padding = new Padding(6, 3, 6, 3);
        statusProblemsLabel.ForeColor = IdeTheme.StatusBarForeground;
        statusProblemsLabel.Cursor = Cursors.Hand;
        toolTips.SetToolTip(statusProblemsLabel, "عرض الأخطاء والتشخيصات (Problems)");
        statusProblemsLabel.Click += (_, _) =>
        {
            EnsureBottomPanelVisible();
            bottomTabs.SelectedIndex = 1;
        };

        rightGroup.Controls.Add(statusGitLabel);
        rightGroup.Controls.Add(statusProblemsLabel);

        // Left items (Arabic end): Position, Encoding, Language, Target
        var leftGroup = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        statusCursorLabel.Text = "السطر 1 ، العمود 1";
        statusCursorLabel.Font = IdeTheme.UiFontRegular;
        statusCursorLabel.AutoSize = true;
        statusCursorLabel.Padding = new Padding(6, 3, 6, 3);
        statusCursorLabel.ForeColor = IdeTheme.StatusBarForeground;

        statusEncodingLabel.Text = "UTF-8";
        statusEncodingLabel.Font = IdeTheme.BadgeFont;
        statusEncodingLabel.AutoSize = true;
        statusEncodingLabel.Padding = new Padding(6, 3, 6, 3);
        statusEncodingLabel.ForeColor = IdeTheme.StatusBarForeground;

        statusLangLabel.Text = "لغة بيان (Bayan)";
        statusLangLabel.Font = IdeTheme.BadgeFont;
        statusLangLabel.AutoSize = true;
        statusLangLabel.Padding = new Padding(6, 3, 6, 3);
        statusLangLabel.ForeColor = IdeTheme.StatusBarForeground;

        statusTargetLabel.Text = "x86 Assembly";
        statusTargetLabel.Font = IdeTheme.BadgeFont;
        statusTargetLabel.AutoSize = true;
        statusTargetLabel.Padding = new Padding(6, 3, 6, 3);
        statusTargetLabel.ForeColor = IdeTheme.StatusBarForeground;

        leftGroup.Controls.Add(statusTargetLabel);
        leftGroup.Controls.Add(statusLangLabel);
        leftGroup.Controls.Add(statusEncodingLabel);
        leftGroup.Controls.Add(statusCursorLabel);

        // Middle: Status Message
        statusMessageLabel.Text = "جاهز — اكتب الكود المصدري ثم اضغط F6 للترجمة أو F5 للتشغيل";
        statusMessageLabel.Dock = DockStyle.Fill;
        statusMessageLabel.Font = IdeTheme.UiFontRegular;
        statusMessageLabel.TextAlign = ContentAlignment.MiddleCenter;
        statusMessageLabel.ForeColor = IdeTheme.StatusBarForeground;

        vsCodeStatusBar.Controls.Add(statusMessageLabel);
        vsCodeStatusBar.Controls.Add(leftGroup);
        vsCodeStatusBar.Controls.Add(rightGroup);
    }

    #endregion

    #region SideBar Navigation & Samples Discovery

    /// <summary>
    /// Walks directory hierarchy upwards to guarantee locating the samples folder.
    /// </summary>
    private static string FindSamplesDirectory()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "samples");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            DirectoryInfo? parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }

        string curDir = Path.Combine(Environment.CurrentDirectory, "samples");
        if (Directory.Exists(curDir))
        {
            return curDir;
        }

        return "samples";
    }

    private void PopulateExplorerTree()
    {
        tvExplorer.Nodes.Clear();

        // 1. Root Node: Open Editors
        var openEditorsNode = new TreeNode("▾ المحررات المفتوحة (المستندات)")
        {
            NodeFont = IdeTheme.UiFontBold
        };
        for (int i = 0; i < openDocuments.Count; i++)
        {
            var docNode = new TreeNode("📄 " + openDocuments[i].FileName)
            {
                Tag = i
            };
            openEditorsNode.Nodes.Add(docNode);
        }
        tvExplorer.Nodes.Add(openEditorsNode);
        openEditorsNode.Expand();

        // 2. Root Node: Official Samples
        var samplesNode = new TreeNode("▾ عَيِّنَات لغة بيان الرسمية (samples)")
        {
            NodeFont = IdeTheme.UiFontBold
        };

        string samplesDir = FindSamplesDirectory();
        if (Directory.Exists(samplesDir))
        {
            string[] files = Directory.GetFiles(samplesDir, "*.bayan");
            Array.Sort(files);
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                var fileNode = new TreeNode("📄 " + fileName)
                {
                    Tag = file
                };
                samplesNode.Nodes.Add(fileNode);
            }
        }
        tvExplorer.Nodes.Add(samplesNode);
        samplesNode.Expand();

        // 3. Root Node: Compiler Stages
        var stagesNode = new TreeNode("▾ مراحل المترجم التسع الرسمية")
        {
            NodeFont = IdeTheme.UiFontBold
        };
        stagesNode.Nodes.Add(new TreeNode(" [01] 📝 محرر الكود المصدري") { Tag = 101 });
        stagesNode.Nodes.Add(new TreeNode(" [02] 🔤 الرموز المعجمية (Tokens)") { Tag = 102 });
        stagesNode.Nodes.Add(new TreeNode(" [03] 🌳 شجرة الإعراب (Parse Tree)") { Tag = 103 });
        stagesNode.Nodes.Add(new TreeNode(" [04] 📋 جدول الرموز الدلالي") { Tag = 104 });
        stagesNode.Nodes.Add(new TreeNode(" [05] 🔬 فحص الأنواع (Semantics)") { Tag = 105 });
        stagesNode.Nodes.Add(new TreeNode(" [06] ⚡ الكود الوسيط (TAC / IR)") { Tag = 106 });
        stagesNode.Nodes.Add(new TreeNode(" [07] ⚙ لغة التجميع (x86 Assembly)") { Tag = 107 });
        stagesNode.Nodes.Add(new TreeNode(" [08] ⚠️ الأخطاء والتشخيصات") { Tag = 108 });
        stagesNode.Nodes.Add(new TreeNode(" [09] 🖥️ الطرفية والتنفيذ (output.exe)") { Tag = 109 });
        tvExplorer.Nodes.Add(stagesNode);
        stagesNode.Expand();
    }

    private void PopulateQuickSamplesDropdown()
    {
        cmbQuickSamples.Items.Clear();
        cmbQuickSamples.Items.Add("— اختر عينة رسمية للتجربة —");

        string samplesDir = FindSamplesDirectory();
        if (Directory.Exists(samplesDir))
        {
            string[] files = Directory.GetFiles(samplesDir, "*.bayan");
            Array.Sort(files);
            foreach (string file in files)
            {
                cmbQuickSamples.Items.Add(Path.GetFileName(file));
            }
        }

        if (cmbQuickSamples.Items.Count > 0)
        {
            cmbQuickSamples.SelectedIndex = 0;
        }
    }

    private void CmbQuickSamples_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (cmbQuickSamples.SelectedIndex <= 0) return;

        string selectedFileName = cmbQuickSamples.SelectedItem?.ToString() ?? "";
        if (string.IsNullOrEmpty(selectedFileName)) return;

        string samplesDir = FindSamplesDirectory();
        string fullPath = Path.Combine(samplesDir, selectedFileName);
        if (File.Exists(fullPath))
        {
            LoadFileIntoNewTab(fullPath);
        }
    }

    private void TvExplorer_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node == null) return;

        // If clicking an open document item
        if (e.Node.Tag is int docIdx && docIdx >= 0 && docIdx < openDocuments.Count)
        {
            SwitchToDocument(docIdx);
            return;
        }

        // If clicking a sample file
        if (e.Node.Tag is string filePath && File.Exists(filePath))
        {
            LoadFileIntoNewTab(filePath);
            return;
        }

        // If clicking a stage item
        if (e.Node.Tag is int stageCode && stageCode >= 101 && stageCode <= 109)
        {
            NavigateToStage(stageCode - 101);
        }
    }

    private void NavigateToStage(int stageIndex)
    {
        switch (stageIndex)
        {
            case 0: // Source
                sourceEditor.Focus();
                break;
            case 1: // Tokens
                EnsureInspectorVisible();
                inspectorTabs.SelectedIndex = 0;
                break;
            case 2: // Tree
                EnsureInspectorVisible();
                inspectorTabs.SelectedIndex = 1;
                break;
            case 3: // Symbols
                EnsureInspectorVisible();
                inspectorTabs.SelectedIndex = 2;
                break;
            case 4: // Semantics
                EnsureInspectorVisible();
                inspectorTabs.SelectedIndex = 5;
                break;
            case 5: // TAC
                EnsureInspectorVisible();
                inspectorTabs.SelectedIndex = 3;
                break;
            case 6: // Assembly
                EnsureInspectorVisible();
                inspectorTabs.SelectedIndex = 4;
                break;
            case 7: // Problems
                EnsureBottomPanelVisible();
                bottomTabs.SelectedIndex = 1;
                break;
            case 8: // Terminal
                EnsureBottomPanelVisible();
                bottomTabs.SelectedIndex = 0;
                break;
        }
    }

    private void LstStages_SelectedIndexChanged(object? sender, EventArgs e)
    {
        int index = lstStages.SelectedIndex;
        if (index >= 0)
        {
            NavigateToStage(index);
        }
    }

    private void SwitchSideBarView(string viewName)
    {
        if (activeSideBarView == viewName && isSideBarOpen)
        {
            ToggleSideBar();
            return;
        }

        isSideBarOpen = true;
        sideBar.Visible = true;
        activeSideBarView = viewName;

        sideBarContent.Controls.Clear();
        switch (viewName)
        {
            case "EXPLORER":
                lblSideBarTitle.Text = "المستكشف : بيئة بيان";
                sideBarContent.Controls.Add(tvExplorer);
                break;
            case "STAGES":
                lblSideBarTitle.Text = "مراحل المترجم التسع";
                sideBarContent.Controls.Add(lstStages);
                break;
            case "RUN":
                lblSideBarTitle.Text = "التشغيل والتصحيح";
                sideBarContent.Controls.Add(runSideBarPanel);
                break;
        }

        HighlightActiveActivityButton();
    }

    private void HighlightActiveActivityButton()
    {
        btnActExplorer.BackColor = (isSideBarOpen && activeSideBarView == "EXPLORER") ? IdeTheme.SurfaceHover : IdeTheme.ActivityBar;
        btnActStages.BackColor = (isSideBarOpen && activeSideBarView == "STAGES") ? IdeTheme.SurfaceHover : IdeTheme.ActivityBar;
        btnActRun.BackColor = (isSideBarOpen && activeSideBarView == "RUN") ? IdeTheme.SurfaceHover : IdeTheme.ActivityBar;
        activityBar.Invalidate(true);
    }

    private void ToggleSideBar()
    {
        isSideBarOpen = !isSideBarOpen;
        sideBar.Visible = isSideBarOpen;
        HighlightActiveActivityButton();
    }

    private void ToggleBottomPanel()
    {
        mainSplitter.Panel2Collapsed = !mainSplitter.Panel2Collapsed;
        btnToggleBottomPanel.Text = mainSplitter.Panel2Collapsed ? "▤ إظهار الطرفية" : "▤ إخفاء الطرفية";
    }

    private void EnsureBottomPanelVisible()
    {
        if (mainSplitter.Panel2Collapsed)
        {
            mainSplitter.Panel2Collapsed = false;
            btnToggleBottomPanel.Text = "▤ إخفاء الطرفية";
        }
    }

    private void ToggleInspectorView()
    {
        isInspectorOpen = !isInspectorOpen;
        btnToggleInspector.Text = isInspectorOpen ? "◫ فاحص المراحل" : "🗖 محرر كامل";
        upperSplitter.Panel2Collapsed = !isInspectorOpen;
        if (isInspectorOpen)
        {
            ApplyProportionalSplitters();
        }
    }

    private void EnsureInspectorVisible()
    {
        if (!isInspectorOpen)
        {
            ToggleInspectorView();
        }
    }

    #endregion

    #region Compilation & Execution

    private async void CompileSource()
    {
        if (isBusy) return;

        // Save current editor content into active document
        if (activeDocumentIndex >= 0 && activeDocumentIndex < openDocuments.Count)
        {
            openDocuments[activeDocumentIndex].Content = sourceEditor.SourceCode;
        }

        SetBusy(true, "يجري تشغيل مترجم بيان المستقل والتحقق من القواعد...");
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
            btnQuickRun.Enabled = succeeded && !string.IsNullOrWhiteSpace(lastExecutablePath) && File.Exists(lastExecutablePath);

            // Update error counters
            if (!succeeded)
            {
                errorCount = Regex.Matches(diagnostics, @"(?:خطأ|Error)", RegexOptions.IgnoreCase).Count;
                if (errorCount == 0) errorCount = 1;
            }
            else
            {
                errorCount = 0;
                warningCount = 0;
            }
            statusProblemsLabel.Text = $"⊗ {errorCount}  ⚠ {warningCount}";

            // Update Pipeline report
            txtPipelineReport.Text =
                $"=== نتيجة معالجة مترجم بيان ===\n\n" +
                $"• الحالة: {(succeeded ? "نجاح الترجمة الكاملة ✔" : "توقفت الترجمة لوجود أخطاء ✖")}\n" +
                $"• آخر مرحلة مكتملة: {run.Manifest?.CompletedStage ?? "غير محدد"}\n" +
                $"• توفر ملف التنفيذ (output.exe): {(btnQuickRun.Enabled ? "نعم — جاهز للتشغيل في الطرفية" : "لا")}\n" +
                $"• مسار الملف التنفيذي: {lastExecutablePath}\n" +
                $"• رمز الخروج للـ CLI: {run.ExitCode}\n\n" +
                $"التفاصيل:\n{run.StandardOutput}\n{run.StandardError}";

            if (succeeded)
            {
                statusMessageLabel.Text = $"اكتملت الترجمة بنجاح! تم بناء output.exe في: {run.Manifest?.CompletedStage}";
                inspectorTabs.SelectedIndex = 4; // Show Assembly tab
            }
            else
            {
                statusMessageLabel.Text = $"توقفت الترجمة عند مرحلة: {run.Manifest?.CompletedStage ?? "الأخطاء"}";
                EnsureBottomPanelVisible();
                bottomTabs.SelectedIndex = 1; // Show Problems tab
            }
        }
        catch (Exception ex)
        {
            txtDiagnostics.Text = "[خطأ في بيئة تشغيل المحرر] " + ex.Message;
            statusMessageLabel.Text = "فشل تشغيل المترجم: " + ex.Message;
            errorCount = 1;
            statusProblemsLabel.Text = $"⊗ {errorCount}  ⚠ {warningCount}";
            EnsureBottomPanelVisible();
            bottomTabs.SelectedIndex = 1;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void RunExecutable()
    {
        if (isBusy) return;

        if (string.IsNullOrWhiteSpace(lastExecutablePath) || !File.Exists(lastExecutablePath))
        {
            MessageBox.Show(this, "يرجى تشغيل «ترجمة ⚙ (F6)» أولاً لبناء ملف output.exe.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        EnsureBottomPanelVisible();
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
        }
        catch (TimeoutException)
        {
            stopwatch.Stop();
            terminalControl.SetResult("", "[تجاوز وقت التنفيذ] استغرق البرنامج وقتاً أطول من المسموح.", -1, stopwatch.Elapsed);
            statusMessageLabel.Text = "تجاوز البرنامج الحد الزمني المسموح.";
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            terminalControl.SetResult("", "[خطأ تشغيل] " + ex.Message, -1, stopwatch.Elapsed);
            statusMessageLabel.Text = "تعذر تشغيل الملف التنفيذي.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    #endregion

    #region Diagnostics Navigation & Editor Actions

    private void TxtDiagnostics_DoubleClick(object? sender, EventArgs e)
    {
        string currentLine = txtDiagnostics.SelectedText;
        if (string.IsNullOrWhiteSpace(currentLine))
        {
            int lineIndex = txtDiagnostics.GetLineFromCharIndex(txtDiagnostics.SelectionStart);
            if (lineIndex >= 0 && lineIndex < txtDiagnostics.Lines.Length)
            {
                currentLine = txtDiagnostics.Lines[lineIndex];
            }
        }

        if (string.IsNullOrWhiteSpace(currentLine)) return;

        var match = Regex.Match(currentLine, @"(?:السطر|Line)\s*:?\s*(\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int targetLine))
        {
            JumpToEditorLine(targetLine);
        }
    }

    private void JumpToEditorLine(int targetLine)
    {
        if (targetLine <= 0) return;

        var rtb = sourceEditor.InnerEditor;
        if (rtb.Lines.Length == 0) return;

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

    private void NewFile()
    {
        int newNum = openDocuments.Count + 1;
        var newDoc = new BayanDocument
        {
            FileName = $"برنامج_{newNum}.bayan",
            FilePath = string.Empty,
            Content =
                "برنامج جديد;\n" +
                "متغير س : صحيح;\n" +
                "{\n" +
                "    س = 100;\n" +
                "    اطبع(س);\n" +
                "}."
        };
        openDocuments.Add(newDoc);
        SwitchToDocument(openDocuments.Count - 1);
        PopulateExplorerTree();
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
            LoadFileIntoNewTab(dialog.FileName);
        }
    }

    private void LoadFileIntoNewTab(string path)
    {
        try
        {
            for (int i = 0; i < openDocuments.Count; i++)
            {
                if (string.Equals(openDocuments[i].FilePath, path, StringComparison.OrdinalIgnoreCase))
                {
                    SwitchToDocument(i);
                    return;
                }
            }

            string content = File.ReadAllText(path);
            var doc = new BayanDocument
            {
                FileName = Path.GetFileName(path),
                FilePath = path,
                Content = content
            };
            openDocuments.Add(doc);
            SwitchToDocument(openDocuments.Count - 1);
            PopulateExplorerTree();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر قراءة الملف: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveCurrentDocument()
    {
        if (activeDocumentIndex < 0 || activeDocumentIndex >= openDocuments.Count) return;
        var doc = openDocuments[activeDocumentIndex];
        doc.Content = sourceEditor.SourceCode;

        if (!string.IsNullOrEmpty(doc.FilePath))
        {
            File.WriteAllText(doc.FilePath, doc.Content);
            doc.IsModified = false;
            RenderTabStrip();
            statusMessageLabel.Text = $"تم حفظ الملف: {doc.FileName}";
            return;
        }

        SaveAsDialog();
    }

    private void SaveAsDialog()
    {
        if (activeDocumentIndex < 0 || activeDocumentIndex >= openDocuments.Count) return;
        var doc = openDocuments[activeDocumentIndex];
        doc.Content = sourceEditor.SourceCode;

        using var dialog = new SaveFileDialog
        {
            Filter = "Bayan source files (*.bayan)|*.bayan|All files (*.*)|*.*",
            DefaultExt = "bayan",
            FileName = doc.FileName,
            Title = "حفظ برنامج بيان"
        };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            File.WriteAllText(dialog.FileName, doc.Content);
            doc.FilePath = dialog.FileName;
            doc.FileName = Path.GetFileName(dialog.FileName);
            doc.IsModified = false;
            RenderTabStrip();
            PopulateExplorerTree();
            statusMessageLabel.Text = $"تم حفظ الملف: {doc.FileName}";
        }
    }

    private void ToggleTheme()
    {
        IdeTheme.IsDarkMode = !IdeTheme.IsDarkMode;
        btnActTheme.Text = IdeTheme.IsDarkMode ? "🌙" : "☀️";
        ApplyActiveTheme();
    }

    private void ApplyActiveTheme()
    {
        BackColor = IdeTheme.AppBackground;
        ForeColor = IdeTheme.TextPrimary;

        vsCodeMenuBar.BackColor = IdeTheme.TitleBar;
        vsCodeMenuBar.ForeColor = IdeTheme.TextPrimary;

        commandCenterBar.BackColor = IdeTheme.TitleBar;
        activityBar.BackColor = IdeTheme.ActivityBar;
        sideBar.BackColor = IdeTheme.SideBar;
        sideBarHeader.BackColor = IdeTheme.SideBarHeader;
        editorTabBar.BackColor = IdeTheme.SurfaceElevated;
        breadcrumbsBar.BackColor = IdeTheme.BreadcrumbsBg;

        tvExplorer.BackColor = IdeTheme.SideBar;
        tvExplorer.ForeColor = IdeTheme.TextPrimary;

        lstStages.BackColor = IdeTheme.SideBar;
        lstStages.ForeColor = IdeTheme.TextPrimary;

        runSideBarPanel.BackColor = IdeTheme.SideBar;

        sourceEditor.ApplyTheme();
        terminalControl.ApplyTheme();

        // Monospace Viewers
        Color viewBg = IdeTheme.EditorBackground;
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

        // Splitter colors
        mainSplitter.BackColor = IdeTheme.Border;
        upperSplitter.BackColor = IdeTheme.Border;

        // Status bar
        vsCodeStatusBar.BackColor = IdeTheme.StatusBar;
        vsCodeStatusBar.ForeColor = IdeTheme.StatusBarForeground;

        HighlightActiveActivityButton();
        RenderTabStrip();
        inspectorTabs.Invalidate();
        bottomTabs.Invalidate();
    }

    private void SetBusy(bool busy, string message = "")
    {
        isBusy = busy;
        compileProgress.Visible = busy;
        UseWaitCursor = busy;

        btnQuickCompile.Enabled = !busy;
        btnQuickRun.Enabled = !busy && !string.IsNullOrWhiteSpace(lastExecutablePath) && File.Exists(lastExecutablePath);

        if (!string.IsNullOrEmpty(message))
        {
            statusMessageLabel.Text = message;
        }
    }

    private void ApplyProportionalSplitters()
    {
        try
        {
            if (mainSplitter.Height > 300 && !mainSplitter.Panel2Collapsed)
            {
                int targetHeight = (int)(mainSplitter.Height * 0.65);
                int min1 = Math.Max(40, mainSplitter.Panel1MinSize);
                int min2 = Math.Max(40, mainSplitter.Panel2MinSize);
                if (targetHeight >= min1 && targetHeight <= (mainSplitter.Height - min2))
                {
                    mainSplitter.SplitterDistance = targetHeight;
                }
            }

            if (upperSplitter.Width > 400 && isInspectorOpen)
            {
                // In RTL, Panel1 is on the Right (Editor: 60%), Panel2 is on the Left (Inspector: 40%)
                int targetWidth = (int)(upperSplitter.Width * 0.60);
                int min1 = Math.Max(40, upperSplitter.Panel1MinSize);
                int min2 = Math.Max(40, upperSplitter.Panel2MinSize);
                if (targetWidth >= min1 && targetWidth <= (upperSplitter.Width - min2))
                {
                    upperSplitter.SplitterDistance = targetWidth;
                }
            }
        }
        catch
        {
            // Calculation guard on rapid resize
        }
    }

    private void ConfigureActionBtn(Button btn, string text, string tooltip, EventHandler onClick, bool isPrimary = false, Color? primaryColor = null)
    {
        btn.Text = text;
        btn.AutoSize = true;
        btn.MinimumSize = new Size(70, 30);
        btn.Height = 30;
        btn.FlatStyle = FlatStyle.Flat;
        btn.Font = isPrimary ? IdeTheme.UiFontBold : IdeTheme.UiFontRegular;
        btn.Cursor = Cursors.Hand;
        btn.Margin = new Padding(3, 2, 3, 2);
        btn.Padding = new Padding(10, 2, 10, 2);
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

    private void ShowAboutDialog()
    {
        MessageBox.Show(
            this,
            "بيئة التطوير المتكاملة للغة بيان (Bayan IDE)\n" +
            "الإصدار: 2.0 — مواصفات أستاذ المقرر (Doctor Specification)\n\n" +
            "المميزات:\n" +
            "• معمارية عربية أصيلة تحاكي Visual Studio Code من اليمين إلى اليسار\n" +
            "• محلل نحوي رسمي LL(1) Parsing Table\n" +
            "• فحص دلالي وجدول رموز دقيق (Symbol Table)\n" +
            "• توليد كود وسيط Three-Address Code (TAC)\n" +
            "• توليد كود تجميع حقيقي x86 Assembly + MIPS Stack Frames\n" +
            "• بناء ملف تنفيذي output.exe وتشغيله تفاعلياً مع تعليمة اقرأ",
            "حول محرر لغة بيان",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ShowGrammarGuide()
    {
        MessageBox.Show(
            this,
            "قواعد لغة بيان الرسمية:\n\n" +
            "1. البرنامج يبدأ بـ: برنامج <اسم>;\n" +
            "2. التصريح عن المتغيرات: متغير <س> : <صحيح | حقيقي | حرف | خيط_رمزي | منطق>;\n" +
            "3. جسم البرنامج بين أقواس { ... }.\n" +
            "4. الطباعة: اطبع(<تعبير>);\n" +
            "5. القراءة: اقرأ(<متغير>);\n" +
            "6. التحكم: إذا <شرط> { ... } إلا { ... }\n" +
            "7. الحلقات: طالما <شرط> { ... } ، كرر { ... } حتى <شرط>\n" +
            "8. الإجراءات: إجراء <اسم>(معاملات) { ... }",
            "دليل لغة بيان",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
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
            SaveCurrentDocument();
        }
        else if (e.Control && e.KeyCode == Keys.W)
        {
            e.SuppressKeyPress = true;
            CloseCurrentTab();
        }
        else if (e.Control && e.KeyCode == Keys.B)
        {
            e.SuppressKeyPress = true;
            ToggleSideBar();
        }
        else if (e.Control && e.KeyCode == Keys.J)
        {
            e.SuppressKeyPress = true;
            ToggleBottomPanel();
        }
        else if (e.Control && e.KeyCode == Keys.Oem5) // Ctrl+\
        {
            e.SuppressKeyPress = true;
            ToggleInspectorView();
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
        else if (e.KeyCode == Keys.F7)
        {
            e.SuppressKeyPress = true;
            ToggleTheme();
        }
    }

    #endregion
}
