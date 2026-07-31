using System.Runtime.InteropServices;
using CrosshairOverlay.Helpers;
using CrosshairOverlay.Models;
using CrosshairOverlay.Services;
using SkiaSharp;
using Timer = System.Timers.Timer;

namespace CrosshairOverlay.Rendering;

public class OverlayHost : IOverlayHost
{
    /// <summary>
    /// ForceTopmost 模式下保持窗口置顶的定时器间隔（毫秒）。
    /// 500ms 在性能开销和抢占恢复速度之间取得平衡。
    /// </summary>
    private const int KeepTopmostIntervalMs = 500;
    private IntPtr _hwnd;
    private readonly CrosshairRenderer _renderer;
    private CrosshairProfile _profile;
    private bool _visible;
    private int _screenX;
    private int _screenY;
    private int _screenWidth;
    private int _screenHeight;
    private IntPtr _hBitmap;
    private IntPtr _memDc;
    private IntPtr _previousBitmap;
    private bool _forceTopmost;
    private Timer? _keepTopmostTimer;
    private float _dpiScale = 1.0f;

    private const string WindowClassName = "CrosshairOverlayWindow";

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private static readonly WndProcDelegate _windowProcDelegate = OverlayWndProc;

    public bool IsVisible => _visible;
    public IntPtr Handle => _hwnd;

    public OverlayHost(CrosshairRenderer renderer, CrosshairProfile profile, bool forceTopmost = false)
    {
        _renderer = renderer;
        _profile = CrosshairProfileRules.Sanitize(profile);
        _forceTopmost = forceTopmost;
        RegisterWindowClass();
    }

