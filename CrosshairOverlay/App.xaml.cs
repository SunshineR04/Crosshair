using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using CrosshairOverlay.Helpers;
using CrosshairOverlay.Rendering;
using CrosshairOverlay.Services;
using CrosshairOverlay.ViewModels;

namespace CrosshairOverlay;

public partial class App : System.Windows.Application
{
    private NotifyIcon? _notifyIcon;
    private MainWindow? _mainWindow;
    private Window? _hotkeyWindow;
    private HwndSource? _hotkeySource;
    private IntPtr _hotkeyHwnd;
    private OverlayHost? _overlayHost;
    private MainViewModel? _viewModel;
    private SettingsService? _settingsService;
    private AppServiceServer? _appServiceServer;
    private ToolStripMenuItem? _toggleMenuItem;

    private int _toggleHotkeyId;
    private int _settingsHotkeyId;
    private bool _hotkeyOk;

    protected override void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);

        LogService.Info($"CrosshairOverlay starting (admin={AdminElevationHelper.IsRunningAsAdmin()})");

        DispatcherUnhandledException += (s, ex) =>
        {
            if (ex.Exception is COMException comEx && comEx.ErrorCode == unchecked((int)0x80263001))
            {
                LogService.Warn("DWM composition not ready (0x80263001), suppressed");
                ex.Handled = true;
            }
        };

        _settingsService = new SettingsService();
        var renderer = new CrosshairRenderer();
        var profile = _settingsService.Load();
        _settingsService.SaveForWidget(profile);

        var useForceTopmost = profile.ExclusiveFullscreenMode && AdminElevationHelper.IsRunningAsAdmin();
        if (profile.ExclusiveFullscreenMode && !AdminElevationHelper.IsRunningAsAdmin())
        {
            profile.ExclusiveFullscreenMode = false;
            _settingsService.Save(profile);
        }
        var overlayHost = new OverlayHost(renderer, profile, useForceTopmost);

        _appServiceServer = new AppServiceServer();
        _ = _appServiceServer.InitializeAsync();

        var hotkeyWindow = new Window
        {
            Width = 1, Height = 1,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            ResizeMode = ResizeMode.NoResize,
            Opacity = 0
        };
        hotkeyWindow.Show();
        _hotkeyWindow = hotkeyWindow;

        var interopHelper = new WindowInteropHelper(hotkeyWindow);
        _hotkeyHwnd = interopHelper.Handle;
        _hotkeySource = HwndSource.FromHwnd(_hotkeyHwnd);
        _hotkeySource.AddHook(HwndHook);

        var viewModel = new MainViewModel(overlayHost, _settingsService, profile, _appServiceServer);
        _viewModel = viewModel;
        _overlayHost = overlayHost;
        _mainWindow = new MainWindow(viewModel);

        viewModel.OverlayError += OnOverlayError;
        viewModel.RestartRequested += OnRestartRequested;

        RegisterHotkeys(_hotkeyHwnd);
        viewModel.HotkeyRegistered = _hotkeyOk;

        CreateTrayIcon();

        if (profile.IsVisible)
        {
            viewModel.IsVisible = true;
        }

        _mainWindow.Show();
        _mainWindow.Activate();

        if (!_hotkeyOk)
        {
            _notifyIcon?.ShowBalloonTip(3000, "Crosshair Overlay",
                "快捷键注册失败（可能被其他程序占用）。请使用托盘菜单切换准心。", ToolTipIcon.Warning);
        }
    }

    private void OnOverlayError(string msg)
    {
        LogService.Error($"Overlay error: {msg}");
        _notifyIcon?.ShowBalloonTip(3000, "Crosshair Error", msg, ToolTipIcon.Error);
    }

    private void OnRestartRequested()
    {
        NativeMethods.UnregisterHotKey(_hotkeyHwnd, _toggleHotkeyId);
        NativeMethods.UnregisterHotKey(_hotkeyHwnd, _settingsHotkeyId);

        try
        {
            _viewModel?.SaveSettings();
            _overlayHost?.Hide();
            LogService.Info("Restarting as admin for Real Overlay mode");
            AdminElevationHelper.RestartAsAdmin();
            Current.Shutdown();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            RegisterHotkeys(_hotkeyHwnd);
            _viewModel?.ResetExclusiveFullscreen();
            _notifyIcon?.ShowBalloonTip(3000, "提示",
                "需要管理员权限才能启用独占全屏模式。请重新勾选并以管理员身份运行。", ToolTipIcon.Info);
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            handled = true;

            if (id == _toggleHotkeyId)
            {
                _viewModel?.ToggleOverlay();
                UpdateToggleMenu(_viewModel?.IsVisible ?? false);
            }
            else if (id == _settingsHotkeyId)
            {
                _mainWindow?.Show();
                _mainWindow?.Activate();
            }
        }
        return IntPtr.Zero;
    }

    private void RegisterHotkeys(IntPtr hwnd)
    {
        _toggleHotkeyId = 1;
        _settingsHotkeyId = 2;

        _hotkeyOk = true;

        if (!NativeMethods.RegisterHotKey(hwnd, _toggleHotkeyId,
                NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, NativeMethods.VK_X))
        {
            _hotkeyOk = false;
        }

        if (!NativeMethods.RegisterHotKey(hwnd, _settingsHotkeyId,
                NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, NativeMethods.VK_OEM_3))
        {
            _hotkeyOk = false;
        }
    }

    private void CreateTrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "Crosshair Overlay"
        };

        _notifyIcon.DoubleClick += (_, _) =>
        {
            _mainWindow?.Show();
            _mainWindow?.Activate();
        };

        var menu = new ContextMenuStrip();

        _toggleMenuItem = new ToolStripMenuItem("显示准心");
        _toggleMenuItem.Click += (_, _) =>
        {
            _viewModel?.ToggleOverlay();
            UpdateToggleMenu(_viewModel?.IsVisible ?? false);
        };
        menu.Items.Add(_toggleMenuItem);

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("设置", null, (_, _) =>
        {
            _mainWindow?.Show();
            _mainWindow?.Activate();
        });

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("退出", null, (_, _) => ShutdownApplication());

        _notifyIcon.ContextMenuStrip = menu;
    }

    private void UpdateToggleMenu(bool visible)
    {
        if (_toggleMenuItem != null)
            _toggleMenuItem.Text = visible ? "隐藏准心" : "显示准心";
    }

    private void ShutdownApplication()
    {
        Current.Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LogService.Info("CrosshairOverlay shutting down");
        _viewModel?.SaveSettings();
        _viewModel?.Dispose();

        NativeMethods.UnregisterHotKey(_hotkeyHwnd, _toggleHotkeyId);
        NativeMethods.UnregisterHotKey(_hotkeyHwnd, _settingsHotkeyId);
        _hotkeySource?.RemoveHook(HwndHook);
        _hotkeySource?.Dispose();
        _hotkeyWindow?.Close();

        _overlayHost?.Dispose();
        _appServiceServer?.Dispose();

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
        base.OnExit(e);
    }
}
