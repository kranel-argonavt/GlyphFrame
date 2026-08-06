using GlyphFrame.Core.Models;
using GlyphFrame.Rendering.Grid;
using GlyphFrame.Rendering.Skia;

namespace GlyphFrame.Tests;

public sealed class GlyphAndGridTests
{
    private readonly SkiaGlyphMeasurer _measurer = new();
    private readonly SymbolGridCalculator _calculator = new();

    [Fact]
    public void Measure_ProducesPositiveMetricsAndSafeFallbackForMissingFont()
    {
        GlyphMetrics metrics = _measurer.Measure("GlyphFrame Definitely Missing Font", 10, " .#");

        Assert.True(metrics.IsValid);
        Assert.True(metrics.UsedFallback);
        Assert.False(string.IsNullOrWhiteSpace(metrics.ResolvedFontFamily));
        Assert.NotNull(metrics.DiagnosticMessage);
    }

    [Fact]
    public void Measure_BlocksPresetUsesTypefaceWithRequiredGlyphs()
    {
        GlyphMetrics metrics = _measurer.Measure("Cascadia Mono", 10, " ░▒▓█");

        Assert.True(metrics.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(metrics.ResolvedFontFamily));
    }

    [Fact]
    public void Calculate_SmallerFontProducesAtLeastAsDenseGrid()
    {
        PixelDimensions output = new(1280, 720);
        GlyphMetrics small = _measurer.Measure("Consolas", 9, " .#");
        GlyphMetrics large = _measurer.Measure("Consolas", 12, " .#");

        SymbolGrid smallGrid = _calculator.Calculate(output, small);
        SymbolGrid largeGrid = _calculator.Calculate(output, large);

        Assert.True(smallGrid.Columns >= largeGrid.Columns);
        Assert.True(smallGrid.Rows >= largeGrid.Rows);
        Assert.True(smallGrid.CellCount >= largeGrid.CellCount);
    }

    [Fact]
    public void Calculate_GridIsPositiveAndFitsOutputCanvas()
    {
        PixelDimensions output = new(17, 11);
        GlyphMetrics metrics = new("Test", "Test", 10, 6, 8, 6, false);

        SymbolGrid grid = _calculator.Calculate(output, metrics);

        Assert.True(grid.Columns > 0);
        Assert.True(grid.Rows > 0);
        Assert.True(grid.CellWidth >= metrics.AdvanceWidth || grid.Columns == 1);
        Assert.True(grid.CellHeight >= metrics.LineHeight || grid.Rows == 1);
        Assert.Equal(output, grid.OutputDimensions);
    }
}
