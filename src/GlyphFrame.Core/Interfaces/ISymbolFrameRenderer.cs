using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface ISymbolFrameRenderer
{
    RenderedImage Render(SymbolFrame frame, CancellationToken cancellationToken = default);
}
