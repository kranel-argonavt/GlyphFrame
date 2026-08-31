using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface IVideoEncoder
{
    /// <summary>
    /// Encodes the sequential stream and assumes ownership of each yielded rendered frame.
    /// Every accepted or rejected yielded frame is disposed before the encoder advances.
    /// </summary>
    Task<VideoEncodingResult> EncodeAsync(
        VideoEncodingJob job,
        IAsyncEnumerable<RenderedImage> frames,
        CancellationToken cancellationToken = default,
        IProgress<VideoEncodingProgress>? progress = null);
}
