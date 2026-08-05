using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;

namespace GlyphFrame.Core.Media;

public sealed class MediaTypeDetector : IMediaTypeDetector
{
    public MediaSelectionKind Detect(string path) => SupportedMediaFormats.DetectFromExtension(path);
}
