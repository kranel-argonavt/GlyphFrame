using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface IImageMetadataReader
{
    Task<ImageMetadata> ReadAsync(string inputPath, CancellationToken cancellationToken = default);
}
