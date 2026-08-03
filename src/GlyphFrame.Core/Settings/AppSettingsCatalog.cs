using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Settings;

public static class AppSettingsCatalog
{
    public static AppSettings Defaults => new();

    public static IReadOnlyList<SettingOption<AccentPreset>> AccentPresets { get; } =
    [
        new(AccentPreset.Violet, "Violet"),
        new(AccentPreset.Purple, "Purple"),
        new(AccentPreset.Blue, "Blue"),
        new(AccentPreset.Cyan, "Cyan"),
        new(AccentPreset.Emerald, "Emerald"),
        new(AccentPreset.Rose, "Rose")
    ];

    public static IReadOnlyList<CharacterPresetOption> CharacterPresets => CharacterPresetOption.BuiltIn;

    public static IReadOnlyList<string> FontFamilies { get; } =
        ["Cascadia Mono", "Consolas", "Courier New", "Lucida Console"];

    public static IReadOnlyList<int> FontSizes { get; } = [9, 10, 12];

    public static IReadOnlyList<SettingOption<OutputResolution>> OutputResolutions { get; } =
    [
        new(OutputResolution.SameAsSource, "Same as source"),
        new(OutputResolution.Hd720, "720p"),
        new(OutputResolution.FullHd1080, "1080p")
    ];

    public static IReadOnlyList<SettingOption<ImageExportFormat>> ImageExportFormats { get; } =
    [
        new(ImageExportFormat.Png, "PNG"),
        new(ImageExportFormat.Jpeg, "JPG/JPEG")
    ];

    public static IReadOnlyList<SettingOption<AudioExportMode>> AudioModes { get; } =
    [
        new(AudioExportMode.KeepAudio, "Keep Audio"),
        new(AudioExportMode.RemoveAudio, "Remove Audio")
    ];

    public static string GetDisplayName(AccentPreset value) =>
        AccentPresets.First(option => option.Value == value).DisplayName;

    public static string GetDisplayName(OutputResolution value) =>
        OutputResolutions.First(option => option.Value == value).DisplayName;

    public static string GetDisplayName(ImageExportFormat value) =>
        ImageExportFormats.First(option => option.Value == value).DisplayName;

    public static string GetDisplayName(AudioExportMode value) =>
        AudioModes.First(option => option.Value == value).DisplayName;
}
