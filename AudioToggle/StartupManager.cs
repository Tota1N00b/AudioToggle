namespace AudioToggle;

internal sealed class StartupManager
{
    private const string ShortcutFileName = "Audio Toggle.lnk";
    private const string LegacyShortcutFileName = "AudioToggle.lnk";
    private const string LegacyExecutableShortcutFileName = "AudioToggle.exe.lnk";

    public bool IsEnabled()
    {
        return GetManagedShortcutPaths().Any(File.Exists);
    }

    public void SetEnabled(bool enabled)
    {
        var shortcutPath = GetPrimaryShortcutPath();

        if (!enabled)
        {
            foreach (var managedShortcutPath in GetManagedShortcutPaths())
            {
                if (File.Exists(managedShortcutPath))
                {
                    File.Delete(managedShortcutPath);
                }
            }

            return;
        }

        RemoveDuplicateManagedShortcuts();
        CreateShortcut(shortcutPath);
    }

    private static string GetPrimaryShortcutPath()
    {
        var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        return Path.Combine(startupFolder, ShortcutFileName);
    }

    private static IEnumerable<string> GetManagedShortcutPaths()
    {
        var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        return
        [
            Path.Combine(startupFolder, ShortcutFileName),
            Path.Combine(startupFolder, LegacyShortcutFileName),
            Path.Combine(startupFolder, LegacyExecutableShortcutFileName)
        ];
    }

    private static void RemoveDuplicateManagedShortcuts()
    {
        var primaryShortcutPath = GetPrimaryShortcutPath();
        foreach (var shortcutPath in GetManagedShortcutPaths())
        {
            if (string.Equals(shortcutPath, primaryShortcutPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }
        }
    }

    private static void CreateShortcut(string shortcutPath)
    {
        ShellLinkUtility.CreateOrUpdateShortcut(new ShellShortcutOptions
        {
            ShortcutPath = shortcutPath,
            TargetPath = AppIdentity.ExecutablePath,
            WorkingDirectory = AppIdentity.WorkingDirectory,
            Description = AppIdentity.DisplayName,
            IconPath = File.Exists(AppIdentity.IconPath) ? AppIdentity.IconPath : null,
            AppUserModelId = AppIdentity.AppUserModelId
        });
    }
}
