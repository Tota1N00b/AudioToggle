using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AudioToggle;

internal sealed class ThemeManager : IDisposable
{
    private const string PersonalizeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsThemeValueName = "AppsUseLightTheme";

    private bool _isDarkMode;

    public ThemeManager()
    {
        _isDarkMode = DetectDarkMode();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public event EventHandler? ThemeChanged;

    public bool IsDarkMode => _isDarkMode;

    public ThemePalette CurrentPalette => _isDarkMode ? ThemePalette.Dark : ThemePalette.Light;

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.Color or UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle or UserPreferenceCategory.Window))
        {
            return;
        }

        var nextDarkMode = DetectDarkMode();
        if (nextDarkMode == _isDarkMode)
        {
            return;
        }

        _isDarkMode = nextDarkMode;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool DetectDarkMode()
    {
        using var personalizeKey = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath, writable: false);
        var appsValue = personalizeKey?.GetValue(AppsThemeValueName);
        return appsValue is int appsTheme && appsTheme == 0;
    }
}

internal sealed class ThemePalette
{
    private ThemePalette(
        bool isDark,
        Color windowBackground,
        Color surfaceBackground,
        Color surfaceDisabledBackground,
        Color borderColor,
        Color textPrimary,
        Color textSecondary,
        Color textMuted,
        Color successText,
        Color warningText,
        Color accent,
        Color selectedTagBackground,
        Color selectedTagBorder,
        Color selectedTagText,
        Color scrollThumb,
        Color menuBackground,
        Color menuBorder,
        Color menuHoverBackground,
        Color menuPressedBackground,
        Color menuText,
        Color menuSeparator,
        Color buttonBackground,
        Color buttonHoverBackground,
        Color buttonPressedBackground,
        Color buttonBorder,
        Color buttonHoverBorder,
        Color buttonText,
        Color primaryButtonBackground,
        Color primaryButtonHoverBackground,
        Color primaryButtonPressedBackground,
        Color primaryButtonBorder,
        Color primaryButtonText)
    {
        IsDark = isDark;
        WindowBackground = windowBackground;
        SurfaceBackground = surfaceBackground;
        SurfaceDisabledBackground = surfaceDisabledBackground;
        BorderColor = borderColor;
        TextPrimary = textPrimary;
        TextSecondary = textSecondary;
        TextMuted = textMuted;
        SuccessText = successText;
        WarningText = warningText;
        Accent = accent;
        SelectedTagBackground = selectedTagBackground;
        SelectedTagBorder = selectedTagBorder;
        SelectedTagText = selectedTagText;
        ScrollThumb = scrollThumb;
        MenuBackground = menuBackground;
        MenuBorder = menuBorder;
        MenuHoverBackground = menuHoverBackground;
        MenuPressedBackground = menuPressedBackground;
        MenuText = menuText;
        MenuSeparator = menuSeparator;
        ButtonBackground = buttonBackground;
        ButtonHoverBackground = buttonHoverBackground;
        ButtonPressedBackground = buttonPressedBackground;
        ButtonBorder = buttonBorder;
        ButtonHoverBorder = buttonHoverBorder;
        ButtonText = buttonText;
        PrimaryButtonBackground = primaryButtonBackground;
        PrimaryButtonHoverBackground = primaryButtonHoverBackground;
        PrimaryButtonPressedBackground = primaryButtonPressedBackground;
        PrimaryButtonBorder = primaryButtonBorder;
        PrimaryButtonText = primaryButtonText;
    }

