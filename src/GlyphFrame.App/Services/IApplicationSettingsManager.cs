using GlyphFrame.Core.Settings;

namespace GlyphFrame.App.Services;

public interface IApplicationSettingsManager
{
    AppSettings Current { get; }

    string? StartupStatusMessage { get; }

    event EventHandler<AppSettings>? SettingsChanged;

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);

    Task<AppSettings> ResetAsync(CancellationToken cancellationToken = default);
}
