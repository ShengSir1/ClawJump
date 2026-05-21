using ClawJump.Avalonia.Models;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace ClawJump.Avalonia.Services;

public static class ConfigService
{
    public static string ConfigDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClawJump");

    public static string ConfigFilePath =>
        Path.Combine(ConfigDirectory, "config.json");

    public static AppConfig Load()
    {
        EnsureConfigFile();

        try
        {
            var json = File.ReadAllText(ConfigFilePath);

            var config = JsonSerializer.Deserialize<AppConfig>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return config ?? new AppConfig();
        }
        catch
        {
            var defaultConfig = new AppConfig();
            Save(defaultConfig);
            return defaultConfig;
        }
    }

    public static void Save(AppConfig config)
    {
        Directory.CreateDirectory(ConfigDirectory);

        var json = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            });

        File.WriteAllText(ConfigFilePath, json);
    }

    private static void EnsureConfigFile()
    {
        Directory.CreateDirectory(ConfigDirectory);

        if (!File.Exists(ConfigFilePath))
        {
            Save(new AppConfig());
        }
    }
}