using System.IO;
using System.Security;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Settings;

namespace GlyphFrame.App.Services;

public sealed class ApplicationSettingsManager : IApplicationSettingsManager
{
    private readonly ISettingsService _settingsService;
    private readonly IAccentThemeService _themeService;

    public ApplicationSettingsManager(
        ISettingsService settingsService,
        IAccentThemeService themeService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
    }

    public AppSettings Current { get; private set; } = AppSettingsCatalog.Defaults;

    public string? StartupStatusMessage { get; private set; }

    public event EventHandler<AppSettings>? SettingsChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            SettingsLoadResult result = await _settingsService.LoadAsync(cancellationToken);
            Current = result.Settings;
            StartupStatusMessage = result.UserMessage;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            Current = AppSettingsCatalog.Defaults;
            StartupStatusMessage = "Settings could not be loaded. GlyphFrame started with approved defaults.";
        }

        _themeService.Apply(Current.AccentPreset);
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        AppSettings normalized = AppSettingsNormalizer.Normalize(settings).Settings;
        await _settingsService.SaveAsync(normalized, cancellationToken);
        SetCurrent(normalized);
    }

    public async Task<AppSettings> ResetAsync(CancellationToken cancellationToken = default)
    {
        AppSettings defaults = await _settingsService.ResetAsync(cancellationToken);
        SetCurrent(defaults);
        return defaults;
    }

    private void SetCurrent(AppSettings settings)
    {
        Current = settings;
        StartupStatusMessage = null;
        _themeService.Apply(settings.AccentPreset);
        SettingsChanged?.Invoke(this, settings);
    }
}
