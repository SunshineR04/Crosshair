using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CrosshairOverlay.Models;
using CrosshairOverlay.Rendering;
using CrosshairOverlay.Services;

namespace CrosshairOverlay.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly OverlayHost _overlayHost;
    private readonly SettingsService _settingsService;
    private readonly AppServiceServer? _appServiceServer;

    private CrosshairProfile _profile;
    private readonly System.Timers.Timer _saveDebounceTimer;
    private bool _disposed;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? CloseRequested;
    public event Action? RestartRequested;

    public CrosshairProfile Profile
    {
        get => _profile;
        set
        {
            _profile = value;
            OnPropertyChanged();
        }
    }

    private string _openSettingsHotkey = "Alt + `";
    public string OpenSettingsHotkey
    {
        get => _openSettingsHotkey;
        set { _openSettingsHotkey = value; OnPropertyChanged(); }
    }

    private string _toggleCrosshairHotkey = "Alt + X";
    public string ToggleCrosshairHotkey
    {
        get => _toggleCrosshairHotkey;
        set { _toggleCrosshairHotkey = value; OnPropertyChanged(); }
    }

    private bool _isVisible;
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            _isVisible = value;
            OnPropertyChanged();
            if (value)
            {
                if (!_overlayHost.Show())
                {
                    _isVisible = false;
                    OnPropertyChanged();
                    OverlayError?.Invoke("CreateWindowEx 失败");
                }
            }
            else
            {
                _overlayHost.Hide();
            }
        }
    }

    public event Action<string>? OverlayError;

    private bool _startupEnabled;
    public bool StartupEnabled
    {
        get => _startupEnabled;
        set
        {
            _startupEnabled = value;
            OnPropertyChanged();
            StartupService.IsEnabled = value;
        }
    }

    public List<string> CrosshairStyles { get; } = new()
    {
        "十字", "圆点", "十字+圆点", "圆环", "圆环+圆点", "实心轮廓"
    };

    private static readonly CrosshairStyle[] StyleMapping =
    {
        CrosshairStyle.Cross, CrosshairStyle.Dot, CrosshairStyle.CrossDot,
        CrosshairStyle.Circle, CrosshairStyle.CircleDot, CrosshairStyle.Outline
    };

    private string _selectedStyle;
    public string SelectedStyle
    {
        get => _selectedStyle;
        set
        {
            if (_selectedStyle == value) return;
            _selectedStyle = value;
            OnPropertyChanged();
            var idx = CrosshairStyles.IndexOf(value);
            if (idx < 0) return;
            Profile.Style = StyleMapping[idx];
            ApplyProfile();
        }
    }

    private string _colorHex;
    public string ColorHex
    {
        get => _colorHex;
        set
        {
            _colorHex = value;
            OnPropertyChanged();
            try
            {
                _ = System.Windows.Media.ColorConverter.ConvertFromString(value);
                Profile.Color = value;
                ApplyProfile();
            }
            catch
            {
                _colorHex = Profile.Color;
                OnPropertyChanged();
            }
        }
    }

    public int ThicknessValue
    {
        get => Profile.Thickness;
        set { Profile.Thickness = value; OnPropertyChanged(); ApplyProfile(); }
    }

    public int SizeValue
    {
        get => Profile.Size;
        set { Profile.Size = value; OnPropertyChanged(); ApplyProfile(); }
    }

    public int GapValue
    {
        get => Profile.Gap;
        set { Profile.Gap = value; OnPropertyChanged(); ApplyProfile(); }
    }

    public int DotSizeValue
    {
        get => Profile.DotSize;
        set { Profile.DotSize = value; OnPropertyChanged(); ApplyProfile(); }
    }

    public double OpacityValue
    {
        get => Profile.Opacity;
        set { Profile.Opacity = System.Math.Round(value, 2); OnPropertyChanged(); ApplyProfile(); }
    }

    public bool OutlineEnabled
    {
        get => Profile.OutlineEnabled;
        set { Profile.OutlineEnabled = value; OnPropertyChanged(); ApplyProfile(); }
    }

    private string _outlineColorHex = "#000000";
    public string OutlineColorHex
    {
        get => _outlineColorHex;
        set
        {
            _outlineColorHex = value;
            OnPropertyChanged();
            try
            {
                _ = System.Windows.Media.ColorConverter.ConvertFromString(value);
                Profile.OutlineColor = value;
                ApplyProfile();
            }
            catch
            {
                _outlineColorHex = Profile.OutlineColor;
                OnPropertyChanged();
            }
        }
    }

    public int OutlineThicknessValue
    {
        get => Profile.OutlineThickness;
        set { Profile.OutlineThickness = value; OnPropertyChanged(); ApplyProfile(); }
    }

    private bool _useExclusiveFullscreen;
    public bool UseExclusiveFullscreen
    {
        get => _useExclusiveFullscreen;
        set
        {
            _useExclusiveFullscreen = value;
            OnPropertyChanged();
            Profile.ExclusiveFullscreenMode = value;

            if (value && !AdminElevationHelper.IsRunningAsAdmin())
            {
                RestartRequested?.Invoke();
            }
        }
    }

    public bool IsAdmin => AdminElevationHelper.IsRunningAsAdmin();

    public void ResetExclusiveFullscreen()
    {
        _useExclusiveFullscreen = false;
        Profile.ExclusiveFullscreenMode = false;
        OnPropertyChanged(nameof(UseExclusiveFullscreen));
    }

    public string AdminStatusText =>
        IsAdmin
            ? "已获得管理员权限，独占全屏可用"
            : "普通权限，仅支持窗口化全屏。启用「独占全屏兼容」将自动重启为管理员。";

    public ICommand ToggleVisibleCommand { get; }
    public ICommand MinimizeCommand { get; }
    public ICommand ExitCommand { get; }

    public MainViewModel(OverlayHost overlayHost, SettingsService settingsService, CrosshairProfile profile, AppServiceServer? appServiceServer = null)
    {
        _overlayHost = overlayHost;
        _settingsService = settingsService;
        _appServiceServer = appServiceServer;

        _profile = profile;

        var styleIdx = Array.IndexOf(StyleMapping, _profile.Style);
        _selectedStyle = styleIdx >= 0 ? CrosshairStyles[styleIdx] : CrosshairStyles[0];
        _colorHex = _profile.Color;
        _outlineColorHex = _profile.OutlineColor;
        _isVisible = _profile.IsVisible;
        _useExclusiveFullscreen = _profile.ExclusiveFullscreenMode;
        _startupEnabled = StartupService.IsEnabled;

        ToggleVisibleCommand = new RelayCommand(() => IsVisible = !IsVisible);
        MinimizeCommand = new RelayCommand(() => CloseRequested?.Invoke());
        ExitCommand = new RelayCommand(() => System.Windows.Application.Current.Shutdown());

        _saveDebounceTimer = new System.Timers.Timer(400) { AutoReset = false };
        _saveDebounceTimer.Elapsed += (_, _) =>
            System.Windows.Application.Current.Dispatcher.Invoke(() => SaveSettings());
    }

    public void SaveSettings()
    {
        if (!_disposed)
            _saveDebounceTimer.Stop();
        _profile.IsVisible = _isVisible;
        _settingsService.Save(_profile);
    }

    public void ToggleOverlay()
    {
        IsVisible = !IsVisible;
    }

    private void ApplyProfile()
    {
        _overlayHost.SetProfile(_profile);
        _ = _appServiceServer?.PushProfile(_profile);
        _saveDebounceTimer.Stop();
        _saveDebounceTimer.Start();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _saveDebounceTimer.Stop();
        _saveDebounceTimer.Dispose();
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
}
