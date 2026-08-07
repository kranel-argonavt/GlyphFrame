namespace GlyphFrame.Core.Models;

public sealed class SymbolFrame
{
    private readonly SymbolCell[] _cells;

    public SymbolFrame(
        SymbolGrid grid,
        GlyphMetrics glyphMetrics,
        SymbolCell[] cells,
        PixelColor backgroundColor)
    {
        Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        GlyphMetrics = glyphMetrics ?? throw new ArgumentNullException(nameof(glyphMetrics));
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Length != grid.CellCount)
        {
            throw new ArgumentException($"Expected {grid.CellCount} symbol cells.", nameof(cells));
        }
        if (backgroundColor.Alpha != byte.MaxValue)
        {
            throw new ArgumentException("Symbol frame backgrounds must be opaque.", nameof(backgroundColor));
        }

        _cells = cells;
        BackgroundColor = backgroundColor;
    }

    public SymbolGrid Grid { get; }

    public GlyphMetrics GlyphMetrics { get; }

    public PixelColor BackgroundColor { get; }

    public ReadOnlyMemory<SymbolCell> Cells => _cells;
}