    private static void RegisterWindowClass()
    {
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_windowProcDelegate),
            hInstance = NativeMethods.GetModuleHandle(null),
            hIcon = IntPtr.Zero,
            hCursor = IntPtr.Zero,
            hbrBackground = IntPtr.Zero,
            lpszClassName = WindowClassName
        };

        var atom = RegisterClassEx(ref wc);
        if (atom == 0)
        {
            var err = Marshal.GetLastWin32Error();
            if (err != 1410)
                throw new InvalidOperationException($"Failed to register window class. Error: {err}");
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private static IntPtr OverlayWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_NCHITTEST)
            return new IntPtr(NativeMethods.HTTRANSPARENT);

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    public void SetProfile(CrosshairProfile profile)
    {
        _profile = CrosshairProfileRules.Sanitize(profile);
        if (_visible)
            RenderAndUpdate();
    }

    public void SetForceTopmost(bool forceTopmost)
    {
        _forceTopmost = forceTopmost;
        if (!_visible)
            return;

        if (_forceTopmost)
        {
            NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            StartKeepTopmostTimer();
        }
        else
        {
            StopKeepTopmostTimer();
        }
    }

    public bool Show()
    {
        if (_visible) return true;

        // 优先使用鼠标所在屏幕（通常是游戏屏幕），回退到主屏幕
        var cursorPos = System.Windows.Forms.Cursor.Position;
        var screen = Screen.AllScreens.FirstOrDefault(s => s.Bounds.Contains(cursorPos))
                     ?? Screen.PrimaryScreen
                     ?? Screen.AllScreens.FirstOrDefault();
        if (screen == null) return false;

        _screenX = screen.Bounds.X;
        _screenY = screen.Bounds.Y;
        _screenWidth = screen.Bounds.Width;
        _screenHeight = screen.Bounds.Height;

        // 获取主显示器 DPI 缩放因子，确保准心在高 DPI 屏幕上保持正确的视觉大小
        var desktopHwnd = NativeMethods.GetDesktopWindow();
        var dpi = NativeMethods.GetDpiForWindow(desktopHwnd);
        _dpiScale = dpi > 0 ? dpi / (float)NativeMethods.DefaultDpi : 1.0f;

        var exStyle = NativeMethods.WS_EX_LAYERED
                      | NativeMethods.WS_EX_TRANSPARENT
                      | NativeMethods.WS_EX_TOOLWINDOW
                      | NativeMethods.WS_EX_NOACTIVATE;

        _hwnd = NativeMethods.CreateWindowEx(
            (uint)exStyle,
            WindowClassName,
            "CrosshairOverlay",
            0,
            _screenX, _screenY,
            _screenWidth, _screenHeight,
            IntPtr.Zero, IntPtr.Zero,
            NativeMethods.GetModuleHandle(null),
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            var err = Marshal.GetLastWin32Error();
            LogService.Error($"CreateWindowEx failed: {err}");
            return false;
        }

        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOWNOACTIVATE);

        _visible = true;
        RenderAndUpdate();

        if (_forceTopmost)
            StartKeepTopmostTimer();

        return true;
    }

    private void StartKeepTopmostTimer()
    {
        if (_keepTopmostTimer != null)
            return;

        _keepTopmostTimer = new Timer(KeepTopmostIntervalMs);
        _keepTopmostTimer.Elapsed += (_, _) =>
        {
            if (_hwnd != IntPtr.Zero)
            {
                NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST,
                    0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            }
        };
        _keepTopmostTimer.AutoReset = true;
        _keepTopmostTimer.Start();
    }

    private void StopKeepTopmostTimer()
    {
        _keepTopmostTimer?.Stop();
        _keepTopmostTimer?.Dispose();
        _keepTopmostTimer = null;
    }

    public void Hide()
    {
        if (!_visible) return;

        StopKeepTopmostTimer();
        CleanupGdi();
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
        _visible = false;
    }

    public void Toggle()
    {
        if (_visible)
            Hide();
        else
            Show();
    }

    public void RenderAndUpdate()
    {
        if (!_visible || _hwnd == IntPtr.Zero) return;

        CleanupGdi();

        SKBitmap bitmap;
        try
        {
            bitmap = _renderer.Render(_profile, _dpiScale);
        }
        catch (Exception ex)
        {
            LogService.Error($"Crosshair render failed: {ex.Message}");
            return;
        }

        using (bitmap)
        {
            var screenDc = NativeMethods.GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero)
            {
                LogService.Error("GetDC failed");
                return;
            }

            try
            {
                _memDc = NativeMethods.CreateCompatibleDC(screenDc);
                if (_memDc == IntPtr.Zero)
                {
                    LogService.Error("CreateCompatibleDC failed");
                    return;
                }

                var bmi = new NativeMethods.BITMAPINFO
                {
                    bmiHeader = new NativeMethods.BITMAPINFOHEADER
                    {
                        biSize = (uint)Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                        biWidth = bitmap.Width,
                        biHeight = -bitmap.Height,
                        biPlanes = 1,
                        biBitCount = 32,
                        biCompression = 0
                    }
                };

                IntPtr bits;
                _hBitmap = NativeMethods.CreateDIBSection(_memDc, ref bmi, 0, out bits, IntPtr.Zero, 0);

                if (_hBitmap == IntPtr.Zero || bits == IntPtr.Zero)
                {
                    LogService.Error($"CreateDIBSection failed: {Marshal.GetLastWin32Error()}");
                    CleanupGdi();
                    return;
                }

                Marshal.Copy(bitmap.Bytes, 0, bits, bitmap.Bytes.Length);

                _previousBitmap = NativeMethods.SelectObject(_memDc, _hBitmap);
                if (_previousBitmap == IntPtr.Zero)
                {
                    LogService.Error("SelectObject failed while selecting the DIB section");
                    CleanupGdi();
                    return;
                }

                var blend = new NativeMethods.BLENDFUNCTION
                {
                    BlendOp = NativeMethods.AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = NativeMethods.AC_SRC_ALPHA
                };

                var pptDst = new NativeMethods.POINT
                {
                    x = _screenX + (_screenWidth - bitmap.Width) / 2,
                    y = _screenY + (_screenHeight - bitmap.Height) / 2
                };

                var psize = new NativeMethods.SIZE
                {
                    cx = bitmap.Width,
                    cy = bitmap.Height
                };

                var pptSrc = new NativeMethods.POINT { x = 0, y = 0 };

                if (!NativeMethods.UpdateLayeredWindow(
                    _hwnd, IntPtr.Zero, ref pptDst, ref psize, _memDc, ref pptSrc, 0, ref blend, NativeMethods.ULW_ALPHA))
                {
                    LogService.Error($"UpdateLayeredWindow failed: {Marshal.GetLastWin32Error()}");
                }
            }
            catch (Exception ex)
            {
                LogService.Error("GDI overlay update failed", ex);
                CleanupGdi();
            }
            finally
            {
                NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
    }

    private void CleanupGdi()
    {
        if (_memDc != IntPtr.Zero && _previousBitmap != IntPtr.Zero)
        {
            NativeMethods.SelectObject(_memDc, _previousBitmap);
            _previousBitmap = IntPtr.Zero;
        }

        if (_hBitmap != IntPtr.Zero)
        {
            if (!NativeMethods.DeleteObject(_hBitmap))
                LogService.Warn("DeleteObject failed while cleaning overlay bitmap");
            _hBitmap = IntPtr.Zero;
        }
        if (_memDc != IntPtr.Zero)
        {
            if (!NativeMethods.DeleteDC(_memDc))
                LogService.Warn("DeleteDC failed while cleaning overlay memory DC");
            _memDc = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        StopKeepTopmostTimer();
        Hide();
    }
}
