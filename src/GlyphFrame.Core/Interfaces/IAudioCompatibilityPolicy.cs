using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Interfaces;

public interface IAudioCompatibilityPolicy
{
    AudioMuxStrategy ResolveStrategy(string? codecName);
}
