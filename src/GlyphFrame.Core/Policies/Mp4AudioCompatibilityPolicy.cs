using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;

namespace GlyphFrame.Core.Policies;

public sealed class Mp4AudioCompatibilityPolicy : IAudioCompatibilityPolicy
{
    public AudioMuxStrategy ResolveStrategy(string? codecName) =>
        string.Equals(codecName?.Trim(), "aac", StringComparison.OrdinalIgnoreCase)
            ? AudioMuxStrategy.StreamCopy
            : AudioMuxStrategy.TranscodeToAac;
}
