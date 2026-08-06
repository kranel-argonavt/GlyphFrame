using System.Windows.Media;
using GlyphFrame.Core.Models;

namespace GlyphFrame.App.Services;

public interface IImageSourceFactory
{
    ImageSource Create(DecodedImage image);

    ImageSource Create(RenderedImage image);
}
