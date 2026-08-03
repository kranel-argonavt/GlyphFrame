using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Settings;

public static class AppSettingsNormalizer
{
    public static SettingsNormalizationResult Normalize(AppSettings? settings)
    {
        AppSettings defaults = AppSettingsCatalog.Defaults;
        settings ??= defaults;

        string normalizedFont = AppSettingsCatalog.FontFamilies.FirstOrDefault(
            font => string.Equals(font, settings.DefaultFontFamily, StringComparison.OrdinalIgnoreCase))
            ?? defaults.DefaultFontFamily;

        AppSettings normalized = settings with
        {
            AccentPreset = IsSupported(settings.AccentPreset)
                ? settings.AccentPreset
                : defaults.AccentPreset,
            DefaultCharacterPreset = IsSupported(settings.DefaultCharacterPreset)
                ? settings.DefaultCharacterPreset
                : defaults.DefaultCharacterPreset,
            DefaultFontFamily = normalizedFont,
            DefaultFontSize = AppSettingsCatalog.FontSizes.Contains(settings.DefaultFontSize)
                ? settings.DefaultFontSize
                : defaults.DefaultFontSize,
            DefaultOutputResolution = IsSupported(settings.DefaultOutputResolution)
                ? settings.DefaultOutputResolution
                : defaults.DefaultOutputResolution,
            DefaultImageExportFormat = IsSupported(settings.DefaultImageExportFormat)
                ? settings.DefaultImageExportFormat
                : defaults.DefaultImageExportFormat,
            DefaultVideoAudioMode = IsSupported(settings.DefaultVideoAudioMode)
                ? settings.DefaultVideoAudioMode
                : defaults.DefaultVideoAudioMode
        };

        return new SettingsNormalizationResult(normalized, normalized != settings);
    }

    private static bool IsSupported(AccentPreset value) =>
        AppSettingsCatalog.AccentPresets.Any(option => option.Value == value);

    private static bool IsSupported(CharacterPresetKind value) =>
        AppSettingsCatalog.CharacterPresets.Any(option => option.Kind == value);

    private static bool IsSupported(OutputResolution value) =>
        AppSettingsCatalog.OutputResolutions.Any(option => option.Value == value);

    private static bool IsSupported(ImageExportFormat value) =>
        AppSettingsCatalog.ImageExportFormats.Any(option => option.Value == value);

    private static bool IsSupported(AudioExportMode value) =>
        AppSettingsCatalog.AudioModes.Any(option => option.Value == value);
}

public sealed record SettingsNormalizationResult(AppSettings Settings, bool WasChanged);
