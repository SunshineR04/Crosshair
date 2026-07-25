# AGENTS.md

This file provides guidance to Qoder (qoder.com) when working with code in this repository.

## 项目概述

Windows 桌面准心叠加工具，专为 FPS 游戏设计，支持三种叠加引擎：
- **WPF 桌面端** (`CrosshairOverlay/`) — .NET 8、SkiaSharp 渲染、托盘图标、全局热键
- **UWP Game Bar Widget** (`CrosshairOverlay.Widget/`) — Xbox Game Bar 集成，支持独占全屏
- **Release 分发** — 自包含 zip 包，用户解压后双击 `setup.bat` 安装

## 构建命令

```powershell
# WPF 桌面端（独立运行，无 Widget 同步）
dotnet build CrosshairOverlay
dotnet run --project CrosshairOverlay
dotnet publish CrosshairOverlay -c Release -r win-x64 --self-contained true

# UWP Widget（需要 VS 2019+ + UWP 工作负荷 + .NET Native）
# msbuild 不在 PATH 中，必须用 VS 2019 完整路径：
& "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" CrosshairOverlay.Widget\CrosshairOverlay.Widget.csproj /p:Configuration=Release /p:Platform=x64
```

WPF 项目使用 `net8.0-windows10.0.19041.0` — WinRT API (`AppServiceConnection`、`Package.Current`) 需要此 TFM，且仅在 MSIX 包内运行时可用。

项目无测试、无 lint 配置。验证方式为 `dotnet build CrosshairOverlay` 编译通过 + 手动运行。

## 架构

### 三种叠加模式

| 模式 | 文件 | 需要 | 全屏支持 |
|---|---|---|---|
| Simple Overlay | `Rendering/OverlayHost.cs`（默认） | 无 | 窗口化/无边框 |
| Real Overlay | `Rendering/OverlayHost.cs`（forceTopmost=true） | 管理员权限 | FSO 独占全屏（DirectX） |
| Game Bar Widget | `CrosshairOverlay.Widget/`（独立 UWP） | Game Bar 已启用 | 真正独占全屏（所有 API） |

### 关键入口

- `App.xaml.cs` — 应用启动、热键注册（Win32 消息窗口）、托盘图标、AppService 初始化、DWM 异常处理
- `MainWindow.xaml` — 设置界面（6 种准心样式、颜色/大小/透明度滑块、叠加模式切换）
- `ViewModels/MainViewModel.cs` — MVVM 绑定 + `RelayCommand`（内联定义）；`ApplyProfile()` 推送设置到 OverlayHost 和 Widget
- `OverlayHost.cs` — 通过 `CreateWindowEx` + `UpdateLayeredWindow` 创建分层透明叠加窗口
- `CrosshairRenderer.cs` — SkiaSharp 矢量渲染（6 种样式、描边、预乘 alpha）
- `CrosshairPage.xaml.cs`（Widget）— UWP XAML Shapes 渲染、WindowBounds 自动居中、文件轮询同步

### 框架级设计决策

- **`ShutdownMode.OnExplicitShutdown`**：应用是托盘常驻型，关闭设置窗口不退出进程，只有托盘「退出」或 `Current.Shutdown()` 才终止。
- **WinForms 依赖**（`UseWindowsForms=true`）：仅用于 `NotifyIcon` + `ContextMenuStrip` 实现系统托盘，无其他 WinForms 用途。
- **JSON 序列化器差异**：WPF 端用 `System.Text.Json`，Widget 端用 `Newtonsoft.Json`（UWP 兼容性约束）。两端共享相同的 `CrosshairProfile` POCO 字段，序列化结果互通。
- **DWM 异常保护**：`DispatcherUnhandledException` 捕获 `0x80263001`（DWM 未就绪），防止系统重启后崩溃。

### 热键机制

