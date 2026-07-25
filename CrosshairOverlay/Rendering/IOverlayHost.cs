using CrosshairOverlay.Models;

namespace CrosshairOverlay.Rendering;

/// <summary>
/// 叠加窗口宿主接口，抽象 Win32 分层窗口的创建、渲染和生命周期管理。
/// </summary>
public interface IOverlayHost : IDisposable
{
    /// <summary>叠加窗口是否当前可见。</summary>
    bool IsVisible { get; }

    /// <summary>叠加窗口的 HWND 句柄。</summary>
    IntPtr Handle { get; }

    /// <summary>
    /// 更新准心配置并重新渲染。如果窗口可见则立即刷新。
    /// </summary>
    void SetProfile(CrosshairProfile profile);

    /// <summary>
    /// 创建并显示叠加窗口。
    /// </summary>
    /// <returns>成功返回 true；CreateWindowEx 失败返回 false。</returns>
    bool Show();

    /// <summary>隐藏并销毁叠加窗口，释放 GDI 资源。</summary>
    void Hide();

    /// <summary>切换叠加窗口的显示/隐藏状态。</summary>
    void Toggle();
}
