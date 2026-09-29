using System.Text;

namespace BayanCompiler.Forms;

/// <summary>
/// A modern, interactive tree visualizer control supporting both:
/// 1. Interactive Visual TreeView (expand/collapse nodes, search, color coding)
/// 2. Connected Hierarchical Diagram (Unicode tree connectors: ├──, └──, │)
/// Designed for both Concrete Parse Tree (CST) and Abstract Syntax Tree (AST).
/// </summary>
public sealed class BayanTreeViewerControl : UserControl
{
    private readonly Panel headerPanel = new();
    private readonly Label lblTitle = new();
    private readonly Label lblBadge = new();
    private readonly Button btnExpandAll = new();
    private readonly Button btnCollapseAll = new();
    private readonly Button btnToggleMode = new();
    private readonly TextBox txtSearch = new();
    private readonly Label lblSearchCount = new();
    private readonly Button btnNextMatch = new();

    private readonly Panel contentContainer = new();
    private readonly TreeView treeView = new();
    private readonly RichTextBox txtConnectorView = new();

    private bool isConnectorMode = false;
    private readonly List<TreeNode> searchMatches = new();
    private int currentMatchIndex = -1;
    private string rawTreeData = string.Empty;
    private string treeTitle = string.Empty;

    public BayanTreeViewerControl()
    {
        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        Font = IdeTheme.UiFontRegular;

        InitializeComponents();
        ApplyTheme();
    }

