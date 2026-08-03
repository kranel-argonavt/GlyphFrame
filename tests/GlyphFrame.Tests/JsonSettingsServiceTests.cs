using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Settings;
using GlyphFrame.Infrastructure.Settings;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class JsonSettingsServiceTests
{
    [Fact]
    public async Task FirstLoad_UsesDefaultsAndCreatesApplicationDirectories()
    {
        using TemporaryApplicationData temporaryData = new();
        using JsonSettingsService service = new(temporaryData.Paths);

        SettingsLoadResult result = await service.LoadAsync();

        Assert.Equal(AppSettingsCatalog.Defaults, result.Settings);
        Assert.Equal(SettingsLoadState.DefaultsUsed, result.State);
        Assert.False(File.Exists(temporaryData.Paths.SettingsFilePath));
        Assert.True(Directory.Exists(temporaryData.Paths.LogsDirectory));
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsReadableEnumNames()
    {
        using TemporaryApplicationData temporaryData = new();
        using JsonSettingsService service = new(temporaryData.Paths);
        AppSettings expected = CreateAlternateSettings();

        await service.SaveAsync(expected);
        SettingsLoadResult result = await service.LoadAsync();
        string json = await File.ReadAllTextAsync(temporaryData.Paths.SettingsFilePath);

        Assert.Equal(expected, result.Settings);
        Assert.Equal(SettingsLoadState.Loaded, result.State);
        Assert.Contains("\"accentPreset\": \"Cyan\"", json, StringComparison.Ordinal);
        Assert.Contains("\"defaultOutputResolution\": \"FullHd1080\"", json, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(temporaryData.Paths.ApplicationDataDirectory, "settings.json.tmp")));
    }

    [Fact]
    public async Task PartialJson_UsesDefaultsOnlyForMissingProperties()
    {
        using TemporaryApplicationData temporaryData = new();
        temporaryData.Paths.EnsureDirectories();
        await File.WriteAllTextAsync(
            temporaryData.Paths.SettingsFilePath,
            """
            {
              "accentPreset": "Blue",
              "defaultFontFamily": "Consolas"
            }
            """);
        using JsonSettingsService service = new(temporaryData.Paths);

        SettingsLoadResult result = await service.LoadAsync();

        Assert.Equal(AccentPreset.Blue, result.Settings.AccentPreset);
        Assert.Equal("Consolas", result.Settings.DefaultFontFamily);
        Assert.Equal(CharacterPresetKind.DetailedAscii, result.Settings.DefaultCharacterPreset);
        Assert.Equal(10, result.Settings.DefaultFontSize);
        Assert.Equal(OutputResolution.SameAsSource, result.Settings.DefaultOutputResolution);
    }

    [Fact]
    public async Task InvalidValues_AreNormalizedWithoutDiscardingValidProperties()
    {
        using TemporaryApplicationData temporaryData = new();
        temporaryData.Paths.EnsureDirectories();
        await File.WriteAllTextAsync(
            temporaryData.Paths.SettingsFilePath,
            """
            {
              "accentPreset": "Orange",
              "defaultCharacterPreset": "Blocks",
              "defaultFontFamily": "Papyrus",
              "defaultFontSize": 72,
              "defaultOutputResolution": "Hd720",
              "defaultImageExportFormat": "Tiff",
              "defaultVideoAudioMode": "RemoveAudio"
            }
            """);
        using JsonSettingsService service = new(temporaryData.Paths);

        SettingsLoadResult result = await service.LoadAsync();

        Assert.Equal(SettingsLoadState.Normalized, result.State);
        Assert.Equal(AccentPreset.Violet, result.Settings.AccentPreset);
        Assert.Equal(CharacterPresetKind.Blocks, result.Settings.DefaultCharacterPreset);
        Assert.Equal("Cascadia Mono", result.Settings.DefaultFontFamily);
        Assert.Equal(10, result.Settings.DefaultFontSize);
        Assert.Equal(OutputResolution.Hd720, result.Settings.DefaultOutputResolution);
        Assert.Equal(ImageExportFormat.Png, result.Settings.DefaultImageExportFormat);
        Assert.Equal(AudioExportMode.RemoveAudio, result.Settings.DefaultVideoAudioMode);
        Assert.True(File.Exists(temporaryData.Paths.SettingsFilePath));
    }

    [Fact]
    public async Task MalformedJson_IsPreservedAndDoesNotCrashLoad()
    {
        using TemporaryApplicationData temporaryData = new();
        temporaryData.Paths.EnsureDirectories();
        const string malformedJson = "{ not valid json";
        await File.WriteAllTextAsync(temporaryData.Paths.SettingsFilePath, malformedJson);
        using JsonSettingsService service = new(temporaryData.Paths);

        SettingsLoadResult result = await service.LoadAsync();

        string invalidPath = Path.Combine(temporaryData.Paths.ApplicationDataDirectory, "settings.invalid.json");
        Assert.Equal(AppSettingsCatalog.Defaults, result.Settings);
        Assert.Equal(SettingsLoadState.RecoveredFromInvalidFile, result.State);
        Assert.Equal(invalidPath, result.PreservedInvalidFilePath);
        Assert.False(File.Exists(temporaryData.Paths.SettingsFilePath));
        Assert.Equal(malformedJson, await File.ReadAllTextAsync(invalidPath));
    }

    [Fact]
    public async Task EmptySettingsFile_IsPreservedAndFallsBackToDefaults()
    {
        using TemporaryApplicationData temporaryData = new();
        temporaryData.Paths.EnsureDirectories();
        await File.WriteAllTextAsync(temporaryData.Paths.SettingsFilePath, string.Empty);
        using JsonSettingsService service = new(temporaryData.Paths);

        SettingsLoadResult result = await service.LoadAsync();

        Assert.Equal(AppSettingsCatalog.Defaults, result.Settings);
        Assert.Equal(SettingsLoadState.RecoveredFromInvalidFile, result.State);
        Assert.NotNull(result.PreservedInvalidFilePath);
        Assert.True(File.Exists(result.PreservedInvalidFilePath));
    }

    [Fact]
    public async Task Reset_RestoresAndPersistsEveryDefaultWithoutDeletingLogs()
    {
        using TemporaryApplicationData temporaryData = new();
        using JsonSettingsService service = new(temporaryData.Paths);
        await service.SaveAsync(CreateAlternateSettings());
        string logSentinel = Path.Combine(temporaryData.Paths.LogsDirectory, "keep.me");
        await File.WriteAllTextAsync(logSentinel, "test-owned sentinel");

        AppSettings reset = await service.ResetAsync();
        SettingsLoadResult reloaded = await service.LoadAsync();

        Assert.Equal(AppSettingsCatalog.Defaults, reset);
        Assert.Equal(AppSettingsCatalog.Defaults, reloaded.Settings);
        Assert.True(File.Exists(logSentinel));
    }

    [Fact]
    public async Task CloselySpacedSaves_RemainACompleteValidSettingsDocument()
    {
        using TemporaryApplicationData temporaryData = new();
        using JsonSettingsService service = new(temporaryData.Paths);
        AppSettings first = CreateAlternateSettings();
        AppSettings second = AppSettingsCatalog.Defaults with { AccentPreset = AccentPreset.Rose };

        await Task.WhenAll(service.SaveAsync(first), service.SaveAsync(second));
        SettingsLoadResult result = await service.LoadAsync();

        Assert.True(result.Settings == first || result.Settings == second);
        Assert.Equal(SettingsLoadState.Loaded, result.State);
        Assert.False(File.Exists(Path.Combine(temporaryData.Paths.ApplicationDataDirectory, "settings.json.tmp")));
    }

    [Fact]
    public async Task CancelledSave_DoesNotCreateSettingsOrTemporaryFiles()
    {
        using TemporaryApplicationData temporaryData = new();
        using JsonSettingsService service = new(temporaryData.Paths);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.SaveAsync(CreateAlternateSettings(), cancellation.Token));

        Assert.False(File.Exists(temporaryData.Paths.SettingsFilePath));
        Assert.False(File.Exists(Path.Combine(temporaryData.Paths.ApplicationDataDirectory, "settings.json.tmp")));
    }

    private static AppSettings CreateAlternateSettings() => new()
    {
        AccentPreset = AccentPreset.Cyan,
        DefaultCharacterPreset = CharacterPresetKind.Blocks,
        DefaultFontFamily = "Consolas",
        DefaultFontSize = 12,
        DefaultOutputResolution = OutputResolution.FullHd1080,
        DefaultImageExportFormat = ImageExportFormat.Jpeg,
        DefaultVideoAudioMode = AudioExportMode.RemoveAudio
    };
}
