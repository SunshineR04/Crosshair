# AGENTS.md — Crosshair Overlay

## Project overview

Two-process Windows desktop crosshair overlay for FPS games:
- **WPF desktop app** (CrosshairOverlay/) — .NET 8, SkiaSharp rendering, tray icon, global hotkeys
- **UWP Game Bar Widget** (CrosshairOverlay.Widget/) — Xbox Game Bar integration for exclusive fullscreen overlays
- **MSIX package** (CrosshairOverlay.Package/) — Desktop Bridge packaging bundling both into a single installer

## Build commands

```powershell
# WPF desktop app (runs standalone, no Widget sync)
dotnet build CrosshairOverlay
dotnet run --project CrosshairOverlay
dotnet publish CrosshairOverlay -c Release -r win-x64 --self-contained true

# UWP Widget (requires VS 2019+ with UWP workload + .NET Native toolchain)
# msbuild is NOT in PATH — use VS 2019 full path:
& "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" CrosshairOverlay.Widget\CrosshairOverlay.Widget.csproj /p:Configuration=Release /p:Platform=x64

# Full MSIX package (manual process, see 'Release packaging' below)
```

The WPF project uses `net8.0-windows10.0.19041.0` — WinRT APIs require this TFM for `AppServiceConnection` and `Package.Current` (only available when running inside an MSIX package).

## Architecture

### Three overlay modes

| Mode | File | Requires | Fullscreen support |
|---|---|---|---|
| Simple Overlay | `Rendering/OverlayHost.cs` (default) | Nothing | Windowed/borderless only |
| Real Overlay | `Rendering/OverlayHost.cs` (forceTopmost=true) | Admin privileges | FSO exclusive fullscreen (DirectX) |
| Game Bar Widget | `CrosshairOverlay.Widget/` (separate UWP) | Game Bar enabled | True exclusive fullscreen (all APIs) |

### Key entry points

- `App.xaml.cs` — Application startup, hotkey registration (Win32 message-only window), tray icon, AppService initialization
- `MainWindow.xaml` — Settings UI (6 crosshair styles, color/size/opacity sliders, overlay mode toggles)
- `ViewModels/MainViewModel.cs` — MVVM binding; `ApplyProfile()` pushes settings to both OverlayHost and Widget via AppService
- `OverlayHost.cs` — Creates layered HWND transparent overlay via `CreateWindowEx` + `UpdateLayeredWindow` with SkiaSharp rendering
- `CrosshairRenderer.cs` — SkiaSharp vector rendering (6 styles, outline support, premultiplied alpha)

### Hotkey mechanism

A 1×1 opacity-0 WPF window is created at startup to serve as the `HWND` for `RegisterHotKey`. Its `HwndSource.AddHook` catches `WM_HOTKEY` messages:
- `Alt+X` → toggle crosshair visibility (`ToggleOverlay`)
- `Alt+`` → open settings window

The hotkey window must be **kept visible** (`Show()` with Opacity=0), not hidden. `AllowsTransparency=true` can interfere with WM_HOTKEY delivery.

### MSIX packaging (manual)

```powershell
# 1. Publish WPF
dotnet publish CrosshairOverlay -c Release -r win-x64 --self-contained true -o CrosshairOverlay\Package\AppContent

# 2. Extract native Widget binary from standalone MSIX
MakeAppx unpack /p CrosshairOverlay.Widget\bin\x64\Release\CrosshairOverlay.Widget_1.0.0.0_x64.msix /d $env:TEMP\widget_extract /o

# 3. Assemble PackageLayout
#    Root ← Widget native files (from extracted MSIX, NOT from bin\Release raw output)
#    DesktopApp\ ← WPF published files (from AppContent)
#    Assets\ ← Widget PNG assets
#    AppxManifest.xml ← Combined manifest with both Application entries