启动时创建 1×1 不透明度 0 的 WPF 窗口作为 `RegisterHotKey` 的 HWND。`HwndSource.AddHook` 捕获 `WM_HOTKEY`：
- `Alt+X` → 切换准心可见性（同步到 Widget）
- `Alt+`` → 打开设置窗口

热键窗口必须**保持可见**（`Show()` + `Opacity=0`），不能隐藏。

### Widget 设置同步（双通道）

**通道 1：AppService IPC**（仅 MSIX 包内运行时可用）
- `AppServiceServer.cs` 连接 `CrosshairProfileService`，推送 profile JSON
- `AppServiceClient.cs`（Widget）通过 `OnBackgroundActivated` 接收
- `_pendingProfile` 暂存机制：连接建立前的推送不丢弃，连接成功后自动 flush

**通道 2：文件同步**（独立 exe 的 fallback）
- 桌面端 `SettingsService.SaveForWidget()` 写入 `widget_settings.json`
- Widget 每 2 秒通过 `DispatcherTimer` 轮询读取
- `IsVisible` setter 立即写入文件，确保 Alt+X 切换实时同步

两个通道并行运行，AppService 可用时即时推送，文件同步作为兜底。

### Widget 准心自动居中

`CrosshairPage` 使用 `XboxGameBarWidget.WindowBounds` API + `DisplayInformation` 计算屏幕中心在 Widget 本地坐标系中的偏移：
- `OnPageLoaded` 缓存 `ScreenWidthInRawPixels`、`ScreenHeightInRawPixels`、`RawPixelsPerViewPixel`
- `RenderCrosshair` 计算：`offsetX = (screenCenterRawX / rawPixelsPerViewPixel) - WindowBounds.X`
- 如果偏移在 Widget 范围内则使用偏移坐标，否则回退到 `ActualWidth/2`
- `OnNavigatedTo` 调用 `CenterWindowAsync()` 将 Widget 居中到屏幕
- `WindowBoundsChanged` 事件触发重新渲染

### MSIX 打包（手动）

```powershell
# 1. 发布 WPF
dotnet publish CrosshairOverlay -c Release -r win-x64 --self-contained true -o CrosshairOverlay\Package\AppContent

# 2. 从独立 MSIX 提取 Widget 原生文件
MakeAppx unpack /p CrosshairOverlay.Widget\bin\x64\Release\CrosshairOverlay.Widget_1.0.0.0_x64.msix /d $env:TEMP\widget_extract /o

# 3. 组装 PackageLayout
#    根目录 ← Widget 原生文件（从提取的 MSIX，不是 bin\Release 原始输出）
#    DesktopApp\ ← WPF 发布文件
#    Assets\ ← Widget PNG 资源
#    AppxManifest.xml ← 合并清单（两个 Application 条目）

