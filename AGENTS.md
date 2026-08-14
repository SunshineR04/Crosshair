# AGENTS.md

本文件供未来 ZCode agents 在本仓库中工作时快速查阅。

## 项目概述

Windows FPS 准心叠加工具：
- `CrosshairOverlay/`：.NET 8 WPF 桌面端，SkiaSharp + Win32 分层窗口、托盘和全局热键。
- `CrosshairOverlay.Widget/`：C# 8 UWP/Xbox Game Bar Widget，使用 XAML Shapes 渲染。
- `CrosshairOverlay.Tests/`：WPF 端 xUnit 测试。
- `Release/`：手动发布的 MSIX、自包含桌面载荷和 `setup.bat`/`setup.ps1`；发布生成物通常被 `.gitignore` 忽略。

## 构建与验证

```powershell
# 桌面端
dotnet build CrosshairOverlay
dotnet run --project CrosshairOverlay
dotnet publish CrosshairOverlay -c Release -r win-x64 --self-contained true

# 测试（可选收集 Cobertura 覆盖率）
dotnet test CrosshairOverlay.Tests
dotnet test CrosshairOverlay.Tests --collect:"XPlat Code Coverage" --settings CrosshairOverlay.Tests/coverage.runsettings

# Widget：需要 VS 2019+、UWP 工作负荷、Windows SDK 10.0.19041.0 和 .NET Native
& "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" CrosshairOverlay.Widget\CrosshairOverlay.Widget.csproj /p:Configuration=Release /p:Platform=x64
```

仓库没有独立 lint 配置；提交前至少运行桌面端构建和 xUnit 测试。WPF TFM 为 `net8.0-windows10.0.19041.0`，Widget 不能用普通 `dotnet build` 替代 VS/MSBuild 的 UWP 构建。

## 架构与编辑边界

- WPF 入口为 `App.xaml.cs`；`MainViewModel` 负责设置绑定、保存防抖和把 profile 推送到 `OverlayHost`/Widget。
- `Rendering/OverlayHost.cs` 使用 Win32 `CreateWindowEx` + `UpdateLayeredWindow`；`CrosshairRenderer` 使用 SkiaSharp 绘制 6 种样式。分层窗口要求 `SKAlphaType.Premul`。
- `ShutdownMode.OnExplicitShutdown` 是刻意设计：关闭设置窗口只隐藏到托盘，必须通过托盘退出或 `Application.Shutdown()` 结束进程。`UseWindowsForms=true` 只为托盘 `NotifyIcon`。
- 全局热键由一个保持 `Show()`、但 `Opacity=0` 的 1×1 WPF HWND 接收：`Alt+X` 切换可见性，`Alt+\`` 打开设置；不要隐藏该窗口。
- WPF 使用 `System.Text.Json`，Widget 使用 `Newtonsoft.Json`；两端的 `CrosshairProfile` 字段和颜色格式（`#RRGGBB`）必须保持兼容。共享规则文件必须保留 C# 8 语法。
- Widget 的 `CrosshairPage` 使用 `WindowBounds` + `DisplayInformation` 居中，并通过 `AppServiceClient` 接收 IPC；独立运行时每 2 秒读取 `widget_settings.json` 作为 fallback。两个通道需继续并行可靠工作。

## UWP/打包约束

- Widget 项目固定 `LangVersion=8.0`，使用块作用域命名空间；需要 VS 2019+ UWP 工作负荷、Windows SDK `10.0.19041.0`、`.NET Native`。不要用 WPF 的 C# 10 文件作用域模型文件替换 Widget 副本。
- 手动组包时，Widget 必须使用独立 MSIX 提取出的 .NET Native 原生载荷放在包根目录；WPF 自包含发布物放 `DesktopApp/`。不能用 WPF .NET 8 DLL 替换 Widget DLL。
- `CrosshairOverlay/Package/Package.appxmanifest` 与 `CrosshairOverlay/Package/PackageLayout/AppxManifest.xml` 必须同步，桌面入口为 `DesktopApp\\CrosshairOverlay.exe`，Widget 尺寸为 `3840×2160`（最大 `7680×4320`）。

## 手动 MSIX 组包

```powershell
dotnet publish CrosshairOverlay -c Release -r win-x64 --self-contained true -o CrosshairOverlay\Package\AppContent
MakeAppx unpack /p CrosshairOverlay.Widget\bin\x64\Release\CrosshairOverlay.Widget_1.0.0.0_x64.msix /d $env:TEMP\widget_extract /o
# 将提取的 Widget 载荷放 PackageLayout 根目录，WPF 发布物放 PackageLayout\DesktopApp
MakeAppx pack /d PackageLayout /p output.msix /o
signtool sign /fd SHA256 /f cert.pfx /p password output.msix
Add-AppxPackage -Path output.msix
```

## 配置、日志与发布

- WPF 设置：`%APPDATA%\CrosshairOverlay\settings.json`；日志：`%APPDATA%\CrosshairOverlay\log.txt`。
- Widget fallback 文件：`%LOCALAPPDATA%\Packages\CrosshairOverlayWidget_ttvw7j9e3pmmp\LocalState\widget_settings.json`。包族名随证书/Identity 变化，不能盲目硬编码到新包。
- `Release\setup.bat` 调用 `setup.ps1`；脚本会把 `.cer` 加入当前用户 `TrustedPeople`、安装 MSIX、校验包并启动 `Release\CrosshairOverlay\CrosshairOverlay.exe`。

## 已知陷阱

- **管理员重启**：启用 Real Overlay 时会先注销热键、保存并以管理员重启；新旧进程短暂竞争时注册可能失败。当前有 5 次、100ms 间隔重试，失败时 UI/托盘提示用户。
- **Widget 点击穿透**：固定 Widget 默认拦截鼠标，必须在 Game Bar Home Bar 点击鼠标图标启用穿透；`PinningSupported=true` 仅表示支持固定。
- **不要扫描 Widget HWND**：UWP CoreWindow 对 Win32 `EnumWindows` 不可见，不能用桌面端窗口扫描定位。
- **线程与资源**：`ForceTopmost` 定时器在线程池调用 USER32；修改时确保 HWND 有效。`OverlayHost.RenderAndUpdate` 的 GDI/DC/bitmap 必须逐一检查和释放。
- **生命周期**：退出顺序保持 `SaveSettings()` → `MainViewModel.Dispose()` → 注销热键/释放 HWND → `OverlayHost`/IPC/单实例资源；保存防抖 Timer 持有 Dispatcher。
- **渲染与同步**：Widget 居中使用的屏幕/DPI 缓存不会在显示器分辨率改变后自动刷新；`IsVisible=false` 时必须清空 Widget Canvas，且可见性切换要立即写 fallback 文件。
