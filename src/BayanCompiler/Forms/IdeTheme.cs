namespace BayanCompiler.Forms;

/// <summary>
/// Authentic Visual Studio Code design tokens and theme palettes.
/// Provides high-fidelity Dark+ and Light+ styling matching VS Code.
/// </summary>
public static class IdeTheme
{
    public static bool IsDarkMode { get; set; } = true;

    // Authentic VS Code Dark+ Palette
    public static class Dark
    {
        public static readonly Color AppBackground = Color.FromArgb(30, 30, 30);
        public static readonly Color TitleBar = Color.FromArgb(50, 50, 51);               // #323233
        public static readonly Color ActivityBar = Color.FromArgb(51, 51, 51);            // #333333
        public static readonly Color ActivityBarActive = Color.FromArgb(0, 122, 204);     // #007ACC
        public static readonly Color SideBar = Color.FromArgb(37, 37, 38);                // #252526
        public static readonly Color SideBarHeader = Color.FromArgb(45, 45, 46);          // #2D2D2E
        public static readonly Color EditorBackground = Color.FromArgb(30, 30, 30);       // #1E1E1E
        public static readonly Color EditorTabActive = Color.FromArgb(30, 30, 30);        // #1E1E1E
        public static readonly Color EditorTabInactive = Color.FromArgb(45, 45, 45);      // #2D2D2D
        public static readonly Color EditorTabIndicator = Color.FromArgb(0, 122, 204);   // #007ACC
        public static readonly Color BreadcrumbsBg = Color.FromArgb(30, 30, 30);
        public static readonly Color PanelBackground = Color.FromArgb(30, 30, 30);        // #1E1E1E
        public static readonly Color PanelTabActive = Color.FromArgb(231, 231, 231);
        public static readonly Color PanelTabInactive = Color.FromArgb(133, 133, 133);
        public static readonly Color StatusBar = Color.FromArgb(0, 122, 204);             // #007ACC
        public static readonly Color StatusBarForeground = Color.White;
        public static readonly Color CommandCenter = Color.FromArgb(60, 60, 60);          // #3C3C3C
        public static readonly Color CommandCenterBorder = Color.FromArgb(75, 75, 75);

        public static readonly Color Surface = Color.FromArgb(37, 37, 38);
        public static readonly Color SurfaceElevated = Color.FromArgb(45, 45, 45);
        public static readonly Color SurfaceHover = Color.FromArgb(55, 55, 55);
        public static readonly Color SurfaceActive = Color.FromArgb(65, 65, 65);
        public static readonly Color Border = Color.FromArgb(43, 43, 43);                 // #2B2B2B
        public static readonly Color BorderLight = Color.FromArgb(63, 63, 70);

        public static readonly Color TextPrimary = Color.FromArgb(212, 212, 212);         // #D4D4D4
        public static readonly Color TextSecondary = Color.FromArgb(160, 160, 160);
        public static readonly Color TextMuted = Color.FromArgb(115, 115, 115);

        public static readonly Color AccentPrimary = Color.FromArgb(0, 122, 204);         // #007ACC
        public static readonly Color AccentPrimaryHover = Color.FromArgb(17, 119, 187);
        public static readonly Color AccentSuccess = Color.FromArgb(46, 160, 67);         // #2EA043 (VS Code Green)
        public static readonly Color AccentSuccessHover = Color.FromArgb(63, 185, 80);
        public static readonly Color AccentError = Color.FromArgb(241, 76, 76);           // #F14C4C
        public static readonly Color AccentWarning = Color.FromArgb(204, 167, 0);         // #CCA700
        public static readonly Color AccentCyan = Color.FromArgb(78, 201, 176);           // #4EC9B0

        public static readonly Color TerminalBg = Color.FromArgb(24, 24, 24);             // #181818
        public static readonly Color TerminalText = Color.FromArgb(204, 204, 204);
        public static readonly Color TerminalPrompt = Color.FromArgb(78, 201, 176);       // #4EC9B0
        public static readonly Color GutterBackground = Color.FromArgb(30, 30, 30);
        public static readonly Color GutterText = Color.FromArgb(100, 100, 100);
    }

