using System.Text.Json;
using DailyWords.Models;

namespace DailyWords.Services;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public AppConfig Load()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.RootDirectory);
            if (!File.Exists(AppPaths.ConfigFilePath))
            {
                return CreateDefault();
            }

            var json = File.ReadAllText(AppPaths.ConfigFilePath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? CreateDefault();
            config.Normalize();
            return config;
        }
        catch
        {
            return CreateDefault();
        }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(AppPaths.RootDirectory);
        config.Normalize();
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(AppPaths.ConfigFilePath, json);
    }

    public AppConfig CreateDefault()
    {
        var config = new AppConfig();
        config.Normalize();
        return config;
    }
}
