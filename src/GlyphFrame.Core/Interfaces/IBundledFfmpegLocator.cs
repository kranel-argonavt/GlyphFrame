using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface IBundledFfmpegLocator
{
    BundledFfmpegLocation Locate();
}
