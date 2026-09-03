using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

/// <summary>
/// Identifies every captured input that changes a completed symbolized video artifact.
/// Frame-preview position is intentionally excluded.
/// </summary>
public sealed record FullVideoRenderIdentity(
    long MediaVersion,
    long SourceIdentityVersion,
    long RenderSettingsVersion,
    CharacterPresetKind CharacterPreset,
    string CharacterRamp,
    string FontFamily,
    int FontSize,
    OutputResolution OutputResolution,
    SymbolPolarity SymbolPolarity,
    AudioExportMode AudioMode);