    public static ThemePalette Light { get; } = new(
        isDark: false,
        windowBackground: Color.FromArgb(246, 247, 249),
        surfaceBackground: Color.FromArgb(252, 252, 253),
        surfaceDisabledBackground: Color.FromArgb(247, 248, 250),
        borderColor: Color.FromArgb(223, 226, 231),
        textPrimary: Color.FromArgb(23, 23, 23),
        textSecondary: Color.FromArgb(91, 95, 102),
        textMuted: Color.FromArgb(122, 128, 137),
        successText: Color.FromArgb(23, 94, 48),
        warningText: Color.FromArgb(148, 40, 26),
        accent: SystemColors.Highlight,
        selectedTagBackground: Color.FromArgb(234, 239, 246),
        selectedTagBorder: Color.FromArgb(214, 223, 234),
        selectedTagText: Color.FromArgb(59, 77, 102),
        scrollThumb: Color.FromArgb(176, 188, 204),
        menuBackground: Color.FromArgb(252, 252, 253),
        menuBorder: Color.FromArgb(218, 221, 227),
        menuHoverBackground: Color.FromArgb(238, 242, 247),
        menuPressedBackground: Color.FromArgb(232, 237, 244),
        menuText: Color.FromArgb(29, 33, 41),
        menuSeparator: Color.FromArgb(228, 231, 236),
        buttonBackground: Color.FromArgb(255, 255, 255),
        buttonHoverBackground: Color.FromArgb(246, 248, 251),
        buttonPressedBackground: Color.FromArgb(238, 241, 245),
        buttonBorder: Color.FromArgb(217, 221, 226),
        buttonHoverBorder: Color.FromArgb(191, 198, 208),
        buttonText: Color.FromArgb(29, 33, 41),
        primaryButtonBackground: Color.FromArgb(17, 24, 39),
        primaryButtonHoverBackground: Color.FromArgb(28, 39, 60),
        primaryButtonPressedBackground: Color.FromArgb(11, 18, 32),
        primaryButtonBorder: Color.FromArgb(17, 24, 39),
        primaryButtonText: Color.White);

    public static ThemePalette Dark { get; } = new(
        isDark: true,
        windowBackground: Color.FromArgb(30, 32, 36),
        surfaceBackground: Color.FromArgb(39, 42, 47),
        surfaceDisabledBackground: Color.FromArgb(35, 37, 42),
        borderColor: Color.FromArgb(73, 77, 85),
        textPrimary: Color.FromArgb(244, 246, 248),
        textSecondary: Color.FromArgb(197, 202, 210),
        textMuted: Color.FromArgb(149, 155, 164),
        successText: Color.FromArgb(101, 195, 124),
        warningText: Color.FromArgb(255, 170, 163),
        accent: SystemColors.Highlight,
        selectedTagBackground: Color.FromArgb(45, 59, 84),
        selectedTagBorder: Color.FromArgb(82, 100, 132),
        selectedTagText: Color.FromArgb(219, 231, 255),
        scrollThumb: Color.FromArgb(162, 171, 185),
        menuBackground: Color.FromArgb(37, 39, 44),
        menuBorder: Color.FromArgb(78, 82, 89),
        menuHoverBackground: Color.FromArgb(56, 60, 68),
        menuPressedBackground: Color.FromArgb(68, 73, 82),
        menuText: Color.FromArgb(244, 246, 248),
        menuSeparator: Color.FromArgb(79, 84, 92),
        buttonBackground: Color.FromArgb(49, 52, 58),
        buttonHoverBackground: Color.FromArgb(60, 64, 71),
        buttonPressedBackground: Color.FromArgb(43, 46, 52),
        buttonBorder: Color.FromArgb(78, 82, 90),
        buttonHoverBorder: Color.FromArgb(108, 114, 124),
        buttonText: Color.FromArgb(244, 246, 248),
        primaryButtonBackground: SystemColors.Highlight,
        primaryButtonHoverBackground: ColorBlendHelper.Blend(SystemColors.Highlight, Color.White, 0.10F),
        primaryButtonPressedBackground: ColorBlendHelper.Blend(SystemColors.Highlight, Color.Black, 0.10F),
        primaryButtonBorder: SystemColors.Highlight,
        primaryButtonText: Color.White);

