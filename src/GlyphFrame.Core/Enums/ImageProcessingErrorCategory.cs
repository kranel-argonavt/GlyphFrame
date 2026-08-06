namespace GlyphFrame.Core.Enums;

public enum ImageProcessingErrorCategory
{
    InvalidPath,
    FileNotFound,
    AccessDenied,
    InvalidImage,
    UnsupportedDimensions,
    InvalidSettings,
    FontUnavailable,
    RenderingFailed,
    OutputUnavailable,
    EncodingFailed,
    Cancelled,
    UnexpectedFailure
}
