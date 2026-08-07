using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface IFrameToSymbolConverter
{
    SymbolFrame Convert(
        DecodedImage source,
        SymbolGrid grid,
        GlyphMetrics glyphMetrics,
        CharacterPresetOption preset,
        SymbolPolarity polarity,
        CancellationToken cancellationToken = default);
}
