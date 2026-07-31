using System.IO;
using System.Text.Json;
using System.Text;
using CrosshairOverlay.Models;

namespace CrosshairOverlay.Services;

public class SettingsService : ISettingsService
{
    public static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CrosshairOverlay");

    private static readonly string DefaultSettingsFile = Path.Combine(AppDataDir, "settings.json");

    private static readonly string WidgetLocalStateDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Packages", "CrosshairOverlayWidget_ttvw7j9e3pmmp", "LocalState");

    private static readonly string DefaultWidgetSettingsFile = Path.Combine(WidgetLocalStateDir, "widget_settings.json");

    private readonly string _appDataDir;
    private readonly string _settingsFile;
    private readonly string _widgetLocalStateDir;
    private readonly string _widgetSettingsFile;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public SettingsService(string? appDataDir = null, string? widgetLocalStateDir = null)
    {
        _appDataDir = appDataDir ?? AppDataDir;
        _settingsFile = appDataDir == null ? DefaultSettingsFile : Path.Combine(_appDataDir, "settings.json");
        _widgetLocalStateDir = widgetLocalStateDir ?? WidgetLocalStateDir;
        _widgetSettingsFile = widgetLocalStateDir == null
            ? DefaultWidgetSettingsFile
            : Path.Combine(_widgetLocalStateDir, "widget_settings.json");
    }

    public CrosshairProfile Load()
    {
        try
        {
            if (!File.Exists(_settingsFile))
                return new CrosshairProfile();

            var json = File.ReadAllText(_settingsFile);
            var profile = JsonSerializer.Deserialize<CrosshairProfile>(json, JsonOptions) ?? new CrosshairProfile();
            return CrosshairProfileRules.Sanitize(profile);
        }
        catch (Exception ex)
        {
            LogService.Error("Settings load failed, using defaults", ex);
            return new CrosshairProfile();
        }
    }

    public void Save(CrosshairProfile profile)
    {
        try
        {
            var sanitized = CrosshairProfileRules.Sanitize(profile);
            var json = JsonSerializer.Serialize(sanitized, JsonOptions);
            WriteAtomic(_settingsFile, json, _appDataDir);
        }
        catch (Exception ex)
        {
            LogService.Error("Settings save failed", ex);
        }
    }

    public void SaveForWidget(CrosshairProfile profile)
    {
        try
        {
            var sanitized = CrosshairProfileRules.Sanitize(profile);
            var json = JsonSerializer.Serialize(sanitized, JsonOptions);
            WriteAtomic(_widgetSettingsFile, json, _widgetLocalStateDir);
        }
        catch (Exception ex)
        {
            LogService.Warn($"SaveForWidget failed: {ex.Message}");
        }
    }

    private static void WriteAtomic(string filePath, string content, string directory)
    {
        Directory.CreateDirectory(directory);
        var tempPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                options: FileOptions.WriteThrough))
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
                {
                    writer.Write(content);
                    writer.Flush();
                }

                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
