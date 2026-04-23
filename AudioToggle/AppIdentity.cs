using System.Runtime.InteropServices;

namespace AudioToggle;

internal static class AppIdentity
{
    public const string AppUserModelId = "AudioToggle.AudioToggle";
    public const string DisplayName = "Audio Toggle";
    public const string CleanupToastRegistrationArgument = "--cleanup-toast-registration";
    public const string RegisterShellIntegrationArgument = "--register-shell-integration";

    public static string ExecutablePath => Application.ExecutablePath;

    public static string WorkingDirectory => AppContext.BaseDirectory;

    public static string IconPath => Path.Combine(AppContext.BaseDirectory, "Assets", "audio-toggle-app.ico");

    public static void ApplyCurrentProcessIdentity()
    {
        _ = SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);
}
