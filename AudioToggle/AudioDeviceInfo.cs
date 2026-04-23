namespace AudioToggle;

internal sealed class AudioDeviceInfo
{
    public required string Id { get; init; }

    public required string FriendlyName { get; init; }

    public bool IsDefaultConsole { get; init; }

    public bool IsDefaultMultimedia { get; init; }

    public bool IsDefaultCommunications { get; init; }

    public bool IsCurrentOutput => IsDefaultConsole || IsDefaultMultimedia || IsDefaultCommunications;

    public string DefaultStatus
    {
        get
        {
            var roles = new List<string>();
            if (IsDefaultConsole)
            {
                roles.Add("Console");
            }

            if (IsDefaultMultimedia)
            {
                roles.Add("Media");
            }

            if (IsDefaultCommunications)
            {
                roles.Add("Comms");
            }

            return roles.Count == 0 ? "No" : string.Join(", ", roles);
        }
    }
}
