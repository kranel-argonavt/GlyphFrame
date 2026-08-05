namespace GlyphFrame.Core.Enums;

public enum MediaInputErrorCategory
{
    None,
    EmptyPath,
    InvalidPath,
    FileNotFound,
    DirectorySelected,
    MultipleFiles,
    UnsupportedExtension,
    AccessDenied,
    FileInUse,
    InvalidImage,
    InvalidVideo,
    MetadataUnavailable,
    Cancelled,
    UnexpectedFailure
}
