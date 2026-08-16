using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface IVideoFrameReader
{
    Task<VideoFrameReadResult> ReadAsync(
        string inputPath,
        double normalizedPosition,
        VideoMetadata? metadataContext = null,
        CancellationToken cancellationToken = default);
}
