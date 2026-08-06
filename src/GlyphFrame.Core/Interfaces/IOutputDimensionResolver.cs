using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface IOutputDimensionResolver
{
    PixelDimensions Resolve(PixelDimensions sourceDimensions, OutputResolution resolution);
}
