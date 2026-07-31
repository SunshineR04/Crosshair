using CrosshairOverlay.Models;
using Xunit;

namespace CrosshairOverlay.Tests;

public class CrosshairProfileRulesTests
{
    [Fact]
    public void Sanitize_ClampsNumericValuesAndKeepsGapWithinSize()
    {
        var profile = new CrosshairProfile
        {
            Thickness = -5,
            Size = 1,
            Gap = 1000,
            DotSize = 0,
            Opacity = double.NaN,
            OutlineThickness = 99
        };

        var sanitized = CrosshairProfileRules.Sanitize(profile);

        Assert.Equal(1, sanitized.Thickness);
        Assert.Equal(2, sanitized.Size);
        Assert.Equal(2, sanitized.Gap);
        Assert.Equal(1, sanitized.DotSize);
        Assert.Equal(1.0, sanitized.Opacity);
        Assert.Equal(10, sanitized.OutlineThickness);
    }

    [Fact]
    public void Sanitize_UsesSafeDefaultsForInvalidStyleAndColors()
    {
        var profile = new CrosshairProfile
        {
            Style = (CrosshairStyle)999,
            Color = "not-a-color",
            OutlineColor = "#GGGGGG"
        };

        var sanitized = CrosshairProfileRules.Sanitize(profile);

        Assert.Equal(CrosshairStyle.Cross, sanitized.Style);
        Assert.Equal("#00FF00", sanitized.Color);
        Assert.Equal("#000000", sanitized.OutlineColor);
    }

    [Fact]
    public void Sanitize_ReturnsASeparateSnapshot()
    {
        var profile = new CrosshairProfile { Color = "#abcdef" };

        var sanitized = CrosshairProfileRules.Sanitize(profile);

        Assert.NotSame(profile, sanitized);
        Assert.Equal("#ABCDEF", sanitized.Color);
        Assert.Equal("#abcdef", profile.Color);
    }
}