# 4. 打包 + 签名 + 安装
MakeAppx pack /d PackageLayout /p output.msix /o
signtool sign /fd SHA256 /f cert.pfx /p password output.msix
Add-AppxPackage -Path output.msix
```

**关键**：Widget 原生 DLL（约 10MB .NET Native 编译）不能被 WPF 的 .NET 8 DLL 替换。Widget 文件放根目录，WPF 文件放 `DesktopApp\` 子目录。

### Widget 项目约束（VS 2019 + UWP）

- C# 语言版本锁定为 **8.0**（UWP XAML 编译器限制）— 不支持文件作用域命名空间、目标类型 new、**lambda 弃元 `(_, _)`**
- Models 必须使用块作用域命名空间
- 需要 .NET Native 编译；`bin\Release` 原始输出包含未编译 IL 文件 — 打包时用独立 MSIX 的**提取载荷**
- 需要 VS Installer 组件：`Microsoft.Gaming.XboxGameBar` NuGet、`Microsoft.NETCore.UniversalWindowsPlatform`、`Newtonsoft.Json`
- Widget 使用自己的 `Models\CrosshairProfile.cs` 副本（不是 WPF 项目的链接，因为 WPF 用 C# 10 文件作用域命名空间）
- 需要 Windows SDK `10.0.19041.0`

### Manifest 一致性

`Package/Package.appxmanifest`（源码）和 `Package/PackageLayout/AppxManifest.xml`（手动打包）必须保持一致：
- 桌面端路径：`Executable="DesktopApp\CrosshairOverlay.exe"`
- Widget 尺寸：`3840×2160`，MaxHeight `4320`，MaxWidth `7680`

### Release 打包

构建后创建 zip 包：
```
CrosshairOverlay_v1.1.0_Setup.zip
├── CrosshairOverlay/        ← 自包含发布（约 198 MB）
├── CrosshairOverlayWidget.msix
├── CrosshairOverlayWidget.cer
└── setup.bat                ← 双击安装（安装证书 + Widget + 启动桌面端）
```

发布到 GitHub Releases 和 Gitee Releases（Gitee 不支持 API 上传附件，需手动在网页端上传或引导用户从 GitHub 下载）。

## 配置与存储

- 设置文件：`%APPDATA%\CrosshairOverlay\settings.json`（System.Text.Json，`CrosshairProfile` POCO）
- 错误日志：`%APPDATA%\CrosshairOverlay\log.txt`
- Widget 设置：`%LOCALAPPDATA%\Packages\CrosshairOverlayWidget_ttvw7j9e3pmmp\LocalState\widget_settings.json`（桌面端写入，Widget 轮询读取）

## 已知陷阱

- **管理员重启热键竞争**：切换 Real Overlay 时，非管理员进程必须先 `UnregisterHotKey` 再启动管理员进程（见 `OnRestartRequested`）。新进程启动时旧进程可能尚未完全退出，`RegisterHotKey` 可能返回 `ERROR_HOTKEY_ALREADY_REGISTERED (1409)`。当前代码无重试逻辑，注册失败时 `_hotkeyOk=false` 并提示用户使用托盘菜单。
- **Game Bar Widget 点击穿透**：固定的小部件默认拦截鼠标输入。用户必须点击 Game Bar Home Bar 上的鼠标图标启用点击穿透。manifest 声明 `PinningSupported=true` 即可支持。
- **EnumWindows 与 Widget HWND**：Widget 的 CoreWindow 对 Win32 `EnumWindows` 不可见。不要尝试 `WidgetPositioner` 式扫描。
- **ForceTopmost 定时器**：`System.Timers.Timer.Elapsed` 在线程池运行；从非 UI 线程调用 `SetWindowPos` 对 USER32 操作是安全的，但确保 HWND 有效。
- **SkiaSharp alpha**：`UpdateLayeredWindow` + `AC_SRC_ALPHA` 要求 `SKAlphaType.Premul`。使用 `Unpremul` 会导致渲染伪影。
- **MainViewModel.Dispose()**：退出时必须调用。`_saveDebounceTimer` 持有 Dispatcher 引用。`SaveSettings()` 检查 `_disposed` 标志避免 `ObjectDisposedException`。退出顺序：先 `SaveSettings()`，再 `Dispose()`。
- **SettingsService.SaveForWidget()**：写入 Widget 的 LocalState 文件夹。路径中的 `CrosshairOverlayWidget_ttvw7j9e3pmmp` 是包族名 — 如果 Widget 证书更换，此路径会变。
- **Widget DisplayInformation 缓存**：`_screenCenterRawX/Y` 在 `OnPageLoaded` 时缓存，不刷新。如果 Widget 打开期间更换显示器分辨率，缓存值会过时。解决：重新打开 Widget。
- **GDI 资源管理**：`RenderAndUpdate` 中所有 Win32 调用（`GetDC`、`CreateCompatibleDC`、`CreateDIBSection`、`UpdateLayeredWindow`）都检查返回值，失败时释放已分配资源并记录 `Debug.WriteLine`。
- **Widget 可见性同步**：`IsVisible` setter 末尾写入 `_profile.IsVisible` 和 `SaveForWidget`，Widget 的 `RenderCrosshair` 开头检查 `_profile.IsVisible`，为 false 时清空 Canvas 不渲染。
