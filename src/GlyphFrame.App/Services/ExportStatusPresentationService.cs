using System.Windows;

namespace GlyphFrame.App.Services;

public sealed class ExportStatusPresentationService : IExportStatusPresentationService
{
    public static readonly TimeSpan CompletedStateDuration = TimeSpan.FromMilliseconds(650);

    public bool AnimationsEnabled => SystemParameters.ClientAreaAnimation;

    public Task HoldCompletedStateAsync(CancellationToken cancellationToken) =>
        Task.Delay(CompletedStateDuration, cancellationToken);
}
