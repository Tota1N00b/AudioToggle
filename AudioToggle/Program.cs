namespace AudioToggle;

static class Program
{
    private const string SingleInstanceMutexName = @"Local\AudioToggle.SingleInstance";

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], AppIdentity.CleanupToastRegistrationArgument, StringComparison.OrdinalIgnoreCase))
        {
            new StartMenuIntegrationManager().CleanupToastRegistration();
            return;
        }

        if (args.Length > 0 && string.Equals(args[0], AppIdentity.RegisterShellIntegrationArgument, StringComparison.OrdinalIgnoreCase))
        {
            new StartMenuIntegrationManager().EnsureInstalledIdentity();
            return;
        }

        AppIdentity.ApplyCurrentProcessIdentity();

        using var singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
