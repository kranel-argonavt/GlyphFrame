using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Interfaces;

public interface IMediaTypeDetector
{
    MediaSelectionKind Detect(string path);
}
