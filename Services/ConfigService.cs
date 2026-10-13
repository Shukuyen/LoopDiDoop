using System.IO;
using System.Text.Json;
using LoopDiDoop.Models;

namespace LoopDiDoop.Services;

public class ConfigService
{
    private static readonly string AppFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LoopDiDoop"
    );

    private static readonly string ConfigPath = Path.Combine(AppFolder, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppConfig Current { get; private set; }

    public ConfigService()
    {
        Current = Load();
    }

    public AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config != null)
                {
                    Current = config;
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load config: {ex.Message}");
        }

        Current = new AppConfig();
        return Current;
    }

    public void Save(AppConfig config)
    {
        try
        {
            if (!Directory.Exists(AppFolder))
            {
                Directory.CreateDirectory(AppFolder);
            }

            Current = config;
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
        }
    }

    public bool HasConfiguredHotkey => Current.Hotkey != null;
}
