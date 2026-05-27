using System.Diagnostics;
using System.Security.Principal;

namespace CrosshairOverlay.Services;

public static class AdminElevationHelper
{
    public static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

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
