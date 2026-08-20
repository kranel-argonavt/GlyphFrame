namespace GlyphFrame.Core.Enums;

public enum VideoExportErrorCategory
{
    None,
    InvalidInput,
    InvalidOutput,
    DecoderUnavailable,
    MetadataUnavailable,
    InvalidDimensions,
    DecodeFailed,
    TruncatedFrame,
    RenderingFailed,
    EncoderStartFailed,
    EncoderWriteFailed,
    EncoderFailed,
    OutputFinalizationFailed,
    Cancelled,
    UnexpectedFailure
}
