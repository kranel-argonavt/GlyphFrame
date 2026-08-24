using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface IAudioMuxer
{
    Task<AudioMuxResult> MuxAsync(
        AudioMuxJob job,
        CancellationToken cancellationToken = default);
}
