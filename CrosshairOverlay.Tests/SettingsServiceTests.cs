using System.IO;
using CrosshairOverlay.Models;
using CrosshairOverlay.Services;
using Xunit;

namespace CrosshairOverlay.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void SaveAndLoad_UsesInjectedSettingsDirectory()
    {
        var root = CreateTempDirectory();
        try
        {
            var service = new SettingsService(root, Path.Combine(root, "widget"));
            var expected = new CrosshairProfile
            {
                Style = CrosshairStyle.CircleDot,
                Color = "#123456",
                Size = 48,
                IsVisible = false
            };

            service.Save(expected);
            var actual = service.Load();

            Assert.Equal(expected.Style, actual.Style);
            Assert.Equal(expected.Color, actual.Color);
            Assert.Equal(expected.Size, actual.Size);
            Assert.False(actual.IsVisible);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveForWidget_WritesToInjectedWidgetDirectory()
    {
        var root = CreateTempDirectory();
        try
        {
            var widgetDirectory = Path.Combine(root, "widget");
            var service = new SettingsService(root, widgetDirectory);
            service.SaveForWidget(new CrosshairProfile { IsVisible = false });

            var widgetFile = Path.Combine(widgetDirectory, "widget_settings.json");
            Assert.True(File.Exists(widgetFile));
            Assert.Contains("\"IsVisible\": false", File.ReadAllText(widgetFile));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "CrosshairOverlayTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
