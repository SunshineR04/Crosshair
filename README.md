# Crosshair Overlay — 桌面准心叠加层

![Windows](https://img.shields.io/badge/Windows-10.0.19041%2B-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![C#](https://img.shields.io/badge/C%23-12-239120)
![WPF](https://img.shields.io/badge/UI-WPF%20%2F%20SkiaSharp-512BD4)
![UWP](https://img.shields.io/badge/Xbox%20Game%20Bar-Widget-0E8A00)
![License](https://img.shields.io/badge/license-MIT-green)

**Crosshair Overlay** 是一款面向 FPS 玩家的 Windows 桌面准心叠加工具。通过三种互补的叠加引擎——窗口叠加、管理员特权叠加与 Xbox Game Bar Widget——覆盖窗口化全屏、FSO 独占全屏以及真正独占全屏（含 Vulkan）等全部常见游戏模式。

---

## 功能特性

- **三种叠加引擎，覆盖全部游戏模式**
  - **Simple Overlay** — 窗口化/无边框全屏下显示准心，`Alt+X` 一键切换
  - **Real Overlay（管理员）** — FSO 独占全屏下显示准心，以管理员权限覆盖游戏窗口
  - **Game Bar Widget** — 基于 Xbox Game Bar 的准心组件，支持真正独占全屏（包括 Vulkan 游戏）
- **Widget 准心自动居中** — 基于 WindowBounds API + DisplayInformation，自动适配所有分辨率/比例/DPI
- **Widget 设置同步** — 桌面端修改样式后 2 秒内自动同步到 Widget
- **Widget 显示/隐藏同步** — `Alt+X` 切换准心可见性自动同步到 Widget
- **6 种准心样式** — 十字、圆点、十字+圆点、圆环、圆环+圆点、实心轮廓
- **完全自定义** — 颜色、粗细、长度、间距、透明度、轮廓描边
- **全局快捷键** — <kbd>Alt</kbd>+<kbd>X</kbd> 显示/隐藏准心，<kbd>Alt</kbd>+<kbd>`</kbd> 打开设置
- **系统托盘** — 最小化到托盘运行，右键菜单快速切换
- **配置持久化** — 设置自动保存到 `%APPDATA%\CrosshairOverlay\settings.json`
- **DWM 异常保护** — 重启后桌面合成器未就绪时不再崩溃

---

## 目录

- [快速开始](#快速开始)
- [使用说明](#使用说明)
- [下载](#下载)
- [游戏兼容性](#游戏兼容性)
- [叠加引擎对比](#叠加引擎对比)
- [配置与数据文件](#配置与数据文件)
- [项目结构](#项目结构)
- [技术栈](#技术栈)
- [构建与测试](#构建与测试)
- [发布与打包](#发布与打包)
- [注意事项](#注意事项)
- [License](#license)

---

## 快速开始

### 系统要求

| 项目 | 要求 |
|------|------|
| 操作系统 | Windows 10 2004（Build 19041）及以上 |
| 桌面端运行时 | .NET 8（自包含发布，免安装） |
| Widget | Xbox Game Bar（Windows 10/11 自带） |

### 桌面叠加层（推荐）

```
1. 下载 zip → 解压
2. 双击 setup.bat → 完成安装
3. 按 Alt+X 显示准心
4. 进游戏设置为「窗口化全屏（无边框）」模式
```

### Game Bar Widget（独占全屏）

```
1. 双击 setup.bat → 自动安装证书和 Widget
2. 按 Win+G 打开 Xbox Game Bar
3. 在 Widget 菜单中选择「准心叠加层」
4. 固定 📌 → 关闭 Game Bar 后准心仍可见
5. 点击 Home Bar 上的鼠标图标 → 启用「点击穿透」
6. 桌面端改设置 → Widget 实时同步
```

> ⚠️ **重要**：固定后必须启用点击穿透，否则 Widget 窗口会拦截鼠标操作，导致桌面对应区域无法点击。启用后所有鼠标事件将穿透 Widget 直达底层游戏/桌面。

> 当前发布包由独立 WPF 桌面端和独立 Widget MSIX 组成，文件同步是跨进程配置同步的可靠回退通道。组合 MSIX 额外启用 AppService 即时同步，但文件通道仍会保留。

---

## 使用说明

| 快捷键 | 功能 |
|--------|------|
| <kbd>Alt</kbd> + <kbd>X</kbd> | 显示/隐藏准心 |
| <kbd>Alt</kbd> + <kbd>`</kbd> | 打开设置窗口 |
| <kbd>Win</kbd> + <kbd>G</kbd> | 打开 Xbox Game Bar (Widget) |

**游戏模式建议**

| 游戏模式 | 推荐引擎 |
|----------|----------|
| 窗口化 / 无边框全屏 | Simple Overlay 或 Game Bar Widget |
| FSO 独占全屏 | Real Overlay（管理员）或 Game Bar Widget |
| 真正独占全屏 / Vulkan | Game Bar Widget |

---

## 下载

- **GitHub**: [v1.1.0 Release](https://github.com/SunshineR04/Crosshair/releases/download/v1.1.0/CrosshairOverlay_v1.1.0_Setup.zip)（约 84 MB）
- **Gitee（国内加速）**: [v1.1.0 Release](https://gitee.com/sr17786628446/CrosshairOverlay/releases)

---

## 游戏兼容性

| 游戏 | Simple Overlay | Real Overlay | Game Bar Widget |
|------|---------------|-------------|----------------|
| 无畏契约 (Valorant) | ✅ 窗口化全屏 | ✅ 独占全屏 | ✅ |
| CS2 | ✅ 窗口化全屏 | ✅ 独占全屏 | ✅ |
| 三角洲行动 | ✅ 窗口化全屏 | ✅ 独占全屏 | ✅ |

---

## 叠加引擎对比

| | Simple Overlay | Real Overlay | Game Bar Widget |
|---|---|---|---|
| **管理员权限** | 不需要 | 需要 | 不需要 |
| **窗口化全屏** | ✅ | ✅ | ✅ |
| **FSO 独占全屏** | ❌ | ✅ | ✅ |
| **真正独占全屏** | ❌ | ❌ | ✅ |
| **Vulkan** | ✅ | ❌ | ✅ |
| **反作弊** | ✅ 安全 | ✅ 安全 | ✅ 最安全 |

---

## 配置与数据文件

| 路径 | 说明 |
|------|------|
| `%APPDATA%\CrosshairOverlay\settings.json` | 桌面端设置（自动保存） |
| `%APPDATA%\CrosshairOverlay\log.txt` | 桌面端运行日志 |
| `%LOCALAPPDATA%\Packages\CrosshairOverlayWidget_ttvw7j9e3pmmp\LocalState\widget_settings.json` | Widget fallback 配置 |

> **注意**：Widget fallback 文件路径中的包族名随证书/Identity 变化，更换证书后需以实际包族名为准，不要硬编码。

---

## 项目结构

```
.
├── CrosshairOverlay/               # WPF 桌面应用（.NET 8）
│   ├── App.xaml.cs                 # 应用入口 + 热键 + 托盘
│   ├── MainWindow.xaml             # 设置窗口
│   ├── Rendering/
│   │   ├── OverlayHost.cs          # Win32 分层窗口叠加引擎
│   │   └── CrosshairRenderer.cs    # SkiaSharp 准心渲染
│   ├── Services/
│   │   ├── AppServiceServer.cs     # MSIX 包内 AppService IPC
│   │   ├── SettingsService.cs      # JSON 配置持久化
│   │   └── StartupService.cs       # 开机自启
│   ├── ViewModels/
│   │   └── MainViewModel.cs        # MVVM ViewModel
│   └── Package/                    # 组合包清单与组包目录
│
├── CrosshairOverlay.Widget/        # UWP Xbox Game Bar Widget（C# 8）
│   ├── CrosshairPage.xaml.cs       # 准心渲染（UWP Shapes）
│   ├── Services/
│   │   └── AppServiceClient.cs     # AppService IPC 客户端
│   └── Package.appxmanifest        # Game Bar 扩展声明
│
├── CrosshairOverlay.Tests/         # 桌面端 xUnit 单元测试
│   ├── CrosshairRendererTests.cs   # 渲染逻辑
│   ├── SettingsServiceTests.cs     # 配置持久化
│   ├── MainViewModelTests.cs       # ViewModel 行为
│   ├── AppServiceServerTests.cs    # IPC 服务端
│   └── ...
│
├── Release/                        # 发布产物（zip、MSIX、证书、安装脚本）
└── docs/                           # 设计文档与开发计划
```

---

## 技术栈

| 组件 | 技术 |
|------|------|
| 桌面应用 | C# 12 / .NET 8 / WPF / SkiaSharp 2.88 |
| Game Bar Widget | C# 8 / UWP / .NET Native |
| 跨进程通信 | AppServiceConnection (WinRT IPC) + 文件同步 fallback |
| 窗口叠加 | Win32 API / UpdateLayeredWindow |
| 配置持久化 | System.Text.Json（桌面端）/ Newtonsoft.Json（Widget） |
| 测试 | xUnit + XPlat Code Coverage |
| 打包 | MSIX / Desktop Bridge |

---

## 构建与测试

### 桌面端

```powershell
# Debug 构建
dotnet build CrosshairOverlay

# 本地运行
dotnet run --project CrosshairOverlay

# 单元测试
dotnet test CrosshairOverlay.Tests

# 收集覆盖率（Cobertura）
dotnet test CrosshairOverlay.Tests --collect:"XPlat Code Coverage" --settings CrosshairOverlay.Tests/coverage.runsettings

# 发布自包含桌面应用
dotnet publish CrosshairOverlay -c Release -r win-x64 --self-contained true
```

### Game Bar Widget

需要 VS 2019+、UWP 工作负荷、Windows SDK `10.0.19041.0` 和 .NET Native。Widget 项目固定 `LangVersion=8.0`，不能使用普通 `dotnet build` 替代 VS/MSBuild 的 UWP 构建：

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" CrosshairOverlay.Widget\CrosshairOverlay.Widget.csproj /p:Configuration=Release /p:Platform=x64
```

---

## 发布与打包

```powershell
# 1. 发布 WPF 自包含桌面应用
dotnet publish CrosshairOverlay -c Release -r win-x64 --self-contained true -o CrosshairOverlay\Package\AppContent

# 2. 解包 Widget MSIX，提取 .NET Native 原生载荷
MakeAppx unpack /p CrosshairOverlay.Widget\bin\x64\Release\CrosshairOverlay.Widget_1.0.0.0_x64.msix /d $env:TEMP\widget_extract /o

# 3. 组包：Widget 载荷放 PackageLayout 根目录，WPF 发布物放 PackageLayout\DesktopApp
MakeAppx pack /d PackageLayout /p output.msix /o

# 4. 签名
signtool sign /fd SHA256 /f cert.pfx /p password output.msix

# 5. 安装
Add-AppxPackage -Path output.msix
```

> **组包约束**：Widget 必须使用独立 MSIX 提取出的 .NET Native 原生载荷放在包根目录，WPF 自包含发布物放 `DesktopApp/`；不能用 WPF .NET 8 DLL 替换 Widget DLL。`Package.appxmanifest` 与 `PackageLayout/AppxManifest.xml` 必须保持同步，桌面入口为 `DesktopApp\CrosshairOverlay.exe`。

---

## 注意事项

- **管理员重启**：启用 Real Overlay 时会先注销热键、保存并以管理员权限重启；新旧进程短暂竞争时热键注册可能失败。当前内置 5 次、100ms 间隔重试，失败时 UI/托盘会提示用户。
- **Widget 点击穿透**：固定 Widget 默认拦截鼠标，必须在 Game Bar Home Bar 点击鼠标图标启用穿透；`PinningSupported=true` 仅表示支持固定。
- **窗口模式**：Simple Overlay 需要游戏设置为「窗口化全屏」；FSO 独占全屏需使用 Real Overlay 或 Widget。
- **不要扫描 Widget HWND**：UWP CoreWindow 对 Win32 `EnumWindows` 不可见，不能用桌面端窗口扫描定位 Widget。

---

## License

MIT

> 本工具仅用于辅助练习与休闲娱乐。使用叠加工具时请遵守目标游戏的用户协议与反作弊规则，风险自负。
