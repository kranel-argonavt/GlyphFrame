using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;
using GlyphFrame.Rendering.Conversion;
using GlyphFrame.Rendering.Mapping;
using GlyphFrame.Rendering.Skia;
using GlyphFrame.Tests.TestInfrastructure;
using SkiaSharp;

namespace GlyphFrame.Tests;

public sealed class StraightAlphaImagePipelineTests
{
    [Theory]
    [InlineData(SymbolPolarity.LightOnDark, 134, 7, 10)]
    [InlineData(SymbolPolarity.DarkOnLight, 250, 122, 121)]
    public async Task RealPng_IsDecodedAsStraightAlphaAndCompositedExactlyOnce(
        SymbolPolarity polarity,
        byte expectedRed,
        byte expectedGreen,
        byte expectedBlue)
    {
        using TemporaryMediaDirectory media = new();
        string path = Phase6TestImageFactory.CreateStraightAlphaPng(
            media,
            "alpha-regression.png",
            3,
            1,
            (x, _) => x switch
            {
                0 => new SKColor(17, 34, 51, 255),
                1 => new SKColor(255, 0, 0, 128),
                _ => new SKColor(73, 149, 211, 0)
            });
        SkiaImageDecoder decoder = new();
        FrameToSymbolConverter converter = new(new BrightnessToSymbolMapper());
        GlyphMetrics metrics = new("Test", "Test", 10, 1, 1, 1, false);
        CharacterPresetOption preset = new(CharacterPresetKind.SimpleAscii, "Test", " .#");

        DecodedImage decoded = await decoder.DecodeAsync(path, ImageOrientation.Normal);
        SymbolFrame frame = converter.Convert(
            decoded,
            new SymbolGrid(3, 1, new PixelDimensions(3, 1)),
            metrics,
            preset,
            polarity);

        Assert.Equal(new PixelColor(17, 34, 51), frame.Cells.Span[0].Color);
        Assert.Equal(new PixelColor(expectedRed, expectedGreen, expectedBlue), frame.Cells.Span[1].Color);
        Assert.Equal(SymbolBackgroundResolver.Resolve(polarity), frame.Cells.Span[2].Color);
        Assert.Equal(SymbolBackgroundResolver.Resolve(polarity), frame.BackgroundColor);
    }
}