# 4. Package + sign + install
MakeAppx pack /d PackageLayout /p output.msix /o
signtool sign /fd SHA256 /f cert.pfx /p password output.msix
Add-AppxPackage -Path output.msix
```

**Critical**: Widget native DLL (from standalone MSIX, ~10MB .NET Native compiled) must NOT be replaced by WPF's .NET 8 DLLs. Put Widget files in root, WPF files in DesktopApp\ subfolder.

### AppService IPC (Widget sync)

Only works when the desktop app runs inside the MSIX package (`Package.Current != null`). The standalone `bin\Debug` EXE has no package identity:
- `AppServiceServer.cs` connects to `CrosshairProfileService` on startup via `AppServiceConnection`
- `AppServiceClient.cs` (in Widget) receives profile JSON via `OnBackgroundActivated`
- Settings are pushed as `ValueSet` with `command` + `profileJson` keys
- **Standalone exe**: no Widget sync; desktop overlay still works

### Widget settings sync (file-based fallback)

When AppService is unavailable (standalone exe), the desktop app writes settings to:
```
%LOCALAPPDATA%\Packages\CrosshairOverlayWidget_ttvw7j9e3pmmp\LocalState\widget_settings.json
```
The Widget reads this file every 2 seconds via a `DispatcherTimer` in `CrosshairPage.xaml.cs`. Both AppService and file sync run in parallel — AppService takes precedence when available (instant push), file sync is the fallback.

### Widget project constraints (VS 2019 + UWP)

- C# language version locked to **8.0** by UWP XAML compiler — no file-scoped namespaces, target-typed new, **lambda discard `(_, _)`**
- Models must use block-scoped namespaces
- .NET Native compilation required; raw `bin\Release` output contains uncompiled IL files — use the standalone MSIX's **extracted payload** for packaging
- Additional VS Installer components needed: `Microsoft.Gaming.XboxGameBar` NuGet, `Microsoft.NETCore.UniversalWindowsPlatform`, `Newtonsoft.Json`
- Widget uses its own `Models\CrosshairProfile.cs` copy (not a link to the WPF project's, which uses C# 10 file-scoped namespaces)
- Requires Windows SDK `10.0.19041.0`

### Release packaging

After building, create a zip containing:
```
CrosshairOverlay_v1.1.0_Setup.zip
├── CrosshairOverlay/        ← self-contained publish (198 MB)
├── CrosshairOverlayWidget.msix
├── CrosshairOverlayWidget.cer
└── setup.bat                ← 双击安装（安装证书 + Widget + 启动桌面端）
```

## Config & storage

- Settings: `%APPDATA%\CrosshairOverlay\settings.json` (System.Text.Json, `CrosshairProfile` POCO)
- Error log: `%APPDATA%\CrosshairOverlay\log.txt`
- Widget settings: `%LOCALAPPDATA%\Packages\CrosshairOverlayWidget_ttvw7j9e3pmmp\LocalState\widget_settings.json` (written by desktop app, polled by Widget)

## Known pitfalls

- **Admin restart hotkey race**: When toggling Real Overlay, the non-admin process must `UnregisterHotKey` before spawning the admin process. The admin process's 5‑retry loop (1s intervals) resolves transient `ERROR_HOTKEY_ALREADY_REGISTERED (1409)`.
- **Game Bar Widget crosshair centering**: Two-tier strategy. Primary: `DisplayInformation.GetForCurrentView().ScreenWidthInRawPixels` cached in `OnPageLoaded` (works because the desktop triggers this before any game resolution change). Fallback: `ActualWidth/2, ActualHeight/2`. Widget auto-centering via any API remains impossible — set the manifest Size to large values (3840×2160) so the Widget covers the screen and the crosshair coordinate always falls within its bounds.
- **Game Bar Widget click-through**: Pinned widgets block mouse input by default. User must click the Mouse icon on Game Bar Home Bar to enable click-through. Without it, the entire widget area is unclickable. Our manifest declares `PinningSupported=true` so click-through is supported out of the box.
- **EnumWindows on Widget HWND**: The Widget's CoreWindow is invisible to Win32 `EnumWindows`. Do not attempt `WidgetPositioner`-style scanning.
- **ForceTopmost timer**: `System.Timers.Timer.Elapsed` runs on thread pool; `SetWindowPos` from non-UI thread is safe for USER32 operations but ensure the HWND remains valid.
- **SkiaSharp alpha**: `SKAlphaType.Premul` is required for `UpdateLayeredWindow` with `AC_SRC_ALPHA`. Using `Unpremul` causes rendering artifacts.
- **MainViewModel.Dispose()**: Must be called on exit. `_saveDebounceTimer` holds references to the Dispatcher. `SaveSettings()` checks `_disposed` flag before calling `timer.Stop()` to avoid `ObjectDisposedException` from stray timer callbacks. On exit: `SaveSettings()` first, then `Dispose()`.
- **SettingsService.SaveForWidget()**: Writes to Widget's LocalState folder. The path `CrosshairOverlayWidget_ttvw7j9e3pmmp` is the package family name — if the Widget certificate changes, this path changes.
- **Widget DisplayInformation cache**: `_cachedCenterX/Y` are captured once in `OnPageLoaded` and never refreshed. If the user changes their monitor resolution while the Widget is open, the cached values become stale. Solution: reopen the Widget.
