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
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(AppName) != null;
            }
            catch (Exception ex)
            {
                LogService.Warn($"Unable to read startup setting: {ex.Message}");
                return false;
            }
        }
        set
        {
            TrySetEnabled(value, out _);
        }
    }

    public static bool TrySetEnabled(bool enabled, out string error)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key == null)
            {
                error = "无法打开当前用户的启动项注册表路径。";
                return false;
            }

            if (enabled)
            {
                var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetEntryAssembly()?.Location;
                if (string.IsNullOrEmpty(exePath))
                {
                    error = "无法确定当前程序路径。";
                    return false;
                }

                key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }

            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            LogService.Error("Unable to update startup setting", ex);
            error = ex.Message;
            return false;
        }
    }
}
