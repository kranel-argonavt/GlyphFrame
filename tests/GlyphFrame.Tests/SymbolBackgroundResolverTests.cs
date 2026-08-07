using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Tests;

public sealed class SymbolBackgroundResolverTests
{
    [Fact]
    public void Resolve_ReturnsExactCentralizedBackgrounds()
    {
        Assert.Equal(new PixelColor(13, 15, 20), PixelColor.SymbolBackground);
        Assert.Equal(new PixelColor(245, 245, 242), PixelColor.SymbolLightBackground);
        Assert.Equal(PixelColor.SymbolBackground, SymbolBackgroundResolver.Resolve(SymbolPolarity.LightOnDark));
        Assert.Equal(PixelColor.SymbolLightBackground, SymbolBackgroundResolver.Resolve(SymbolPolarity.DarkOnLight));
    }

    [Fact]
    public void Resolve_RejectsUndefinedPolarity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SymbolBackgroundResolver.Resolve((SymbolPolarity)999));
    }
}
