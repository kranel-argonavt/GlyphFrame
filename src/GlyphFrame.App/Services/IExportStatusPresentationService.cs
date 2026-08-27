namespace GlyphFrame.App.Services;

public interface IExportStatusPresentationService
{
    bool AnimationsEnabled { get; }

    Task HoldCompletedStateAsync(CancellationToken cancellationToken);
}