    public bool IsDark { get; }
    public Color WindowBackground { get; }
    public Color SurfaceBackground { get; }
    public Color SurfaceDisabledBackground { get; }
    public Color BorderColor { get; }
    public Color TextPrimary { get; }
    public Color TextSecondary { get; }
    public Color TextMuted { get; }
    public Color SuccessText { get; }
    public Color WarningText { get; }
    public Color Accent { get; }
    public Color SelectedTagBackground { get; }
    public Color SelectedTagBorder { get; }
    public Color SelectedTagText { get; }
    public Color ScrollThumb { get; }
    public Color MenuBackground { get; }
    public Color MenuBorder { get; }
    public Color MenuHoverBackground { get; }
    public Color MenuPressedBackground { get; }
    public Color MenuText { get; }
    public Color MenuSeparator { get; }
    public Color ButtonBackground { get; }
    public Color ButtonHoverBackground { get; }
    public Color ButtonPressedBackground { get; }
    public Color ButtonBorder { get; }
    public Color ButtonHoverBorder { get; }
    public Color ButtonText { get; }
    public Color PrimaryButtonBackground { get; }
    public Color PrimaryButtonHoverBackground { get; }
    public Color PrimaryButtonPressedBackground { get; }
    public Color PrimaryButtonBorder { get; }
    public Color PrimaryButtonText { get; }
}

internal sealed class FluentContextMenuRenderer(ThemePalette palette) : ToolStripProfessionalRenderer(new FluentContextMenuColorTable(palette))
{
    private readonly ThemePalette _palette = palette;

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(_palette.MenuBackground);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var y = e.Item.ContentRectangle.Top + (e.Item.ContentRectangle.Height / 2);
        using var pen = new Pen(_palette.MenuSeparator);
        e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = _palette.MenuText;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = _palette.MenuText;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(_palette.MenuBorder);
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }
}

internal sealed class FluentContextMenuColorTable(ThemePalette palette) : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => palette.MenuBackground;
    public override Color ImageMarginGradientBegin => palette.MenuBackground;
    public override Color ImageMarginGradientMiddle => palette.MenuBackground;
    public override Color ImageMarginGradientEnd => palette.MenuBackground;
    public override Color MenuBorder => palette.MenuBorder;
    public override Color MenuItemBorder => Color.Transparent;
    public override Color MenuItemSelected => palette.MenuHoverBackground;
    public override Color MenuItemSelectedGradientBegin => palette.MenuHoverBackground;
    public override Color MenuItemSelectedGradientEnd => palette.MenuHoverBackground;
    public override Color MenuItemPressedGradientBegin => palette.MenuPressedBackground;
    public override Color MenuItemPressedGradientMiddle => palette.MenuPressedBackground;
    public override Color MenuItemPressedGradientEnd => palette.MenuPressedBackground;
    public override Color SeparatorDark => palette.MenuSeparator;
    public override Color SeparatorLight => palette.MenuSeparator;
}

internal static class ThemeInterop
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

    public static void ApplyWindowTheme(Form form, ThemePalette palette)
    {
        if (!form.IsHandleCreated)
        {
            return;
        }

        var useDarkMode = palette.IsDark ? 1 : 0;
        _ = DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref useDarkMode, Marshal.SizeOf<int>());
        _ = DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkModeBefore20H1, ref useDarkMode, Marshal.SizeOf<int>());
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int attributeSize);
}

internal static class ColorBlendHelper
{
    public static Color Blend(Color baseColor, Color blendColor, float amount)
    {
        amount = Math.Clamp(amount, 0F, 1F);
        var red = (int)Math.Round((baseColor.R * (1 - amount)) + (blendColor.R * amount));
        var green = (int)Math.Round((baseColor.G * (1 - amount)) + (blendColor.G * amount));
        var blue = (int)Math.Round((baseColor.B * (1 - amount)) + (blendColor.B * amount));
        return Color.FromArgb(red, green, blue);
    }
}