    // Authentic VS Code Light+ Palette
    public static class Light
    {
        public static readonly Color AppBackground = Color.FromArgb(243, 243, 243);
        public static readonly Color TitleBar = Color.FromArgb(221, 221, 221);
        public static readonly Color ActivityBar = Color.FromArgb(44, 44, 44);
        public static readonly Color ActivityBarActive = Color.FromArgb(0, 122, 204);
        public static readonly Color SideBar = Color.FromArgb(243, 243, 243);
        public static readonly Color SideBarHeader = Color.FromArgb(235, 235, 235);
        public static readonly Color EditorBackground = Color.FromArgb(255, 255, 255);
        public static readonly Color EditorTabActive = Color.FromArgb(255, 255, 255);
        public static readonly Color EditorTabInactive = Color.FromArgb(236, 236, 236);
        public static readonly Color EditorTabIndicator = Color.FromArgb(0, 122, 204);
        public static readonly Color BreadcrumbsBg = Color.FromArgb(255, 255, 255);
        public static readonly Color PanelBackground = Color.FromArgb(243, 243, 243);
        public static readonly Color PanelTabActive = Color.FromArgb(51, 51, 51);
        public static readonly Color PanelTabInactive = Color.FromArgb(112, 112, 112);
        public static readonly Color StatusBar = Color.FromArgb(0, 122, 204);
        public static readonly Color StatusBarForeground = Color.White;
        public static readonly Color CommandCenter = Color.FromArgb(229, 229, 229);
        public static readonly Color CommandCenterBorder = Color.FromArgb(212, 212, 212);

        public static readonly Color Surface = Color.FromArgb(243, 243, 243);
        public static readonly Color SurfaceElevated = Color.FromArgb(235, 235, 235);
        public static readonly Color SurfaceHover = Color.FromArgb(224, 224, 224);
        public static readonly Color SurfaceActive = Color.FromArgb(210, 210, 210);
        public static readonly Color Border = Color.FromArgb(229, 229, 229);
        public static readonly Color BorderLight = Color.FromArgb(210, 210, 210);

        public static readonly Color TextPrimary = Color.FromArgb(30, 30, 30);
        public static readonly Color TextSecondary = Color.FromArgb(97, 97, 97);
        public static readonly Color TextMuted = Color.FromArgb(150, 150, 150);

        public static readonly Color AccentPrimary = Color.FromArgb(0, 122, 204);
        public static readonly Color AccentPrimaryHover = Color.FromArgb(17, 119, 187);
        public static readonly Color AccentSuccess = Color.FromArgb(46, 160, 67);
        public static readonly Color AccentSuccessHover = Color.FromArgb(63, 185, 80);
        public static readonly Color AccentError = Color.FromArgb(229, 20, 0);
        public static readonly Color AccentWarning = Color.FromArgb(191, 142, 0);
        public static readonly Color AccentCyan = Color.FromArgb(0, 150, 136);

        public static readonly Color TerminalBg = Color.FromArgb(248, 248, 248);
        public static readonly Color TerminalText = Color.FromArgb(51, 51, 51);
        public static readonly Color TerminalPrompt = Color.FromArgb(16, 149, 106);
        public static readonly Color GutterBackground = Color.FromArgb(255, 255, 255);
        public static readonly Color GutterText = Color.FromArgb(160, 160, 160);
    }

    // Dynamic Getters based on active theme
    public static Color AppBackground => IsDarkMode ? Dark.AppBackground : Light.AppBackground;
    public static Color TitleBar => IsDarkMode ? Dark.TitleBar : Light.TitleBar;
    public static Color ActivityBar => IsDarkMode ? Dark.ActivityBar : Light.ActivityBar;
    public static Color ActivityBarActive => IsDarkMode ? Dark.ActivityBarActive : Light.ActivityBarActive;
    public static Color SideBar => IsDarkMode ? Dark.SideBar : Light.SideBar;
    public static Color SideBarHeader => IsDarkMode ? Dark.SideBarHeader : Light.SideBarHeader;
    public static Color EditorBackground => IsDarkMode ? Dark.EditorBackground : Light.EditorBackground;
    public static Color EditorTabActive => IsDarkMode ? Dark.EditorTabActive : Light.EditorTabActive;
    public static Color EditorTabInactive => IsDarkMode ? Dark.EditorTabInactive : Light.EditorTabInactive;
    public static Color EditorTabIndicator => IsDarkMode ? Dark.EditorTabIndicator : Light.EditorTabIndicator;
    public static Color BreadcrumbsBg => IsDarkMode ? Dark.BreadcrumbsBg : Light.BreadcrumbsBg;
    public static Color PanelBackground => IsDarkMode ? Dark.PanelBackground : Light.PanelBackground;
    public static Color PanelTabActive => IsDarkMode ? Dark.PanelTabActive : Light.PanelTabActive;
    public static Color PanelTabInactive => IsDarkMode ? Dark.PanelTabInactive : Light.PanelTabInactive;
    public static Color StatusBar => IsDarkMode ? Dark.StatusBar : Light.StatusBar;
    public static Color StatusBarForeground => IsDarkMode ? Dark.StatusBarForeground : Light.StatusBarForeground;
    public static Color CommandCenter => IsDarkMode ? Dark.CommandCenter : Light.CommandCenter;
    public static Color CommandCenterBorder => IsDarkMode ? Dark.CommandCenterBorder : Light.CommandCenterBorder;

