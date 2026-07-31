using CrosshairOverlay.Models;
using CrosshairOverlay.Services;
using Xunit;

namespace CrosshairOverlay.Tests;

public class AppServiceServerTests
{
    [Fact]
    public async Task StandaloneMode_FallsBackWithoutThrowing()
    {
        using var server = new AppServiceServer();

        await server.InitializeAsync(new CrosshairProfile { IsVisible = false });
        await server.PushProfile(new CrosshairProfile { IsVisible = true });
    }
}
