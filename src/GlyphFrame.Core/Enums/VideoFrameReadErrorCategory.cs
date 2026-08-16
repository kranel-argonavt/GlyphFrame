namespace GlyphFrame.Core.Enums;

public enum VideoFrameReadErrorCategory
{
    None,
    InvalidPath,
    FileNotFound,
    AccessDenied,
    InvalidMedia,
    OpenFailed,
    NoVideoStream,
    DecodeFailed,
    DecoderUnavailable,
    ProcessStartFailed,
    TimedOut,
    InvalidDimensions,
    IncompleteOutput,
    MetadataUnavailable,
    Cancelled,
    UnexpectedFailure
}
