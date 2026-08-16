using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

/// <summary>
/// Identifies the exact media source and render settings used for a symbolized preview.
/// </summary>
public sealed record PreviewIdentity(
    MediaSelectionKind MediaType,
    long MediaVersion,
    long SourceVersion,
    long SettingsVersion);
