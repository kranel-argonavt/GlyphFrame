using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface ISymbolGridCalculator
{
    SymbolGrid Calculate(PixelDimensions outputDimensions, GlyphMetrics glyphMetrics);
}
