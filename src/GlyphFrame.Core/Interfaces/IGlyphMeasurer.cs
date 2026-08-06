using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface IGlyphMeasurer
{
    GlyphMetrics Measure(string fontFamily, int fontSize, string characters);
}
