using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface IVideoEncoder
{
    Task<VideoEncodingResult> EncodeAsync(
        VideoEncodingJob job,
        IAsyncEnumerable<RenderedImage> frames,
        CancellationToken cancellationToken = default,
        IProgress<VideoEncodingProgress>? progress = null);
}
