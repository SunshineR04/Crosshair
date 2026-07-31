using CrosshairOverlay.Models;
using CrosshairOverlay.Rendering;
using Xunit;

namespace CrosshairOverlay.Tests;

public class CrosshairRendererTests
{
    [Theory]
    [InlineData(CrosshairStyle.Cross)]
    [InlineData(CrosshairStyle.Dot)]
    [InlineData(CrosshairStyle.CrossDot)]
    [InlineData(CrosshairStyle.Circle)]
    [InlineData(CrosshairStyle.CircleDot)]
    [InlineData(CrosshairStyle.Outline)]
    public void Render_ProducesNonEmptyPremultipliedBitmap(CrosshairStyle style)
    {
        var profile = new CrosshairProfile
        {
            Style = style,
            Size = 24,
            Thickness = 3,
            Gap = 4,
            DotSize = 6,
            Opacity = 1.0,
            OutlineEnabled = true
        };

        var renderer = new CrosshairRenderer();
        using var bitmap = renderer.Render(profile, dpiScale: 1.5f);

        Assert.Equal(108, bitmap.Width);
        Assert.Equal(108, bitmap.Height);
        Assert.Equal(SkiaSharp.SKAlphaType.Premul, bitmap.AlphaType);
        Assert.Contains(bitmap.Pixels, pixel => pixel.Alpha > 0);
    }
}
