using CrosshairOverlay.Models;
using CrosshairOverlay.Rendering;
using CrosshairOverlay.Services;
using CrosshairOverlay.ViewModels;
using Xunit;

namespace CrosshairOverlay.Tests;

public class MainViewModelTests
{
    [Fact]
    public void ResetExclusiveFullscreen_DisablesTopmostModeOnOverlayHost()
    {
        var overlay = new FakeOverlayHost();
        var profile = new CrosshairProfile { ExclusiveFullscreenMode = true };
        var viewModel = new MainViewModel(overlay, new FakeSettingsService(), profile);
        try
        {
            viewModel.ResetExclusiveFullscreen();

            Assert.False(profile.ExclusiveFullscreenMode);
            Assert.False(overlay.LastForceTopmost);
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    [Fact]
    public void PropertyChanges_UpdateProfileAndOverlay()
    {
        var overlay = new FakeOverlayHost();
        var settings = new FakeSettingsService();
        var profile = new CrosshairProfile { IsVisible = false };
        var viewModel = new MainViewModel(overlay, settings, profile);
        try
        {
            viewModel.SelectedStyle = "圆环";
            viewModel.ColorHex = "#123456";
            viewModel.OutlineColorHex = "#654321";
            viewModel.SelectedStyle = "unknown";
            viewModel.ColorHex = "invalid";
            viewModel.OutlineColorHex = "invalid";
            viewModel.ThicknessValue = 5;
            viewModel.SizeValue = 48;
            viewModel.GapValue = 8;
            viewModel.DotSizeValue = 9;
            viewModel.OpacityValue = 0.75;
            viewModel.OutlineEnabled = false;
            viewModel.OutlineThicknessValue = 4;
            viewModel.IsVisible = true;
            viewModel.IsVisible = false;
            viewModel.HotkeyRegistered = false;

            Assert.Equal(CrosshairStyle.Circle, profile.Style);
            Assert.Equal("#123456", profile.Color);
            Assert.Equal("#654321", profile.OutlineColor);
            Assert.Equal(5, profile.Thickness);
            Assert.Equal(48, profile.Size);
            Assert.Equal(8, profile.Gap);
            Assert.Equal(9, profile.DotSize);
            Assert.Equal(0.75, profile.Opacity);
            Assert.False(profile.OutlineEnabled);
            Assert.Equal(4, profile.OutlineThickness);
            Assert.False(profile.IsVisible);
            Assert.True(overlay.SetProfileCount > 0);
            Assert.Equal("快捷键注册失败（可能被其他程序占用），请使用托盘菜单操作", viewModel.HotkeyStatusText);
        }
        finally
        {
            viewModel.Dispose();
        }

        Assert.True(settings.SaveForWidgetCount > 0);
    }

    [Fact]
    public void IsVisible_ShowFailureResetsStateAndRaisesError()
    {
        var overlay = new FakeOverlayHost { ShowResult = false };
        var viewModel = new MainViewModel(
            overlay,
            new FakeSettingsService(),
            new CrosshairProfile { IsVisible = false });
        string? error = null;
        viewModel.OverlayError += message => error = message;

        try
        {
            viewModel.IsVisible = true;

            Assert.False(viewModel.IsVisible);
            Assert.Equal("CreateWindowEx 失败", error);
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    private sealed class FakeOverlayHost : IOverlayHost
    {
        public bool IsVisible => false;
        public IntPtr Handle => IntPtr.Zero;
        public bool LastForceTopmost { get; private set; }
        public bool ShowResult { get; set; } = true;
        public int SetProfileCount { get; private set; }

        public void SetProfile(CrosshairProfile profile) => SetProfileCount++;
        public void SetForceTopmost(bool forceTopmost) => LastForceTopmost = forceTopmost;
        public bool Show() => ShowResult;
        public void Hide() { }
        public void Toggle() { }
        public void Dispose() { }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public int SaveForWidgetCount { get; private set; }

        public CrosshairProfile Load() => new CrosshairProfile();
        public void Save(CrosshairProfile profile) { }
        public void SaveForWidget(CrosshairProfile profile) => SaveForWidgetCount++;
    }
}
