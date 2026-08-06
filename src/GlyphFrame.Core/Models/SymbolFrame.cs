namespace GlyphFrame.Core.Models;

public sealed class SymbolFrame
{
    private readonly SymbolCell[] _cells;

    public SymbolFrame(SymbolGrid grid, GlyphMetrics glyphMetrics, SymbolCell[] cells)
    {
        Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        GlyphMetrics = glyphMetrics ?? throw new ArgumentNullException(nameof(glyphMetrics));
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Length != grid.CellCount)
        {
            throw new ArgumentException($"Expected {grid.CellCount} symbol cells.", nameof(cells));
        }

        _cells = cells;
    }

    public SymbolGrid Grid { get; }

    public GlyphMetrics GlyphMetrics { get; }

    public ReadOnlyMemory<SymbolCell> Cells => _cells;
}