    private void InitializeComponents()
    {
        // 1. Header Toolbar
        headerPanel.Dock = DockStyle.Top;
        headerPanel.Height = 38;
        headerPanel.Padding = new Padding(8, 4, 8, 4);
        headerPanel.RightToLeft = RightToLeft.Yes;

        // Flow container for controls from right to left
        var toolbarFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = false,
            AutoScroll = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        // Title
        lblTitle.Text = "شجرة التحليل النحوي";
        lblTitle.Font = IdeTheme.SubHeaderFont;
        lblTitle.AutoSize = true;
        lblTitle.Margin = new Padding(0, 6, 8, 0);

        // Node Count Badge
        lblBadge.Text = "0 عقدة";
        lblBadge.Font = IdeTheme.BadgeFont;
        lblBadge.AutoSize = true;
        lblBadge.Padding = new Padding(8, 3, 8, 3);
        lblBadge.Margin = new Padding(0, 5, 12, 0);

        // Expand All Button
        btnExpandAll.Text = "➕ توسيع الكل";
        btnExpandAll.Font = IdeTheme.BadgeFont;
        btnExpandAll.AutoSize = true;
        btnExpandAll.Height = 26;
        btnExpandAll.FlatStyle = FlatStyle.Flat;
        btnExpandAll.FlatAppearance.BorderSize = 1;
        btnExpandAll.Cursor = Cursors.Hand;
        btnExpandAll.Margin = new Padding(0, 2, 6, 0);
        btnExpandAll.Click += (_, _) =>
        {
            treeView.BeginUpdate();
            treeView.ExpandAll();
            treeView.EndUpdate();
        };

        // Collapse All Button
        btnCollapseAll.Text = "➖ طي الكل";
        btnCollapseAll.Font = IdeTheme.BadgeFont;
        btnCollapseAll.AutoSize = true;
        btnCollapseAll.Height = 26;
        btnCollapseAll.FlatStyle = FlatStyle.Flat;
        btnCollapseAll.FlatAppearance.BorderSize = 1;
        btnCollapseAll.Cursor = Cursors.Hand;
        btnCollapseAll.Margin = new Padding(0, 2, 6, 0);
        btnCollapseAll.Click += (_, _) =>
        {
            treeView.BeginUpdate();
            treeView.CollapseAll();
            if (treeView.Nodes.Count > 0)
            {
                foreach (TreeNode root in treeView.Nodes)
                {
                    root.Expand();
                }
            }
            treeView.EndUpdate();
        };

        // Toggle View Mode Button
        btnToggleMode.Text = "📜 رسم متصل";
        btnToggleMode.Font = IdeTheme.BadgeFont;
        btnToggleMode.AutoSize = true;
        btnToggleMode.Height = 26;
        btnToggleMode.FlatStyle = FlatStyle.Flat;
        btnToggleMode.FlatAppearance.BorderSize = 1;
        btnToggleMode.Cursor = Cursors.Hand;
        btnToggleMode.Margin = new Padding(0, 2, 12, 0);
        btnToggleMode.Click += (_, _) => ToggleMode();

        // Search Box
        txtSearch.Width = 140;
        txtSearch.Height = 24;
        txtSearch.Font = IdeTheme.BadgeFont;
        txtSearch.Margin = new Padding(0, 4, 4, 0);
        txtSearch.PlaceholderText = "🔍 بحث في العقد...";
        txtSearch.TextChanged += (_, _) => PerformSearch(txtSearch.Text);
        txtSearch.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                JumpToNextMatch();
            }
        };

        // Next Match Button
        btnNextMatch.Text = "▼";
        btnNextMatch.Width = 26;
        btnNextMatch.Height = 24;
        btnNextMatch.Font = IdeTheme.BadgeFont;
        btnNextMatch.FlatStyle = FlatStyle.Flat;
        btnNextMatch.FlatAppearance.BorderSize = 1;
        btnNextMatch.Cursor = Cursors.Hand;
        btnNextMatch.Margin = new Padding(0, 4, 4, 0);
        btnNextMatch.Click += (_, _) => JumpToNextMatch();

        // Search count label
        lblSearchCount.Text = "";
        lblSearchCount.Font = IdeTheme.BadgeFont;
        lblSearchCount.AutoSize = true;
        lblSearchCount.Margin = new Padding(0, 7, 0, 0);

        toolbarFlow.Controls.Add(lblTitle);
        toolbarFlow.Controls.Add(lblBadge);
        toolbarFlow.Controls.Add(btnExpandAll);
        toolbarFlow.Controls.Add(btnCollapseAll);
        toolbarFlow.Controls.Add(btnToggleMode);
        toolbarFlow.Controls.Add(txtSearch);
        toolbarFlow.Controls.Add(btnNextMatch);
        toolbarFlow.Controls.Add(lblSearchCount);

        headerPanel.Controls.Add(toolbarFlow);

        // 2. Content Container
        contentContainer.Dock = DockStyle.Fill;

        // TreeView configuration
        treeView.Dock = DockStyle.Fill;
        treeView.BorderStyle = BorderStyle.None;
        treeView.ShowPlusMinus = true;
        treeView.ShowLines = true;
        treeView.ShowRootLines = true;
        treeView.HideSelection = false;
        treeView.Font = IdeTheme.ViewerCodeFont;
        // Keeping LTR on the TreeView control prevents Windows Forms glyph-clipping bugs in RTL
        treeView.RightToLeft = RightToLeft.No;
        treeView.ItemHeight = 24;

        // Connector Text Box configuration
        txtConnectorView.Dock = DockStyle.Fill;
        txtConnectorView.BorderStyle = BorderStyle.None;
        txtConnectorView.ReadOnly = true;
        txtConnectorView.WordWrap = false;
        txtConnectorView.Font = IdeTheme.ViewerCodeFont;
        txtConnectorView.ScrollBars = RichTextBoxScrollBars.Both;
        txtConnectorView.RightToLeft = RightToLeft.No;
        txtConnectorView.Visible = false;

        contentContainer.Controls.Add(treeView);
        contentContainer.Controls.Add(txtConnectorView);

        Controls.Add(contentContainer);
        Controls.Add(headerPanel);
    }

    /// <summary>
    /// Loads indented tree data, parses into nodes, and populates both views.
    /// </summary>
    public void LoadTree(string rawText, string title)
    {
        rawTreeData = rawText ?? string.Empty;
        treeTitle = title ?? "الشجرة";
        lblTitle.Text = treeTitle;

        searchMatches.Clear();
        currentMatchIndex = -1;
        lblSearchCount.Text = "";
        txtSearch.Clear();

        treeView.BeginUpdate();
        treeView.Nodes.Clear();

        if (string.IsNullOrWhiteSpace(rawTreeData))
        {
            lblBadge.Text = "0 عقدة";
            txtConnectorView.Text = "ستظهر هنا الشجرة بعد اكتمال مرحلة التحليل بنجاح.";
            treeView.EndUpdate();
            return;
        }

        var lines = rawTreeData.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var stack = new Stack<(TreeNode node, int indent)>();
        int totalNodes = 0;

        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine)) continue;

            int indent = 0;
            while (indent < rawLine.Length && (rawLine[indent] == ' ' || rawLine[indent] == '\t'))
            {
                if (rawLine[indent] == '\t') indent += 4;
                else indent += 1;
            }

            string text = rawLine.Trim();
            var node = CreateStyledNode(text);
            totalNodes++;

            while (stack.Count > 0 && stack.Peek().indent >= indent)
            {
                stack.Pop();
            }

            if (stack.Count == 0)
            {
                treeView.Nodes.Add(node);
            }
            else
            {
                stack.Peek().node.Nodes.Add(node);
            }

            stack.Push((node, indent));
        }

        // Expand top 2 levels by default so user gets immediate visual depth
        if (treeView.Nodes.Count > 0)
        {
            foreach (TreeNode root in treeView.Nodes)
            {
                root.Expand();
                foreach (TreeNode child in root.Nodes)
                {
                    child.Expand();
                }
            }
        }

        treeView.EndUpdate();

        lblBadge.Text = $"{totalNodes} عقدة";

        // Build the connected graphical text
        txtConnectorView.Text = BuildConnectorText(treeView.Nodes);
    }

    private static TreeNode CreateStyledNode(string text)
    {
        var node = new TreeNode(text);

        // Distinguish terminals/tokens from non-terminal rules and AST nodes
        if (text.Contains("→") || text.Contains("->"))
        {
            node.ForeColor = IdeTheme.IsDarkMode ? Color.FromArgb(152, 195, 121) : Color.FromArgb(22, 101, 52); // Green (Terminal Token)
        }
        else if (text.Contains("Node:") || text.Contains("Declaration") || text.Contains("Expression"))
        {
            node.ForeColor = IdeTheme.IsDarkMode ? Color.FromArgb(97, 175, 239) : Color.FromArgb(29, 78, 216); // Blue (AST Node)
        }
        else if (text.EndsWith(":"))
        {
            node.ForeColor = IdeTheme.IsDarkMode ? Color.FromArgb(229, 192, 123) : Color.FromArgb(161, 98, 7); // Gold / Section
        }
        else
        {
            node.ForeColor = IdeTheme.IsDarkMode ? Color.FromArgb(209, 154, 102) : Color.FromArgb(194, 65, 12); // Orange / Grammar Rule
        }

        return node;
    }

    private static string BuildConnectorText(TreeNodeCollection nodes)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < nodes.Count; i++)
        {
            bool isLast = (i == nodes.Count - 1);
            AppendConnectorNode(nodes[i], sb, "", isLast);
        }
        return sb.ToString();
    }

    private static void AppendConnectorNode(TreeNode node, StringBuilder sb, string indent, bool isLast)
    {
        sb.Append(indent);
        sb.Append(isLast ? "└── " : "├── ");
        sb.AppendLine(node.Text);

        for (int i = 0; i < node.Nodes.Count; i++)
        {
            bool childLast = (i == node.Nodes.Count - 1);
            string childIndent = indent + (isLast ? "    " : "│   ");
            AppendConnectorNode(node.Nodes[i], sb, childIndent, childLast);
        }
    }

    private void ToggleMode()
    {
        isConnectorMode = !isConnectorMode;
        if (isConnectorMode)
        {
            treeView.Visible = false;
            txtConnectorView.Visible = true;
            btnToggleMode.Text = "🌳 شجرة تفاعلية";
            btnExpandAll.Enabled = false;
            btnCollapseAll.Enabled = false;
        }
        else
        {
            txtConnectorView.Visible = false;
            treeView.Visible = true;
            btnToggleMode.Text = "📜 رسم متصل";
            btnExpandAll.Enabled = true;
            btnCollapseAll.Enabled = true;
        }
    }

    private void PerformSearch(string query)
    {
        // Clear previous highlights
        ResetNodeColors(treeView.Nodes);
        searchMatches.Clear();
        currentMatchIndex = -1;

        if (string.IsNullOrWhiteSpace(query))
        {
            lblSearchCount.Text = "";
            return;
        }

        FindMatchingNodes(treeView.Nodes, query.Trim());

        if (searchMatches.Count > 0)
        {
            lblSearchCount.Text = $"{searchMatches.Count} نتيجة";
            lblSearchCount.ForeColor = IdeTheme.AccentWarning;
            JumpToNextMatch();
        }
        else
        {
            lblSearchCount.Text = "لا نتائج";
            lblSearchCount.ForeColor = IdeTheme.AccentError;
        }
    }

    private void FindMatchingNodes(TreeNodeCollection nodes, string query)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                node.BackColor = Color.FromArgb(204, 167, 0); // Yellow highlight
                node.ForeColor = Color.Black;
                searchMatches.Add(node);

                // Ensure parent nodes are expanded so the match is visible
                TreeNode? parent = node.Parent;
                while (parent != null)
                {
                    parent.Expand();
                    parent = parent.Parent;
                }
            }

            if (node.Nodes.Count > 0)
            {
                FindMatchingNodes(node.Nodes, query);
            }
        }
    }

    private void JumpToNextMatch()
    {
        if (searchMatches.Count == 0) return;

        currentMatchIndex = (currentMatchIndex + 1) % searchMatches.Count;
        TreeNode match = searchMatches[currentMatchIndex];
        treeView.SelectedNode = match;
        match.EnsureVisible();
        lblSearchCount.Text = $"{currentMatchIndex + 1}/{searchMatches.Count}";
    }

    private static void ResetNodeColors(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            node.BackColor = Color.Empty;
            // Restore token / rule colors
            if (node.Text.Contains("→") || node.Text.Contains("->"))
            {
                node.ForeColor = IdeTheme.IsDarkMode ? Color.FromArgb(152, 195, 121) : Color.FromArgb(22, 101, 52);
            }
            else if (node.Text.Contains("Node:") || node.Text.Contains("Declaration") || node.Text.Contains("Expression"))
            {
                node.ForeColor = IdeTheme.IsDarkMode ? Color.FromArgb(97, 175, 239) : Color.FromArgb(29, 78, 216);
            }
            else if (node.Text.EndsWith(":"))
            {
                node.ForeColor = IdeTheme.IsDarkMode ? Color.FromArgb(229, 192, 123) : Color.FromArgb(161, 98, 7);
            }
            else
            {
                node.ForeColor = IdeTheme.IsDarkMode ? Color.FromArgb(209, 154, 102) : Color.FromArgb(194, 65, 12);
            }

            if (node.Nodes.Count > 0)
            {
                ResetNodeColors(node.Nodes);
            }
        }
    }

    /// <summary>
    /// Applies active IdeTheme colors to header, controls, and tree views.
    /// </summary>
    public void ApplyTheme()
    {
        headerPanel.BackColor = IdeTheme.SurfaceElevated;
        lblTitle.ForeColor = IdeTheme.TextPrimary;

        lblBadge.BackColor = IdeTheme.IsDarkMode ? Color.FromArgb(40, 50, 65) : Color.FromArgb(219, 234, 254);
        lblBadge.ForeColor = IdeTheme.IsDarkMode ? Color.FromArgb(147, 197, 253) : Color.FromArgb(29, 78, 216);

        StyleToolbarButton(btnExpandAll);
        StyleToolbarButton(btnCollapseAll);
        StyleToolbarButton(btnToggleMode);
        StyleToolbarButton(btnNextMatch);

        txtSearch.BackColor = IdeTheme.EditorBackground;
        txtSearch.ForeColor = IdeTheme.TextPrimary;

        contentContainer.BackColor = IdeTheme.EditorBackground;
        treeView.BackColor = IdeTheme.EditorBackground;
        treeView.ForeColor = IdeTheme.TextPrimary;
        treeView.LineColor = IdeTheme.IsDarkMode ? Color.FromArgb(80, 80, 80) : Color.FromArgb(180, 180, 180);

        txtConnectorView.BackColor = IdeTheme.EditorBackground;
        txtConnectorView.ForeColor = IdeTheme.TextPrimary;

        if (treeView.Nodes.Count > 0)
        {
            ResetNodeColors(treeView.Nodes);
        }
    }

    private static void StyleToolbarButton(Button btn)
    {
        btn.BackColor = IdeTheme.Surface;
        btn.ForeColor = IdeTheme.TextPrimary;
        btn.FlatAppearance.BorderColor = IdeTheme.BorderLight;
        btn.FlatAppearance.MouseOverBackColor = IdeTheme.SurfaceHover;
    }
}
