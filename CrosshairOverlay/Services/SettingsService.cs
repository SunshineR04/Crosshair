using System.IO;
using System.Text.Json;
using CrosshairOverlay.Models;

namespace CrosshairOverlay.Services;

public class SettingsService
{
    public static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CrosshairOverlay");

    private static readonly string SettingsFile = Path.Combine(AppDataDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public CrosshairProfile Load()
    {
        if (!File.Exists(SettingsFile))
            return new CrosshairProfile();

        try
        {
            var json = File.ReadAllText(SettingsFile);
            return JsonSerializer.Deserialize<CrosshairProfile>(json, JsonOptions) ?? new CrosshairProfile();
        }
        catch
        {
            return new CrosshairProfile();
        }
    }

    public void Save(CrosshairProfile profile)
    {
        Directory.CreateDirectory(AppDataDir);
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        File.WriteAllText(SettingsFile, json);
    }
}
