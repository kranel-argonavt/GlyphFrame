using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;
using GlyphFrame.Rendering.Conversion;
using GlyphFrame.Rendering.Mapping;

namespace GlyphFrame.Tests;

public sealed class FrameToSymbolConverterTests
{
    private readonly FrameToSymbolConverter _converter = new(new BrightnessToSymbolMapper());
    private static readonly GlyphMetrics Metrics = new("Test", "Test", 10, 1, 1, 1, false);
    private static readonly CharacterPresetOption Preset = new(CharacterPresetKind.SimpleAscii, "Test", " .#");

    [Fact]
    public void Convert_MapsBlackAndWhiteCellsInExpectedDirection()
    {
        DecodedImage source = new(2, 1,
        [
            0, 0, 0, 255,
            255, 255, 255, 255
        ]);
        SymbolGrid grid = new(2, 1, new PixelDimensions(2, 1));

        SymbolFrame result = _converter.Convert(source, grid, Metrics, Preset, SymbolPolarity.LightOnDark);

        Assert.Equal(' ', result.Cells.Span[0].Character);
        Assert.Equal('#', result.Cells.Span[1].Character);
        Assert.Equal((byte)0, result.Cells.Span[0].Luminance);
        Assert.Equal((byte)255, result.Cells.Span[1].Luminance);
    }

    [Fact]
    public void Convert_AveragesEveryPixelInCellAndPreservesColor()
    {
        DecodedImage source = new(2, 2,
        [
            255, 0, 0, 255,   0, 255, 0, 255,
            0, 0, 255, 255,   255, 255, 255, 255
        ]);
        SymbolGrid grid = new(1, 1, new PixelDimensions(10, 10));

        SymbolCell cell = _converter.Convert(source, grid, Metrics, Preset, SymbolPolarity.LightOnDark).Cells.Span[0];

        Assert.Equal(new PixelColor(127, 127, 127), cell.Color);
        Assert.Equal((byte)127, cell.Luminance);
    }

    [Fact]
    public void Convert_CompositesTransparentPixelsOverGlyphFrameBackground()
    {
        DecodedImage source = new(1, 1, [255, 0, 0, 0]);
        SymbolGrid grid = new(1, 1, new PixelDimensions(1, 1));

        SymbolFrame frame = _converter.Convert(source, grid, Metrics, Preset, SymbolPolarity.LightOnDark);
        SymbolCell cell = frame.Cells.Span[0];

        Assert.Equal(PixelColor.SymbolBackground, cell.Color);
        Assert.Equal(PixelColor.SymbolBackground, frame.BackgroundColor);
    }

    [Fact]
    public void Convert_CompositesStraightAlphaExactlyOnceWithIntegerRounding()
    {
        DecodedImage source = new(1, 1, [255, 0, 0, 128]);
        SymbolGrid grid = new(1, 1, new PixelDimensions(1, 1));

        SymbolCell cell = _converter.Convert(source, grid, Metrics, Preset, SymbolPolarity.LightOnDark).Cells.Span[0];

        Assert.Equal(new PixelColor(134, 7, 10), cell.Color);
    }

    [Fact]
    public void Convert_IgnoresRgbChannelsOfFullyTransparentPixels()
    {
        DecodedImage first = new(1, 1, [255, 0, 0, 0]);
        DecodedImage second = new(1, 1, [17, 231, 99, 0]);
        SymbolGrid grid = new(1, 1, new PixelDimensions(1, 1));

        SymbolCell firstCell = _converter.Convert(first, grid, Metrics, Preset, SymbolPolarity.LightOnDark).Cells.Span[0];
        SymbolCell secondCell = _converter.Convert(second, grid, Metrics, Preset, SymbolPolarity.LightOnDark).Cells.Span[0];

        Assert.Equal(PixelColor.SymbolBackground, firstCell.Color);
        Assert.Equal(firstCell.Color, secondCell.Color);
    }

    [Fact]
    public void Convert_HonorsCancellation()
    {
        DecodedImage source = new(1, 1, [0, 0, 0, 255]);
        SymbolGrid grid = new(1, 1, new PixelDimensions(1, 1));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            _converter.Convert(source, grid, Metrics, Preset, SymbolPolarity.LightOnDark, cancellation.Token));
    }

    [Theory]
    [InlineData(SymbolPolarity.LightOnDark, 134, 7, 10)]
    [InlineData(SymbolPolarity.DarkOnLight, 250, 122, 121)]
    public void Convert_CompositesStraightAlphaOverPolarityBackground(
        SymbolPolarity polarity,
        byte expectedRed,
        byte expectedGreen,
        byte expectedBlue)
    {
        DecodedImage source = new(1, 1, [255, 0, 0, 128]);
        SymbolGrid grid = new(1, 1, new PixelDimensions(1, 1));

        SymbolFrame frame = _converter.Convert(source, grid, Metrics, Preset, polarity);

        Assert.Equal(new PixelColor(expectedRed, expectedGreen, expectedBlue), frame.Cells.Span[0].Color);
        Assert.Equal(SymbolBackgroundResolver.Resolve(polarity), frame.BackgroundColor);
    }

    [Theory]
    [InlineData(SymbolPolarity.LightOnDark, " ..##")]
    [InlineData(SymbolPolarity.DarkOnLight, "#..  ")]
    public void Convert_MapsGrayScaleFromSparseToDenseByPolarity(
        SymbolPolarity polarity,
        string expectedCharacters)
    {
        DecodedImage source = new(5, 1,
        [
            0, 0, 0, 255,
            64, 64, 64, 255,
            128, 128, 128, 255,
            192, 192, 192, 255,
            255, 255, 255, 255
        ]);
        SymbolGrid grid = new(5, 1, new PixelDimensions(5, 1));

        SymbolFrame frame = _converter.Convert(source, grid, Metrics, Preset, polarity);

        Assert.Equal(expectedCharacters, new string(frame.Cells.Span.ToArray().Select(cell => cell.Character).ToArray()));
    }
}
