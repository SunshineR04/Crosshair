# 独立双产物发布与同步加固设计

日期：2026-08-02
状态：已获用户批准，待进入实施计划

## 目标

让普通用户通过“下载 ZIP → 解压 → 双击 `setup.bat`”安装后，能够完整使用 WPF 桌面端和 Xbox Game Bar Widget。正式发布采用独立双产物：WPF 自包含 EXE + 独立 Widget MSIX；文件同步是当前正式配置通道，AppService 仅保留为修正后的未来组合部署能力。

## 发布架构

发布 ZIP 由干净 staging 目录生成，包含：

- `setup.bat`、`setup.ps1`
- WPF 自包含 `CrosshairOverlay/` 载荷
- `CrosshairOverlayWidget.msix`
- `CrosshairOverlayWidget.cer`
- 必需的 x64 UWP/.NET Native/VCLibs 依赖，或明确的依赖预检结果
- `release-manifest.json`（版本、源码提交、文件 SHA-256、证书指纹）

版本由单一来源注入桌面程序集、Widget manifest、MSIX、ZIP 和清单。发布脚本必须清空 staging、构建当前源码、校验 ZIP allowlist、嵌入 manifest 和载荷哈希，禁止复用旧 `Release` 文件。

独立发布的 README/安装提示明确说明：Widget 的正常同步依赖文件 fallback；AppService 只用于未来组合 MSIX，不作为独立安装成功的前提。

## 安装器与信任

`setup.ps1` 在安装前校验 Windows/x64、必需文件、SHA-256、证书 Subject/Publisher/Thumbprint/有效期和 MSIX 签名 Publisher。证书只允许按预期指纹导入当前用户 `TrustedPeople`，不导入 `Root`。安装失败时不启动桌面端。

安装器预检或安装发布包中的精确依赖；缺失时输出可操作错误。使用单调递增 MSIX 版本，默认不允许降级，不使用 `-ForceUpdateFromAnyVersion`。提权流程必须避免从可写目录再次执行未校验脚本；至少在提权前校验脚本/载荷哈希，长期迁移到受保护的签名 bootstrapper。

## 配置同步协议

Widget 文件配置采用向后兼容 envelope：

```json
{
  "schemaVersion": 1,
  "revision": 42,
  "updatedUtc": "2026-08-02T12:00:00Z",
  "profile": { }
}
```

桌面端每次有效 profile 变化递增 revision，并在可见性切换时立即写入。Widget 同时兼容旧版裸 `CrosshairProfile` JSON（视为 revision 0），只接受比当前 revision 更新的 profile。IPC 和文件通道遵循相同 revision 规则；IPC 作为未来组合模式的即时通道，文件作为当前独立模式的可靠通道。

Widget 文件读取改为可取消、串行执行，成功反序列化和 sanitize 后才更新 `_lastJson`；页面卸载时取消任务，旧读取完成时不得覆盖较新的 IPC/profile 状态。损坏文件必须在后续轮询中可重试。

桌面端不再永久硬编码包族名：优先读取安装器写入的实际 Package Family Name，并校验 LocalState；缺失时动态发现已安装 Widget，失败时记录明确日志但不阻止桌面端运行。

## AppService 修复

Widget 对每个 `RequestReceived` 请求发送成功或错误 `ValueSet`，并在发送响应后完成 deferral。桌面端保留最新 profile、处理响应失败/断线和重连。重复初始化必须完成所有 deferral，旧连接事件要解绑和释放；跨线程状态通过锁或 UI dispatcher 统一访问。

`MainViewModel.IsVisible` 在本地切换后同时更新 fallback 和可用 IPC，确保组合部署下 `Alt+X` 状态一致。

## 平台可靠性

- Overlay 按目标显示器获取 DPI，而不是使用主桌面 HWND 的 DPI；监视器/DPI 改变时刷新。
- Widget 在 bounds/display 变化时刷新显示器指标并正确处理虚拟桌面坐标。
- Widget 居中任务观察异常并在 bounds 就绪后重试。
- ForceTopmost 定时器与 HWND 销毁通过生命周期同步避免竞态。
- 保留 Game Bar 点击穿透说明；如主机行为允许，再验证/配置 `IsHitTestVisible=false`。

## 测试与验收

现有 WPF 构建和 xUnit 测试继续作为基础门禁。新增：

- AppService request/response、错误、重连、重复初始化、取消和 Dispose 测试
- revision、旧 JSON、损坏 JSON、文件/IPC 并发和卸载取消测试
- visibility 同步测试
- 包族名发现和缺失 Widget 测试
- manifest/ZIP allowlist、版本、证书指纹、SHA-256 和依赖预检测试
- Renderer 几何/颜色/DPI 边界测试
- 干净 Windows 10/11 x64 安装、升级、卸载冒烟测试

性能优化（渲染缓存、GDI 复用、Shape 复用、轮询调整）放在正确性和发布链路稳定之后，并以 profiling 数据为依据。
