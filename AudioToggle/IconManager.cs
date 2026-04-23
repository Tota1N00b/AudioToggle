using Microsoft.Win32;

namespace AudioToggle;

internal sealed class IconManager : IDisposable
{
    private const string PersonalizeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string SystemThemeValueName = "SystemUsesLightTheme";
    private const string AppsThemeValueName = "AppsUseLightTheme";

    private readonly string _lightIconPath;
    private readonly string _darkIconPath;
    private readonly string _appIconPath;

    public IconManager()
    {
        _appIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "audio-toggle-app.ico");
        _lightIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "audio-toggle-light.ico");
        _darkIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "audio-toggle-dark.ico");
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public event EventHandler? ThemeChanged;

    public Icon CreateTrayIcon()
    {
        return new Icon(GetThemeAwareIconPath());
    }

    public Icon CreateWindowIcon()
    {
        return new Icon(_appIconPath);
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private string GetThemeAwareIconPath()
    {
        return IsSystemUsingLightTheme() ? _lightIconPath : _darkIconPath;
    }

    private static bool IsSystemUsingLightTheme()
    {
        using var personalizeKey = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath, writable: false);
        var systemValue = personalizeKey?.GetValue(SystemThemeValueName);
        if (systemValue is int systemTheme)
        {
            return systemTheme != 0;
        }

        var appsValue = personalizeKey?.GetValue(AppsThemeValueName);
        return appsValue is not int appsTheme || appsTheme != 0;
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
}
