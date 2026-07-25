using System.Diagnostics;
using System.Security.Principal;

namespace CrosshairOverlay.Services;

/// <summary>
/// 管理员权限检测与进程提升工具。
/// </summary>
public static class AdminElevationHelper
{
    /// <summary>检测当前进程是否以管理员权限运行。</summary>
    public static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// 以管理员权限重启当前进程。触发 UAC 提示。
    /// 调用方应在调用前保存设置并注销热键。
    /// </summary>
    public static void RestartAsAdmin(string extraArgs = "")
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
            throw new InvalidOperationException("Unable to determine process executable path.");

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = extraArgs,
            UseShellExecute = true,
            Verb = "runas"
        };

        Process.Start(startInfo);
    }
}