    public static Color Surface => IsDarkMode ? Dark.Surface : Light.Surface;
    public static Color SurfaceElevated => IsDarkMode ? Dark.SurfaceElevated : Light.SurfaceElevated;
    public static Color SurfaceHover => IsDarkMode ? Dark.SurfaceHover : Light.SurfaceHover;
    public static Color SurfaceActive => IsDarkMode ? Dark.SurfaceActive : Light.SurfaceActive;
    public static Color Border => IsDarkMode ? Dark.Border : Light.Border;
    public static Color BorderLight => IsDarkMode ? Dark.BorderLight : Light.BorderLight;
    public static Color TextPrimary => IsDarkMode ? Dark.TextPrimary : Light.TextPrimary;
    public static Color TextSecondary => IsDarkMode ? Dark.TextSecondary : Light.TextSecondary;
    public static Color TextMuted => IsDarkMode ? Dark.TextMuted : Light.TextMuted;

    public static Color AccentPrimary => IsDarkMode ? Dark.AccentPrimary : Light.AccentPrimary;
    public static Color AccentPrimaryHover => IsDarkMode ? Dark.AccentPrimaryHover : Light.AccentPrimaryHover;
    public static Color AccentSuccess => IsDarkMode ? Dark.AccentSuccess : Light.AccentSuccess;
    public static Color AccentSuccessHover => IsDarkMode ? Dark.AccentSuccessHover : Light.AccentSuccessHover;
    public static Color AccentError => IsDarkMode ? Dark.AccentError : Light.AccentError;
    public static Color AccentWarning => IsDarkMode ? Dark.AccentWarning : Light.AccentWarning;
    public static Color AccentCyan => IsDarkMode ? Dark.AccentCyan : Light.AccentCyan;

    public static Color TerminalBg => IsDarkMode ? Dark.TerminalBg : Light.TerminalBg;
    public static Color TerminalText => IsDarkMode ? Dark.TerminalText : Light.TerminalText;
    public static Color TerminalPrompt => IsDarkMode ? Dark.TerminalPrompt : Light.TerminalPrompt;
    public static Color GutterBackground => IsDarkMode ? Dark.GutterBackground : Light.GutterBackground;
    public static Color GutterText => IsDarkMode ? Dark.GutterText : Light.GutterText;

    // Standard Fonts
    public static readonly Font EditorFont = new("Cascadia Mono", 11.5F, FontStyle.Regular);
    public static readonly Font ViewerCodeFont = new("Cascadia Mono", 10F, FontStyle.Regular);
    public static readonly Font UiFontRegular = new("Segoe UI", 9F, FontStyle.Regular);
    public static readonly Font UiFontBold = new("Segoe UI", 9F, FontStyle.Bold);
    public static readonly Font HeaderFont = new("Segoe UI", 11.5F, FontStyle.Bold);
    public static readonly Font SubHeaderFont = new("Segoe UI", 9.5F, FontStyle.Bold);
    public static readonly Font TerminalFont = new("Cascadia Mono", 10F, FontStyle.Regular);
    public static readonly Font BadgeFont = new("Segoe UI", 8.5F, FontStyle.Bold);
    public static readonly Font ActivityIconFont = new("Segoe UI Symbol", 14F, FontStyle.Regular);
}
