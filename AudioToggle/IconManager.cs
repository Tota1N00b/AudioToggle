using Microsoft.Win32;

namespace AudioToggle;

internal enum TrayIconState
{
    Error,
    FirstDevice,
    SecondDevice
}

internal sealed class IconManager : IDisposable
{
    private const string PersonalizeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string SystemThemeValueName = "SystemUsesLightTheme";
    private const string AppsThemeValueName = "AppsUseLightTheme";

    private readonly string _appIconPath;
    private readonly string _errorLightIconPath;
    private readonly string _errorDarkIconPath;
    private readonly string _firstLightIconPath;
    private readonly string _firstDarkIconPath;
    private readonly string _secondLightIconPath;
    private readonly string _secondDarkIconPath;

    public IconManager()
    {
        _appIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "audio-toggle-app.ico");
        _errorLightIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "audio-toggle-error-light.ico");
        _errorDarkIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "audio-toggle-error-dark.ico");
        _firstLightIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "audio-toggle-1-light.ico");
        _firstDarkIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "audio-toggle-1-dark.ico");
        _secondLightIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "audio-toggle-2-light.ico");
        _secondDarkIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "audio-toggle-2-dark.ico");
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public event EventHandler? ThemeChanged;

    public Icon CreateTrayIcon(TrayIconState state)
    {
        return new Icon(GetThemeAwareIconPath(state));
    }

    public Icon CreateWindowIcon()
    {
        return new Icon(_appIconPath);
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private string GetThemeAwareIconPath(TrayIconState state)
    {
        var useLightIcon = IsSystemUsingLightTheme();
        return state switch
        {
            TrayIconState.FirstDevice => useLightIcon ? _firstLightIconPath : _firstDarkIconPath,
            TrayIconState.SecondDevice => useLightIcon ? _secondLightIconPath : _secondDarkIconPath,
            _ => useLightIcon ? _errorLightIconPath : _errorDarkIconPath
        };
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
