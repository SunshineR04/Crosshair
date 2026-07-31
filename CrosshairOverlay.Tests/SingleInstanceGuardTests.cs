using CrosshairOverlay.Services;
using Xunit;

namespace CrosshairOverlay.Tests;

public class SingleInstanceGuardTests
{
    [Fact]
    public void TryCreate_AllowsOnlyOneGuardAtATime()
    {
        var first = SingleInstanceGuard.TryCreate();
        Assert.NotNull(first);

        try
        {
            var second = SingleInstanceGuard.TryCreate();
            Assert.Null(second);
        }
        finally
        {
            first!.Dispose();
        }

        using var afterRelease = SingleInstanceGuard.TryCreate();
        Assert.NotNull(afterRelease);
    }
}
