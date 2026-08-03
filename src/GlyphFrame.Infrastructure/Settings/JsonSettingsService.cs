using System.Text.Json;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Settings;

namespace GlyphFrame.Infrastructure.Settings;

public sealed class JsonSettingsService : ISettingsService, IDisposable
{
    private const string TemporaryFileName = "settings.json.tmp";
    private const string InvalidFileName = "settings.invalid.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly IApplicationPaths _paths;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private bool _disposed;

    public JsonSettingsService(IApplicationPaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public async Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        _paths.EnsureDirectories();

        if (!File.Exists(_paths.SettingsFilePath))
        {
            return new SettingsLoadResult(
                AppSettingsCatalog.Defaults,
                SettingsLoadState.DefaultsUsed,
                "Using approved default settings.");
        }

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            PersistedSettings? persisted;
            await using (FileStream stream = new(
                _paths.SettingsFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                persisted = await JsonSerializer.DeserializeAsync<PersistedSettings>(
                    stream,
                    SerializerOptions,
                    cancellationToken).ConfigureAwait(false);
            }

            if (persisted is null)
            {
                return RecoverInvalidFile("The settings file was empty. Defaults were restored.");
            }

            (AppSettings candidate, bool hadInvalidValue) = FromPersistedSettings(persisted);
            SettingsNormalizationResult normalization = AppSettingsNormalizer.Normalize(candidate);
            bool wasNormalized = hadInvalidValue || normalization.WasChanged;

            return new SettingsLoadResult(
                normalization.Settings,
                wasNormalized ? SettingsLoadState.Normalized : SettingsLoadState.Loaded,
                wasNormalized
                    ? "Some saved settings were invalid and were replaced with approved defaults."
                    : null);
        }
        catch (JsonException)
        {
            return RecoverInvalidFile(
                "The saved settings could not be read. The original file was preserved and defaults were restored.");
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        SettingsNormalizationResult normalization = AppSettingsNormalizer.Normalize(settings);

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveCoreAsync(normalization.Settings, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<AppSettings> ResetAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        AppSettings defaults = AppSettingsCatalog.Defaults;

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveCoreAsync(defaults, cancellationToken).ConfigureAwait(false);
            return defaults;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _writeGate.Dispose();
        _disposed = true;
    }

    private async Task SaveCoreAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        _paths.EnsureDirectories();
        string temporaryPath = Path.Combine(_paths.ApplicationDataDirectory, TemporaryFileName);
        PersistedSettings persisted = ToPersistedSettings(settings);

        try
        {
            await using (FileStream stream = new(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    persisted,
                    SerializerOptions,
                    cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, _paths.SettingsFilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private SettingsLoadResult RecoverInvalidFile(string message)
    {
        string? preservedPath = null;

        try
        {
            preservedPath = GetAvailableInvalidFilePath();
            File.Move(_paths.SettingsFilePath, preservedPath);
        }
        catch (IOException)
        {
            preservedPath = null;
        }
        catch (UnauthorizedAccessException)
        {
            preservedPath = null;
        }

        return new SettingsLoadResult(
            AppSettingsCatalog.Defaults,
            SettingsLoadState.RecoveredFromInvalidFile,
            message,
            preservedPath);
    }

    private string GetAvailableInvalidFilePath()
    {
        string firstCandidate = Path.Combine(_paths.ApplicationDataDirectory, InvalidFileName);
        if (!File.Exists(firstCandidate))
        {
            return firstCandidate;
        }

        for (int suffix = 1; suffix < int.MaxValue; suffix++)
        {
            string candidate = Path.Combine(
                _paths.ApplicationDataDirectory,
                $"settings.invalid.{suffix}.json");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("No available path could be found for the invalid settings file.");
    }

    private static PersistedSettings ToPersistedSettings(AppSettings settings) => new()
    {
        AccentPreset = settings.AccentPreset.ToString(),
        DefaultCharacterPreset = settings.DefaultCharacterPreset.ToString(),
        DefaultFontFamily = settings.DefaultFontFamily,
        DefaultFontSize = settings.DefaultFontSize,
        DefaultOutputResolution = settings.DefaultOutputResolution.ToString(),
        DefaultImageExportFormat = settings.DefaultImageExportFormat.ToString(),
        DefaultVideoAudioMode = settings.DefaultVideoAudioMode.ToString()
    };

    private static (AppSettings Settings, bool HadInvalidValue) FromPersistedSettings(
        PersistedSettings persisted)
    {
        AppSettings defaults = AppSettingsCatalog.Defaults;
        bool invalid = false;

        AccentPreset accent = ParseEnum(persisted.AccentPreset, defaults.AccentPreset, ref invalid);
        CharacterPresetKind preset = ParseEnum(
            persisted.DefaultCharacterPreset,
            defaults.DefaultCharacterPreset,
            ref invalid);
        OutputResolution resolution = ParseEnum(
            persisted.DefaultOutputResolution,
            defaults.DefaultOutputResolution,
            ref invalid);
        ImageExportFormat imageFormat = ParseEnum(
            persisted.DefaultImageExportFormat,
            defaults.DefaultImageExportFormat,
            ref invalid);
        AudioExportMode audioMode = ParseEnum(
            persisted.DefaultVideoAudioMode,
            defaults.DefaultVideoAudioMode,
            ref invalid);

        string fontFamily = persisted.DefaultFontFamily ?? defaults.DefaultFontFamily;
        int fontSize = persisted.DefaultFontSize ?? defaults.DefaultFontSize;

        return (new AppSettings
        {
            AccentPreset = accent,
            DefaultCharacterPreset = preset,
            DefaultFontFamily = fontFamily,
            DefaultFontSize = fontSize,
            DefaultOutputResolution = resolution,
            DefaultImageExportFormat = imageFormat,
            DefaultVideoAudioMode = audioMode
        }, invalid);
    }

    private static TEnum ParseEnum<TEnum>(string? text, TEnum defaultValue, ref bool invalid)
        where TEnum : struct, Enum
    {
        if (text is null)
        {
            return defaultValue;
        }

        if (Enum.TryParse(text, ignoreCase: true, out TEnum parsed))
        {
            return parsed;
        }

        invalid = true;
        return defaultValue;
    }

    private sealed class PersistedSettings
    {
        public string? AccentPreset { get; init; }

        public string? DefaultCharacterPreset { get; init; }

        public string? DefaultFontFamily { get; init; }

        public int? DefaultFontSize { get; init; }

        public string? DefaultOutputResolution { get; init; }

        public string? DefaultImageExportFormat { get; init; }

        public string? DefaultVideoAudioMode { get; init; }
    }
}
