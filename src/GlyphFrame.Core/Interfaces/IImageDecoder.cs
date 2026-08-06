using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface IImageDecoder
{
    Task<DecodedImage> DecodeAsync(
        string inputPath,
        ImageOrientation orientation,
        CancellationToken cancellationToken = default);
}
