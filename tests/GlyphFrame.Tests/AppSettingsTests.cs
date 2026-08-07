using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Settings;

namespace GlyphFrame.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void Defaults_MatchEveryApprovedPhase4Value()
    {
        AppSettings settings = AppSettingsCatalog.Defaults;

        Assert.Equal(AccentPreset.Violet, settings.AccentPreset);
        Assert.Equal(CharacterPresetKind.DetailedAscii, settings.DefaultCharacterPreset);
        Assert.Equal("Cascadia Mono", settings.DefaultFontFamily);
        Assert.Equal(10, settings.DefaultFontSize);
        Assert.Equal(OutputResolution.SameAsSource, settings.DefaultOutputResolution);
        Assert.Equal(SymbolPolarity.LightOnDark, settings.DefaultSymbolPolarity);
        Assert.Equal(ImageExportFormat.Png, settings.DefaultImageExportFormat);
        Assert.Equal(AudioExportMode.KeepAudio, settings.DefaultVideoAudioMode);
    }

    [Fact]
    public void Normalize_ReplacesUnsupportedValuesWhilePreservingValidValues()
    {
        AppSettings invalid = new()
        {
            AccentPreset = (AccentPreset)999,
            DefaultCharacterPreset = CharacterPresetKind.Blocks,
            DefaultFontFamily = "Comic Sans MS",
            DefaultFontSize = 48,
            DefaultOutputResolution = OutputResolution.Hd720,
            DefaultSymbolPolarity = (SymbolPolarity)999,
            DefaultImageExportFormat = (ImageExportFormat)999,
            DefaultVideoAudioMode = AudioExportMode.RemoveAudio
        };

        SettingsNormalizationResult result = AppSettingsNormalizer.Normalize(invalid);

        Assert.True(result.WasChanged);
        Assert.Equal(AccentPreset.Violet, result.Settings.AccentPreset);
        Assert.Equal(CharacterPresetKind.Blocks, result.Settings.DefaultCharacterPreset);
        Assert.Equal("Cascadia Mono", result.Settings.DefaultFontFamily);
        Assert.Equal(10, result.Settings.DefaultFontSize);
        Assert.Equal(OutputResolution.Hd720, result.Settings.DefaultOutputResolution);
        Assert.Equal(SymbolPolarity.LightOnDark, result.Settings.DefaultSymbolPolarity);
        Assert.Equal(ImageExportFormat.Png, result.Settings.DefaultImageExportFormat);
        Assert.Equal(AudioExportMode.RemoveAudio, result.Settings.DefaultVideoAudioMode);
    }

    [Fact]
    public void Catalog_ContainsAllApprovedChoices()
    {
        Assert.Equal(6, AppSettingsCatalog.AccentPresets.Count);
        Assert.Equal(4, AppSettingsCatalog.FontFamilies.Count);
        Assert.Equal([9, 10, 12], AppSettingsCatalog.FontSizes);
        Assert.Equal(3, AppSettingsCatalog.OutputResolutions.Count);
        Assert.Equal(2, AppSettingsCatalog.SymbolPolarities.Count);
        Assert.Equal(2, AppSettingsCatalog.ImageExportFormats.Count);
        Assert.Equal(2, AppSettingsCatalog.AudioModes.Count);
    }
}
