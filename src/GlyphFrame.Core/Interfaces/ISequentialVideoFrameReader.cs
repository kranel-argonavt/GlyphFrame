using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface ISequentialVideoFrameReader
{
    IAsyncEnumerable<DecodedVideoFrame> ReadFramesAsync(
        SequentialVideoDecodeJob job,
        CancellationToken cancellationToken = default);
}
