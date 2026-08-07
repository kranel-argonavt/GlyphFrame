using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Settings;

public sealed record AppSettings
{
    public AccentPreset AccentPreset { get; init; } = AccentPreset.Violet;

    public CharacterPresetKind DefaultCharacterPreset { get; init; } = CharacterPresetKind.DetailedAscii;

    public string DefaultFontFamily { get; init; } = "Cascadia Mono";

    public int DefaultFontSize { get; init; } = 10;

    public OutputResolution DefaultOutputResolution { get; init; } = OutputResolution.SameAsSource;

    public SymbolPolarity DefaultSymbolPolarity { get; init; } = SymbolPolarity.LightOnDark;

    public ImageExportFormat DefaultImageExportFormat { get; init; } = ImageExportFormat.Png;

    public AudioExportMode DefaultVideoAudioMode { get; init; } = AudioExportMode.KeepAudio;
}
