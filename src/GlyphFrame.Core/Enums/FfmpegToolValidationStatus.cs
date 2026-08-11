namespace GlyphFrame.Core.Enums;

public enum FfmpegToolValidationStatus
{
    Available,
    Missing,
    InvalidPath,
    AccessDenied,
    StartFailed,
    NonZeroExitCode,
    TimedOut,
    InvalidVersionOutput
}
