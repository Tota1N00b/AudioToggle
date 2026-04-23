using Microsoft.Toolkit.Uwp.Notifications;

namespace AudioToggle;

internal sealed class NotificationService : IDisposable
{
    private readonly SynchronizationContext _synchronizationContext;

    public NotificationService(SynchronizationContext synchronizationContext)
    {
        _synchronizationContext = synchronizationContext;
        ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompatOnActivated;
    }

    public event EventHandler<NotificationActivatedEventArgs>? NotificationActivated;

    public void Show(string title, string message, string? action = null)
    {
        var builder = new ToastContentBuilder()
            .AddText(title)
            .AddText(message);

        if (!string.IsNullOrWhiteSpace(action))
        {
            builder.AddArgument("action", action);
        }

        builder.Show(toast =>
        {
            toast.Tag = "status";
            toast.Group = "audio-toggle";
            toast.ExpirationTime = DateTimeOffset.Now.AddMinutes(5);
        });
    }

    public void Dispose()
    {
        ToastNotificationManagerCompat.OnActivated -= ToastNotificationManagerCompatOnActivated;
    }

    private void ToastNotificationManagerCompatOnActivated(ToastNotificationActivatedEventArgsCompat toastArgs)
    {
        var parsedArguments = ToastArguments.Parse(toastArgs.Argument);
        parsedArguments.TryGetValue("action", out var action);
        _synchronizationContext.Post(
            _ => NotificationActivated?.Invoke(this, new NotificationActivatedEventArgs(action, toastArgs.Argument)),
            null);
    }
}

internal sealed class NotificationActivatedEventArgs(string? action, string rawArguments) : EventArgs
{
    public string? Action { get; } = action;
    public string RawArguments { get; } = rawArguments;
}
