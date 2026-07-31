using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using CrosshairOverlay.Models;
using CrosshairOverlay.Rendering;
using CrosshairOverlay.Services;

namespace CrosshairOverlay.ViewModels;

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    /// <summary>
    /// 设置保存防抖间隔（毫秒）。滑块拖动期间避免每次变更都写磁盘。
    /// </summary>
    private const int SaveDebounceIntervalMs = 400;
    private readonly IOverlayHost _overlayHost;
    private readonly ISettingsService _settingsService;
    private readonly AppServiceServer? _appServiceServer;

    private CrosshairProfile _profile;
    private readonly DispatcherTimer _saveDebounceTimer;
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
            _profile.IsVisible = _isVisible;
            _settingsService.SaveForWidget(_profile);
        }
    }

    public event Action<string>? OverlayError;

    private bool _startupEnabled;
    public bool StartupEnabled
    {
        get => _startupEnabled;
        set
        {
            if (_startupEnabled == value) return;

            _startupEnabled = value;
            OnPropertyChanged();
            if (!StartupService.TrySetEnabled(value, out var error))
            {
                _startupEnabled = StartupService.IsEnabled;
                OnPropertyChanged();
                OverlayError?.Invoke($"开机自启设置失败: {error}");
            }
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
            var normalized = NormalizeColorHex(value);
            if (normalized != null)
            {
                Profile.Color = normalized;
                _colorHex = normalized;
                OnPropertyChanged();
                ApplyProfile();
            }
            else
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
            var normalized = NormalizeColorHex(value);
            if (normalized != null)
            {
                Profile.OutlineColor = normalized;
                _outlineColorHex = normalized;
                OnPropertyChanged();
                ApplyProfile();
            }
            else
            {
                _outlineColorHex = Profile.OutlineColor;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 将任意 WPF 支持的颜色格式（命名颜色、#RGB、#ARGB、#RRGGBB、#AARRGGBB）
    /// 规范化为 #RRGGBB 格式，确保 Widget 端 ParseColor 能正确解析。
    /// </summary>
    /// <returns>规范化后的 #RRGGBB 字符串，无效输入返回 null。</returns>
    private static string? NormalizeColorHex(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(input);
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        catch
        {
            return null;
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
            if (_useExclusiveFullscreen == value) return;

            _useExclusiveFullscreen = value;
            OnPropertyChanged();
            Profile.ExclusiveFullscreenMode = value;

            if (value && !AdminElevationHelper.IsRunningAsAdmin())
            {
                RestartRequested?.Invoke();
                return;
            }

            _overlayHost.SetForceTopmost(value);
            ApplyProfile();
        }
    }

    public bool IsAdmin => AdminElevationHelper.IsRunningAsAdmin();

    private bool _hotkeyRegistered = true;
    /// <summary>全局热键是否注册成功。失败时 UI 应提示用户使用托盘菜单。</summary>
    public bool HotkeyRegistered
    {
        get => _hotkeyRegistered;
        set { _hotkeyRegistered = value; OnPropertyChanged(); OnPropertyChanged(nameof(HotkeyStatusText)); }
    }

    public string HotkeyStatusText =>
        _hotkeyRegistered
            ? "快捷键已就绪"
            : "快捷键注册失败（可能被其他程序占用），请使用托盘菜单操作";

    public void ResetExclusiveFullscreen()
    {
        _useExclusiveFullscreen = false;
        Profile.ExclusiveFullscreenMode = false;
        _overlayHost.SetForceTopmost(false);
        OnPropertyChanged(nameof(UseExclusiveFullscreen));
    }

    public string AdminStatusText =>
        IsAdmin
            ? "已获得管理员权限，独占全屏可用"
            : "普通权限，仅支持窗口化全屏。启用「独占全屏兼容」将自动重启为管理员。";

    public ICommand ToggleVisibleCommand { get; }
    public ICommand MinimizeCommand { get; }
    public ICommand ExitCommand { get; }

    public MainViewModel(IOverlayHost overlayHost, ISettingsService settingsService, CrosshairProfile profile, AppServiceServer? appServiceServer = null)
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

        _saveDebounceTimer = new DispatcherTimer(
            DispatcherPriority.Background,
            System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(SaveDebounceIntervalMs)
        };
        _saveDebounceTimer.Tick += OnSaveDebounceTick;
    }

    private void OnSaveDebounceTick(object? sender, EventArgs e)
    {
        _saveDebounceTimer.Stop();
        SaveSettings();
    }

    public void SaveSettings()
    {
        if (_disposed) return;

        _saveDebounceTimer.Stop();
        _profile = CrosshairProfileRules.Sanitize(_profile);
        _profile.IsVisible = _isVisible;
        _settingsService.Save(_profile);
        _settingsService.SaveForWidget(_profile);
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
        _saveDebounceTimer.Tick -= OnSaveDebounceTick;
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
