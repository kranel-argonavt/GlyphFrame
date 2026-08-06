namespace GlyphFrame.Core.Models;

public sealed record SymbolGrid
{
    public SymbolGrid(int columns, int rows, PixelDimensions outputDimensions)
    {
        if (columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }

        if (rows <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rows));
        }

        Columns = columns;
        Rows = rows;
        OutputDimensions = outputDimensions;
    }

    public int Columns { get; }

    public int Rows { get; }

    public PixelDimensions OutputDimensions { get; }

    public double CellWidth => OutputDimensions.Width / (double)Columns;

    public double CellHeight => OutputDimensions.Height / (double)Rows;

    public int CellCount => checked(Columns * Rows);
}
