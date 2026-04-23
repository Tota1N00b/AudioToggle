using System.Text.Json;

namespace AudioToggle;

internal sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _configPath;

    public ConfigService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDirectory = Path.Combine(appData, "AudioToggle");
        Directory.CreateDirectory(configDirectory);
        _configPath = Path.Combine(configDirectory, "config.json");
    }

    public string ConfigPath => _configPath;

    public AppConfig Load()
    {
        if (!File.Exists(_configPath))
        {
            return new AppConfig();
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("selectedDeviceIds", out var selectedIdsElement) &&
                selectedIdsElement.ValueKind == JsonValueKind.Array)
            {
                config.SetSelectedDeviceIds(selectedIdsElement
                    .EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? string.Empty));
                return config;
            }

            var migratedIds = new List<string>();
            if (root.TryGetProperty("deviceAId", out var deviceAElement) && deviceAElement.ValueKind == JsonValueKind.String)
            {
                migratedIds.Add(deviceAElement.GetString() ?? string.Empty);
            }

            if (root.TryGetProperty("deviceBId", out var deviceBElement) && deviceBElement.ValueKind == JsonValueKind.String)
            {
                migratedIds.Add(deviceBElement.GetString() ?? string.Empty);
            }

            config.SetSelectedDeviceIds(migratedIds);
            return config;
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(_configPath, json);
    }
}
