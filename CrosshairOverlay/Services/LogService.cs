using System.IO;

namespace CrosshairOverlay.Services;

/// <summary>
/// 轻量级文件日志服务。在 Debug 和 Release 模式下均写入日志文件，
/// 同时输出到 Debug 控制台（仅 Debug 模式可见）。
/// </summary>
public static class LogService
{
    private static readonly string LogDir = SettingsService.AppDataDir;
    private static readonly string LogFile = Path.Combine(LogDir, "log.txt");
    private static readonly object Lock = new();

    /// <summary>记录信息级别日志。</summary>
    public static void Info(string message) => Write("INFO", message);

    /// <summary>记录警告级别日志。</summary>
    public static void Warn(string message) => Write("WARN", message);

    /// <summary>记录错误级别日志。</summary>
    public static void Error(string message) => Write("ERROR", message);

    /// <summary>记录错误级别日志（含异常详情）。</summary>
    public static void Error(string message, Exception ex) => Write("ERROR", $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level} {message}";

        System.Diagnostics.Debug.WriteLine($"[CrosshairOverlay] {line}");

        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(LogFile, line + Environment.NewLine);
            }
        }
        catch
        {
            // 日志写入失败不应影响主流程
        }
    }
}
