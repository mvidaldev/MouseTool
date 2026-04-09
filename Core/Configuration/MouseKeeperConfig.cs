using System.IO;
using System.Text.Json;

namespace MouseTool;

internal sealed class MouseKeeperConfig
{
    public bool CheckForUpdatesOnStartup { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public bool LoggingEnabled { get; set; }
    public bool StartWithWindows { get; set; }
    public string SelectedLanguage { get; set; } = string.Empty;
    public string UpdateManifestUrl { get; set; } = string.Empty;
    public bool AllowMouseOnTouchscreen { get; set; }
    public string PrimaryMonitorDeviceName { get; set; } = string.Empty;
    public string TouchMonitorDeviceName { get; set; } = string.Empty;
    public bool RestoreImmediatelyOnTouchRelease { get; set; }
    public SerializablePoint LastPrimaryMousePosition { get; set; } = new();

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true
    };

    public static MouseKeeperConfig LoadOrCreate(string path)
    {
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<MouseKeeperConfig>(json, JsonOptions);
            if (loaded is not null)
            {
                return loaded;
            }
        }

        var config = new MouseKeeperConfig
        {
            CheckForUpdatesOnStartup = true,
            LoggingEnabled = false,
            SelectedLanguage = string.Empty,
            UpdateManifestUrl = string.Empty
        };
        File.WriteAllText(path, JsonSerializer.Serialize(config, JsonOptions));
        return config;
    }
}

