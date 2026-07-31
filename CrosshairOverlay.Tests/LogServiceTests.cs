using CrosshairOverlay.Services;
using Xunit;

namespace CrosshairOverlay.Tests;

public class LogServiceTests
{
    [Fact]
    public void LoggingMethods_DoNotThrow()
    {
        var exception = new InvalidOperationException("test exception");

        LogService.Info("test info");
        LogService.Warn("test warning");
        LogService.Error("test error");
        LogService.Error("test exception", exception);
    }
}
