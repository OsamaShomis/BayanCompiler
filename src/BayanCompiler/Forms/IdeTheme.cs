namespace BayanCompiler.Forms;

/// <summary>
/// Central design system and color palette tokens for the modern Bayan IDE.
/// Provides cohesive dark and light visual styling inspired by professional modern developer tools.
/// </summary>
public static class IdeTheme
{
    public static bool IsDarkMode { get; set; } = true;

    // Dark Mode Palette (VS Code / Catppuccin inspired)
    public static class Dark
    {
        public static readonly Color AppBackground = Color.FromArgb(20, 22, 34);       // #141622
        public static readonly Color Surface = Color.FromArgb(28, 31, 48);             // #1C1F30
        public static readonly Color SurfaceElevated = Color.FromArgb(35, 39, 60);     // #23273C
        public static readonly Color SurfaceHover = Color.FromArgb(45, 50, 77);        // #2D324D
        public static readonly Color SurfaceActive = Color.FromArgb(55, 62, 95);       // #373E5F
        public static readonly Color Border = Color.FromArgb(46, 52, 78);              // #2E344E
        public static readonly Color BorderLight = Color.FromArgb(64, 72, 105);        // #404869
        
        public static readonly Color TextPrimary = Color.FromArgb(235, 239, 252);      // #EBEFFC
        public static readonly Color TextSecondary = Color.FromArgb(160, 168, 195);    // #A0A8C3
        public static readonly Color TextMuted = Color.FromArgb(115, 123, 150);        // #737B96

        public static readonly Color AccentPrimary = Color.FromArgb(79, 110, 247);     // #4F6EF7 (Indigo/Blue)
        public static readonly Color AccentPrimaryHover = Color.FromArgb(99, 128, 255);
        public static readonly Color AccentSuccess = Color.FromArgb(16, 185, 129);     // #10B981 (Emerald)
        public static readonly Color AccentSuccessHover = Color.FromArgb(34, 197, 94);
        public static readonly Color AccentError = Color.FromArgb(239, 68, 68);        // #EF4444 (Coral Red)
        public static readonly Color AccentWarning = Color.FromArgb(245, 158, 11);     // #F59E0B (Amber)
        public static readonly Color AccentPurple = Color.FromArgb(168, 85, 247);      // #A855F7 (Purple)
        public static readonly Color AccentCyan = Color.FromArgb(6, 182, 212);         // #06B6D4 (Cyan)

        public static readonly Color TerminalBg = Color.FromArgb(13, 15, 24);          // #0D0F18
        public static readonly Color TerminalText = Color.FromArgb(209, 213, 219);     // #D1D5DB
        public static readonly Color TerminalPrompt = Color.FromArgb(52, 211, 153);    // #34D399
        public static readonly Color GutterBackground = Color.FromArgb(22, 24, 38);
        public static readonly Color GutterText = Color.FromArgb(90, 98, 125);
    }

    // Clean Light Mode Palette
    public static class Light
    {
        public static readonly Color AppBackground = Color.FromArgb(243, 245, 249);
        public static readonly Color Surface = Color.FromArgb(255, 255, 255);
        public static readonly Color SurfaceElevated = Color.FromArgb(235, 238, 245);
        public static readonly Color SurfaceHover = Color.FromArgb(224, 228, 238);
        public static readonly Color SurfaceActive = Color.FromArgb(210, 216, 230);
        public static readonly Color Border = Color.FromArgb(218, 223, 233);
        public static readonly Color BorderLight = Color.FromArgb(195, 202, 216);

        public static readonly Color TextPrimary = Color.FromArgb(17, 24, 39);
        public static readonly Color TextSecondary = Color.FromArgb(75, 85, 99);
        public static readonly Color TextMuted = Color.FromArgb(140, 149, 163);

        public static readonly Color AccentPrimary = Color.FromArgb(37, 99, 235);
        public static readonly Color AccentPrimaryHover = Color.FromArgb(29, 78, 216);
        public static readonly Color AccentSuccess = Color.FromArgb(16, 149, 106);
        public static readonly Color AccentSuccessHover = Color.FromArgb(13, 122, 87);
        public static readonly Color AccentError = Color.FromArgb(220, 38, 38);
        public static readonly Color AccentWarning = Color.FromArgb(217, 119, 6);
        public static readonly Color AccentPurple = Color.FromArgb(147, 51, 234);
        public static readonly Color AccentCyan = Color.FromArgb(8, 145, 178);

        public static readonly Color TerminalBg = Color.FromArgb(24, 28, 36);
        public static readonly Color TerminalText = Color.FromArgb(229, 231, 235);
        public static readonly Color TerminalPrompt = Color.FromArgb(52, 211, 153);
        public static readonly Color GutterBackground = Color.FromArgb(240, 242, 247);
        public static readonly Color GutterText = Color.FromArgb(130, 138, 155);
    }

    // Dynamic Getters based on active theme
    public static Color AppBackground => IsDarkMode ? Dark.AppBackground : Light.AppBackground;
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
    public static Color AccentSuccess => IsDarkMode ? Dark.AccentSuccess : Light.AccentSuccess;
    public static Color AccentError => IsDarkMode ? Dark.AccentError : Light.AccentError;
    public static Color AccentWarning => IsDarkMode ? Dark.AccentWarning : Light.AccentWarning;
    public static Color AccentPurple => IsDarkMode ? Dark.AccentPurple : Light.AccentPurple;
    public static Color AccentCyan => IsDarkMode ? Dark.AccentCyan : Light.AccentCyan;

    public static Color TerminalBg => IsDarkMode ? Dark.TerminalBg : Light.TerminalBg;
    public static Color TerminalText => IsDarkMode ? Dark.TerminalText : Light.TerminalText;
    public static Color TerminalPrompt => IsDarkMode ? Dark.TerminalPrompt : Light.TerminalPrompt;
    public static Color GutterBackground => IsDarkMode ? Dark.GutterBackground : Light.GutterBackground;
    public static Color GutterText => IsDarkMode ? Dark.GutterText : Light.GutterText;

    // Standard Fonts
    public static readonly Font EditorFont = new("Cascadia Mono", 11.5F, FontStyle.Regular);
    public static readonly Font ViewerCodeFont = new("Cascadia Mono", 10.5F, FontStyle.Regular);
    public static readonly Font UiFontRegular = new("Segoe UI", 9.5F, FontStyle.Regular);
    public static readonly Font UiFontBold = new("Segoe UI", 9.5F, FontStyle.Bold);
    public static readonly Font HeaderFont = new("Segoe UI", 13.5F, FontStyle.Bold);
    public static readonly Font SubHeaderFont = new("Segoe UI", 10.5F, FontStyle.Bold);
    public static readonly Font TerminalFont = new("Cascadia Mono", 10F, FontStyle.Regular);
    public static readonly Font BadgeFont = new("Segoe UI", 8.5F, FontStyle.Bold);
}
