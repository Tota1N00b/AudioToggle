using System.Text.Json.Serialization;

namespace AudioToggle;

internal sealed class AppConfig
{
    [JsonPropertyName("selectedDeviceIds")]
    public List<string> SelectedDeviceIds { get; set; } = [];

    [JsonPropertyName("openOnStartup")]
    public bool OpenOnStartup { get; set; }

    public IReadOnlyList<string> GetSelectedDeviceIds()
    {
        return SelectedDeviceIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToList();
    }

    public void SetSelectedDeviceIds(IEnumerable<string> deviceIds)
    {
        SelectedDeviceIds = deviceIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToList();
    }
}
