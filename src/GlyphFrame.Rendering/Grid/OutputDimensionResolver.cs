using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Rendering.Grid;

public sealed class OutputDimensionResolver : IOutputDimensionResolver
{
    public PixelDimensions Resolve(PixelDimensions sourceDimensions, OutputResolution resolution)
    {
        if (resolution == OutputResolution.SameAsSource)
        {
            return sourceDimensions;
        }

        (int maximumWidth, int maximumHeight) = (resolution, sourceDimensions.IsPortrait) switch
        {
            (OutputResolution.Hd720, false) => (1280, 720),
            (OutputResolution.Hd720, true) => (720, 1280),
            (OutputResolution.FullHd1080, false) => (1920, 1080),
            (OutputResolution.FullHd1080, true) => (1080, 1920),
            _ => throw new ArgumentOutOfRangeException(nameof(resolution), resolution, "Unsupported output resolution.")
        };

        double scale = Math.Min(
            maximumWidth / (double)sourceDimensions.Width,
            maximumHeight / (double)sourceDimensions.Height);

        int width = Math.Clamp((int)Math.Round(sourceDimensions.Width * scale), 1, maximumWidth);
        int height = Math.Clamp((int)Math.Round(sourceDimensions.Height * scale), 1, maximumHeight);
        return new PixelDimensions(width, height);
    }
}
