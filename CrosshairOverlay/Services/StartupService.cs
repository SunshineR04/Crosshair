using Microsoft.Win32;

namespace CrosshairOverlay.Services;

public class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "CrosshairOverlay";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(AppName) != null;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (value)
            {
                var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetEntryAssembly()?.Location;
                if (!string.IsNullOrEmpty(exePath))
                    key?.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                key?.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
    }
}
