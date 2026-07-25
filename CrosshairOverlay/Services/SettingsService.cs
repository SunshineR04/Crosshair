using System.IO;
using System.Text.Json;
using CrosshairOverlay.Models;

namespace CrosshairOverlay.Services;

public class SettingsService : ISettingsService
{
    public static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CrosshairOverlay");

    private static readonly string SettingsFile = Path.Combine(AppDataDir, "settings.json");

    private static readonly string WidgetLocalStateDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Packages", "CrosshairOverlayWidget_ttvw7j9e3pmmp", "LocalState");

    private static readonly string WidgetSettingsFile = Path.Combine(WidgetLocalStateDir, "widget_settings.json");

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
        catch (Exception ex)
        {
            LogService.Error("Settings load failed, using defaults", ex);
            return new CrosshairProfile();
        }
    }

    public void Save(CrosshairProfile profile)
    {
        Directory.CreateDirectory(AppDataDir);
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        File.WriteAllText(SettingsFile, json);
    }

    public void SaveForWidget(CrosshairProfile profile)
    {
        try
        {
            Directory.CreateDirectory(WidgetLocalStateDir);
            var json = JsonSerializer.Serialize(profile, JsonOptions);
            File.WriteAllText(WidgetSettingsFile, json);
        }
        catch (Exception ex)
        {
            LogService.Warn($"SaveForWidget failed: {ex.Message}");
        }
    }
}
