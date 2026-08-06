namespace GlyphFrame.Core.Models;

public sealed record ImagePipelineResult(
    RenderedImage RenderedImage,
    SymbolGrid Grid,
    GlyphMetrics GlyphMetrics);
