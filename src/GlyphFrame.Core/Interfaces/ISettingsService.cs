using GlyphFrame.Core.Settings;

namespace GlyphFrame.Core.Interfaces;

public interface ISettingsService
{
    Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);

    Task<AppSettings> ResetAsync(CancellationToken cancellationToken = default);
}
