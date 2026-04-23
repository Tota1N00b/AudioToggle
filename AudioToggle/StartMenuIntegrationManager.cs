using Microsoft.Toolkit.Uwp.Notifications;

namespace AudioToggle;

internal sealed class StartMenuIntegrationManager
{
    private const string ShortcutFileName = "Audio Toggle.lnk";
    private const string LegacyShortcutFileName = "AudioToggle.lnk";
    private const string LegacyExecutableShortcutFileName = "AudioToggle.exe.lnk";

    public void EnsureInstalledIdentity()
    {
        AppIdentity.ApplyCurrentProcessIdentity();
        RemoveDuplicateManagedShortcuts();
        ShellLinkUtility.CreateOrUpdateShortcut(new ShellShortcutOptions
        {
            ShortcutPath = GetPrimaryShortcutPath(),
            TargetPath = AppIdentity.ExecutablePath,
            WorkingDirectory = AppIdentity.WorkingDirectory,
            Description = AppIdentity.DisplayName,
            IconPath = File.Exists(AppIdentity.IconPath) ? AppIdentity.IconPath : null,
            AppUserModelId = AppIdentity.AppUserModelId
        });
    }

    public void CleanupToastRegistration()
    {
        ToastNotificationManagerCompat.Uninstall();
        RemoveManagedShortcuts();
    }

    private static string GetPrimaryShortcutPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), ShortcutFileName);
    }

    private static IEnumerable<string> GetManagedShortcutPaths()
    {
        var startMenuPrograms = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        return
        [
            Path.Combine(startMenuPrograms, ShortcutFileName),
            Path.Combine(startMenuPrograms, LegacyShortcutFileName),
            Path.Combine(startMenuPrograms, LegacyExecutableShortcutFileName)
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

    private static void RemoveManagedShortcuts()
    {
        foreach (var shortcutPath in GetManagedShortcutPaths())
        {
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }
        }
    }
}
