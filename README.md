# Crosshair Overlay — 桌面准心叠加层

![Windows](https://img.shields.io/badge/Windows-10%2B-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/license-MIT-green)

**Crosshair Overlay** 是一款 Windows 桌面准心叠加工具，专为 FPS 游戏设计。支持三种叠加引擎，覆盖所有游戏模式。

---

## 功能

- **Simple Overlay** — 窗口化/无边框全屏下显示准心，`Alt+X` 一键切换
- **Real Overlay（管理员）** — FSO 独占全屏下显示准心，使用管理员权限覆盖游戏窗口
- **Game Bar Widget** — 基于 Xbox Game Bar 的准心组件，支持真正的独占全屏（包括 Vulkan 游戏）
- **6 种准心样式** — 十字、圆点、十字+圆点、圆环、圆环+圆点、实心轮廓
- **完全自定义** — 颜色、粗细、长度、间距、透明度、轮廓描边
- **全局快捷键** — `Alt+X` 显示/隐藏准心，`Alt+`` 打开设置
- **系统托盘** — 最小化到托盘运行，右键菜单快速切换
- **配置持久化** — 设置自动保存到 `%APPDATA%\CrosshairOverlay\settings.json`

---

## 快速开始

### 桌面叠加层（推荐）

```
1. 解压 Release → 双击 CrosshairOverlay.exe
2. 按 Alt+X 显示准心
3. 进游戏设置为「窗口化全屏（无边框）」模式
```

### Game Bar Widget（独占全屏）

```
1. 管理员运行 install.ps1
2. 按 Win+G 打开 Xbox Game Bar
3. 在 Widget 菜单中选择「准心叠加层」
4. 固定 📌 → 关闭 Game Bar 后准心仍可见
5. 桌面端改设置 → Widget 实时同步
```

---

## 快捷键

| 快捷键 | 功能 |
|--------|------|
| `Alt + X` | 显示/隐藏准心 |
| `Alt + `` ` | 打开设置窗口 |
| `Win + G` | 打开 Xbox Game Bar (Widget) |

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

## 项目结构

```
CrosshairOverlay/
├── CrosshairOverlay/               # WPF 桌面应用
│   ├── App.xaml.cs                 # 应用入口 + 热键 + 托盘
│   ├── MainWindow.xaml             # 设置窗口
│   ├── Rendering/
│   │   ├── OverlayHost.cs          # 叠加窗口引擎
│   │   └── CrosshairRenderer.cs    # SkiaSharp 准心渲染
│   ├── Services/
│   │   ├── AppServiceServer.cs     # MSIX 包内 IPC
│   │   ├── SettingsService.cs      # JSON 配置持久化
│   │   └── StartupService.cs       # 开机自启
│   └── ViewModels/
│       └── MainViewModel.cs        # MVVM ViewModel
│
├── CrosshairOverlay.Widget/        # UWP Game Bar Widget
│   ├── CrosshairPage.xaml.cs       # 准心渲染 (UWP Shapes)
│   ├── Services/
│   │   └── AppServiceClient.cs     # IPC 客户端
│   └── Package.appxmanifest        # Game Bar 扩展声明
│
└── CrosshairOverlay.Package/       # MSIX 打包
    ├── CrosshairOverlay.Package.wapproj
    └── Package.appxmanifest        # 合并清单
```

---

## 技术栈

| 组件 | 技术 |
|------|------|
| 桌面应用 | C# / .NET 8 / WPF / SkiaSharp |
| Game Bar Widget | C# / UWP / .NET Native |
| 跨进程通信 | AppServiceConnection (WinRT IPC) |
| 窗口叠加 | Win32 API / UpdateLayeredWindow |
| 配置持久化 | System.Text.Json |
| 打包 | MSIX / Desktop Bridge |

---

## 构建

```powershell
# 桌面应用
dotnet build CrosshairOverlay

# Game Bar Widget（需要 VS 2019+ + UWP 工作负荷）
cd CrosshairOverlay.Widget
msbuild CrosshairOverlay.Widget.csproj /p:Configuration=Release /p:Platform=x64

# 发布（自包含）
dotnet publish CrosshairOverlay -c Release -r win-x64 --self-contained true
```

---

## License

MIT
