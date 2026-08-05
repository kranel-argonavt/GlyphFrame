using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface IVideoMetadataReader
{
    Task<VideoMetadata> ReadAsync(string inputPath, CancellationToken cancellationToken = default);
}
