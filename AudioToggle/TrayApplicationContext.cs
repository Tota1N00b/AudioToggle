namespace AudioToggle;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private const int ToggleDelayMs = 275;
    private const string OpenSettingsNotificationAction = "openSettings";

    private readonly ConfigService _configService = new();
    private readonly AudioDeviceManager _audioDeviceManager = new();
    private readonly StartupManager _startupManager = new();
    private readonly StartMenuIntegrationManager _startMenuIntegrationManager = new();
    private readonly IconManager _iconManager = new();
    private readonly ThemeManager _themeManager = new();
    private readonly SynchronizationContext _syncContext;
    private readonly NotificationService _notificationService;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly System.Windows.Forms.Timer _toggleTimer;

    private AppConfig _config;
    private List<AudioDeviceInfo> _devices = [];
    private SettingsForm? _settingsForm;
    private Icon? _currentTrayIcon;

    public TrayApplicationContext()
    {
        _syncContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _notificationService = new NotificationService(_syncContext);
        _config = _configService.Load();
        if (!TryApplyStartupPreference(_config.OpenOnStartup, out _))
        {
            _config.OpenOnStartup = _startupManager.IsEnabled();
        }

        TryEnsureStartMenuIntegration();

        _contextMenu = BuildContextMenu();
        ApplyContextMenuTheme();

        _currentTrayIcon = _iconManager.CreateTrayIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _currentTrayIcon,
            Text = "Audio Toggle",
            Visible = true,
            ContextMenuStrip = _contextMenu
        };

        _toggleTimer = new System.Windows.Forms.Timer
        {
            Interval = ToggleDelayMs
        };
        _toggleTimer.Tick += ToggleTimerOnTick;

        _notifyIcon.MouseClick += NotifyIconOnMouseClick;
        _notifyIcon.MouseDoubleClick += NotifyIconOnMouseDoubleClick;
        _iconManager.ThemeChanged += IconManagerOnThemeChanged;
        _themeManager.ThemeChanged += ThemeManagerOnThemeChanged;
        _notificationService.NotificationActivated += NotificationServiceOnNotificationActivated;

        RefreshDevices(showNotification: false);
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
            Padding = new Padding(6)
        };
        menu.Items.Add("Open Settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Toggle Output", null, (_, _) => ToggleOutput(showNotification: true));
        menu.Items.Add("Refresh Devices", null, (_, _) => RefreshDevices(showNotification: true));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        menu.Opening += (_, _) => ApplyContextMenuTheme();
        return menu;
    }

    private void NotifyIconOnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _toggleTimer.Stop();
        _toggleTimer.Start();
    }

    private void NotifyIconOnMouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _toggleTimer.Stop();
        ShowSettings();
    }

    private void ToggleTimerOnTick(object? sender, EventArgs e)
    {
        _toggleTimer.Stop();
        ToggleOutput(showNotification: true);
    }

    private void ShowSettings()
    {
        var palette = _themeManager.CurrentPalette;
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.ApplyTheme(palette);
            _settingsForm.BindDevices(_devices, _config.GetSelectedDeviceIds(), GetSelectionStatusMessage(), _startupManager.IsEnabled());
            _settingsForm.Show();
            _settingsForm.WindowState = FormWindowState.Normal;
            _settingsForm.BringToFront();
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(palette);
        _settingsForm.Icon = _iconManager.CreateWindowIcon();
        _settingsForm.RefreshRequested += (_, _) => RefreshDevices(showNotification: true);
        _settingsForm.ToggleNowRequested += (_, _) => ToggleOutput(showNotification: true);
        _settingsForm.DeviceSelectionToggled += (_, deviceId) => UpdateSelection(deviceId);
        _settingsForm.ClearSelectionRequested += (_, _) => ClearSelection();
        _settingsForm.StartupPreferenceChanged += (_, enabled) => UpdateStartupPreference(enabled);
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.BindDevices(_devices, _config.GetSelectedDeviceIds(), GetSelectionStatusMessage(), _startupManager.IsEnabled());
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    private void IconManagerOnThemeChanged(object? sender, EventArgs e)
    {
        var newIcon = _iconManager.CreateTrayIcon();
        var oldIcon = _currentTrayIcon;
        _currentTrayIcon = newIcon;
        _notifyIcon.Icon = newIcon;
        oldIcon?.Dispose();
    }

    private void ThemeManagerOnThemeChanged(object? sender, EventArgs e)
    {
        PostToUi(() =>
        {
            ApplyContextMenuTheme();
            _settingsForm?.ApplyTheme(_themeManager.CurrentPalette);
        });
    }

    private void NotificationServiceOnNotificationActivated(object? sender, NotificationActivatedEventArgs e)
    {
        if (string.Equals(e.Action, OpenSettingsNotificationAction, StringComparison.OrdinalIgnoreCase))
        {
            ShowSettings();
        }
    }

    private void UpdateSelection(string deviceId)
    {
        var selectedIds = _config.GetSelectedDeviceIds().ToList();

        if (selectedIds.Remove(deviceId))
        {
            _config.SetSelectedDeviceIds(selectedIds);
            SaveConfig();
            RefreshSettingsBinding();
            return;
        }

        if (selectedIds.Count >= 2)
        {
            RefreshSettingsBinding("Deselect one device first.");
            return;
        }

        selectedIds.Add(deviceId);
        _config.SetSelectedDeviceIds(selectedIds);
        SaveConfig();
        RefreshSettingsBinding();
    }

    private void ClearSelection()
    {
        _config.SetSelectedDeviceIds([]);
        SaveConfig();
        RefreshSettingsBinding();
    }

    private void UpdateStartupPreference(bool enabled)
    {
        if (TryApplyStartupPreference(enabled, out var errorMessage))
        {
            _config.OpenOnStartup = _startupManager.IsEnabled();
            SaveConfig();
            RefreshSettingsBinding();
            return;
        }

        _config.OpenOnStartup = _startupManager.IsEnabled();
        SaveConfig();
        RefreshSettingsBinding(errorMessage ?? "Could not update startup preference.");
        ShowNotification("Audio Toggle", errorMessage ?? "Could not update startup preference.", OpenSettingsNotificationAction);
    }

    private void RefreshDevices(bool showNotification)
    {
        try
        {
            _devices = _audioDeviceManager.GetActiveRenderDevices().ToList();
            UpdateTrayText();
            RefreshSettingsBinding();

            if (showNotification)
            {
                ShowNotification("Audio Toggle", $"Found {_devices.Count} active playback device(s).");
            }
        }
        catch (Exception ex)
        {
            ShowNotification("Audio Toggle", $"Could not refresh devices: {ex.Message}", OpenSettingsNotificationAction);
            RefreshSettingsBinding("Unable to refresh audio devices.");
        }
    }

    private void ToggleOutput(bool showNotification)
    {
        var selectedIds = _config.GetSelectedDeviceIds();
        if (selectedIds.Count != 2)
        {
            var message = "Select 2 devices before toggling.";
            RefreshSettingsBinding(message);
            if (showNotification)
            {
                ShowNotification("Audio Toggle", message, OpenSettingsNotificationAction);
            }

            return;
        }

        var primaryDevice = _devices.FirstOrDefault(device => device.Id == selectedIds[0]);
        var secondaryDevice = _devices.FirstOrDefault(device => device.Id == selectedIds[1]);

        if (primaryDevice is null || secondaryDevice is null)
        {
            var missingDeviceMessage = "One selected device is missing.";
            RefreshSettingsBinding(missingDeviceMessage);
            if (showNotification)
            {
                ShowNotification("Audio Toggle", missingDeviceMessage, OpenSettingsNotificationAction);
            }

            UpdateTrayText();
            return;
        }

        var currentDefaultId = _audioDeviceManager.GetCurrentDefaultDeviceId();
        var nextDevice = string.Equals(currentDefaultId, primaryDevice.Id, StringComparison.Ordinal) ? secondaryDevice : primaryDevice;

        try
        {
            _audioDeviceManager.SetDefaultOutputDevice(nextDevice.Id);
            RefreshDevices(showNotification: false);
            RefreshSettingsBinding();

            if (showNotification)
            {
                ShowNotification("Audio Toggle", $"Output: {nextDevice.FriendlyName}");
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"Could not switch output: {ex.Message}";
            RefreshSettingsBinding(errorMessage);
            if (showNotification)
            {
                ShowNotification("Audio Toggle", errorMessage, OpenSettingsNotificationAction);
            }
        }
    }

    private void RefreshSettingsBinding(string? statusOverride = null)
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.BindDevices(_devices, _config.GetSelectedDeviceIds(), statusOverride ?? GetSelectionStatusMessage(), _startupManager.IsEnabled());
        }
    }

    private string GetSelectionStatusMessage()
    {
        var selectedIds = _config.GetSelectedDeviceIds();
        if (selectedIds.Count == 0)
        {
            return "0 of 2 selected - Select up to 2 devices";
        }

        if (selectedIds.Count == 1)
        {
            return "1 of 2 selected - Select one more device";
        }

        var missingSelectedDevice = selectedIds.Any(selectedId => _devices.All(device => !string.Equals(device.Id, selectedId, StringComparison.Ordinal)));
        if (missingSelectedDevice)
        {
            return "2 of 2 selected - One saved device is missing";
        }

        return "2 of 2 selected - Ready to toggle";
    }

    private string BuildSelectionStatus()
    {
        var selectedIds = _config.GetSelectedDeviceIds();
        if (selectedIds.Count == 0)
        {
            return "0 of 2 selected · Select up to 2 devices";
        }

        if (selectedIds.Count == 1)
        {
            return "1 of 2 selected · Select one more device";
        }

        var missingSelectedDevice = selectedIds.Any(selectedId => _devices.All(device => !string.Equals(device.Id, selectedId, StringComparison.Ordinal)));
        if (missingSelectedDevice)
        {
            return "2 of 2 selected · One saved device is missing";
        }

        return "2 of 2 selected · Ready to toggle";
    }

    private void UpdateTrayText()
    {
        var currentDefaultId = _devices.FirstOrDefault(device => device.IsDefaultMultimedia)?.Id
            ?? _devices.FirstOrDefault(device => device.IsDefaultConsole)?.Id
            ?? _devices.FirstOrDefault(device => device.IsDefaultCommunications)?.Id;
        var currentName = _devices.FirstOrDefault(device => device.Id == currentDefaultId)?.FriendlyName
            ?? "Unknown output";

        var tooltip = $"Audio Toggle: {currentName}";
        _notifyIcon.Text = tooltip.Length <= 63 ? tooltip : tooltip[..63];
    }

    private bool TryApplyStartupPreference(bool enabled, out string? errorMessage)
    {
        try
        {
            _startupManager.SetEnabled(enabled);
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Startup setting failed: {ex.Message}";
            return false;
        }
    }

    private void SaveConfig()
    {
        _configService.Save(_config);
    }

    private void ShowNotification(string title, string message, string? action = null)
    {
        try
        {
            _notificationService.Show(title, message, action);
        }
        catch
        {
            _notifyIcon.ShowBalloonTip(2500, title, message, ToolTipIcon.None);
        }
    }

    private void TryEnsureStartMenuIntegration()
    {
        try
        {
            _startMenuIntegrationManager.EnsureInstalledIdentity();
        }
        catch
        {
            // Best effort: the app remains usable even if shell integration fails.
        }
    }

    private void ApplyContextMenuTheme()
    {
        var palette = _themeManager.CurrentPalette;
        _contextMenu.SuspendLayout();
        _contextMenu.RenderMode = ToolStripRenderMode.Professional;
        _contextMenu.Renderer = new FluentContextMenuRenderer(palette);
        _contextMenu.BackColor = palette.MenuBackground;
        _contextMenu.ForeColor = palette.MenuText;

        foreach (ToolStripItem item in _contextMenu.Items)
        {
            item.BackColor = palette.MenuBackground;
            item.ForeColor = palette.MenuText;
            item.Margin = item is ToolStripSeparator ? new Padding(0, 4, 0, 4) : new Padding(0);
            item.Padding = item is ToolStripSeparator ? Padding.Empty : new Padding(12, 8, 12, 8);
        }

        _contextMenu.ResumeLayout();
        _contextMenu.Invalidate();
    }

    private void PostToUi(Action action)
    {
        _syncContext.Post(_ => action(), null);
    }

    protected override void ExitThreadCore()
    {
        _toggleTimer.Stop();
        _iconManager.ThemeChanged -= IconManagerOnThemeChanged;
        _themeManager.ThemeChanged -= ThemeManagerOnThemeChanged;
        _notificationService.NotificationActivated -= NotificationServiceOnNotificationActivated;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _currentTrayIcon?.Dispose();
        _iconManager.Dispose();
        _themeManager.Dispose();
        _notificationService.Dispose();
        _contextMenu.Dispose();
        _settingsForm?.Close();
        base.ExitThreadCore();
    }
}
