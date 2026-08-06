using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Rendering.Grid;

public sealed class SymbolGridCalculator : ISymbolGridCalculator
{
    public SymbolGrid Calculate(PixelDimensions outputDimensions, GlyphMetrics glyphMetrics)
    {
        ArgumentNullException.ThrowIfNull(glyphMetrics);
        if (!glyphMetrics.IsValid || !float.IsFinite(glyphMetrics.AdvanceWidth) || !float.IsFinite(glyphMetrics.LineHeight))
        {
            throw new ArgumentException("Glyph metrics must contain positive finite dimensions.", nameof(glyphMetrics));
        }

        int columns = Math.Max(1, (int)Math.Floor(outputDimensions.Width / glyphMetrics.AdvanceWidth));
        int rows = Math.Max(1, (int)Math.Floor(outputDimensions.Height / glyphMetrics.LineHeight));
        return new SymbolGrid(columns, rows, outputDimensions);
    }
}
