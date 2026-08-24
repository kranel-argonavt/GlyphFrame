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
    AudioMetadataUnavailable,
    AudioProbeFailed,
    AudioMuxStartFailed,
    AudioStreamCopyFailed,
    AudioTranscodeFailed,
    AudioMuxFailed,
    OutputFinalizationFailed,
    Cancelled,
    UnexpectedFailure
}
