using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;
using GlyphFrame.Rendering.Grid;

namespace GlyphFrame.Tests;

public sealed class OutputDimensionResolverTests
{
    private readonly OutputDimensionResolver _resolver = new();

    [Theory]
    [InlineData(640, 480, OutputResolution.SameAsSource, 640, 480)]
    [InlineData(1920, 1080, OutputResolution.Hd720, 1280, 720)]
    [InlineData(1920, 1080, OutputResolution.FullHd1080, 1920, 1080)]
    [InlineData(1080, 1920, OutputResolution.Hd720, 720, 1280)]
    [InlineData(1080, 1920, OutputResolution.FullHd1080, 1080, 1920)]
    [InlineData(1000, 1000, OutputResolution.Hd720, 720, 720)]
    [InlineData(1000, 500, OutputResolution.Hd720, 1280, 640)]
    [InlineData(500, 1000, OutputResolution.Hd720, 640, 1280)]
    public void Resolve_PreservesAspectRatioWithinApprovedTarget(
        int sourceWidth,
        int sourceHeight,
        OutputResolution resolution,
        int expectedWidth,
        int expectedHeight)
    {
        PixelDimensions result = _resolver.Resolve(new PixelDimensions(sourceWidth, sourceHeight), resolution);

        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);
        Assert.Equal(sourceWidth / (double)sourceHeight, result.AspectRatio, precision: 3);
    }

    [Fact]
    public void PixelDimensions_RejectsInvalidDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PixelDimensions(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PixelDimensions(10, -1));
    }
}
