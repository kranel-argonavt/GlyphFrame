using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface IAudioStreamProbe
{
    Task<AudioProbeResult> ProbeAsync(
        string inputPath,
        CancellationToken cancellationToken = default);
}
