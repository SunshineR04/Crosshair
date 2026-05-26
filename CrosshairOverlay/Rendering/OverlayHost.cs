using System.Runtime.InteropServices;
using CrosshairOverlay.Helpers;
using CrosshairOverlay.Models;
using SkiaSharp;
using Timer = System.Timers.Timer;

namespace CrosshairOverlay.Rendering;

public class OverlayHost : IDisposable
{
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
    private readonly bool _forceTopmost;
    private Timer? _keepTopmostTimer;

    private const string WindowClassName = "CrosshairOverlayWindow";

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private static readonly WndProcDelegate _defWndProcDelegate = DefWindowProc;

    public bool IsVisible => _visible;
    public IntPtr Handle => _hwnd;

    public OverlayHost(CrosshairRenderer renderer, CrosshairProfile profile, bool forceTopmost = false)
    {
        _renderer = renderer;
        _profile = profile;
        _forceTopmost = forceTopmost;
        RegisterWindowClass();
    }

    private static void RegisterWindowClass()
    {
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_defWndProcDelegate),
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

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
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
        _profile = profile;
        if (_visible)
            RenderAndUpdate();
    }

    public bool Show()
    {
        if (_visible) return true;

        var screen = Screen.PrimaryScreen ?? Screen.AllScreens.FirstOrDefault();
        if (screen == null) return false;

        _screenX = screen.Bounds.X;
        _screenY = screen.Bounds.Y;
        _screenWidth = screen.Bounds.Width;
        _screenHeight = screen.Bounds.Height;

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
            System.Diagnostics.Debug.WriteLine($"[CrosshairOverlay] CreateWindowEx failed: {err}");
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
        _keepTopmostTimer = new Timer(500);
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

        using var bitmap = _renderer.Render(_profile);

        var screenDc = NativeMethods.GetDC(IntPtr.Zero);
        _memDc = NativeMethods.CreateCompatibleDC(screenDc);

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

        if (_hBitmap != IntPtr.Zero && bits != IntPtr.Zero)
        {
            Marshal.Copy(bitmap.Bytes, 0, bits, bitmap.Bytes.Length);
        }

        if (_hBitmap != IntPtr.Zero)
            NativeMethods.SelectObject(_memDc, _hBitmap);
        NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);

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

        NativeMethods.UpdateLayeredWindow(
            _hwnd,
            IntPtr.Zero,
            ref pptDst,
            ref psize,
            _memDc,
            ref pptSrc,
            0,
            ref blend,
            NativeMethods.ULW_ALPHA);
    }

    private void CleanupGdi()
    {
        if (_hBitmap != IntPtr.Zero)
        {
            NativeMethods.DeleteObject(_hBitmap);
            _hBitmap = IntPtr.Zero;
        }
        if (_memDc != IntPtr.Zero)
        {
            NativeMethods.DeleteDC(_memDc);
            _memDc = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        StopKeepTopmostTimer();
        Hide();
    }
}
