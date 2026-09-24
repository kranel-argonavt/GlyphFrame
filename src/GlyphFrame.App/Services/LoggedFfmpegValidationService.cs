using System.Runtime.CompilerServices;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;

namespace GlyphFrame.App.Services;

/// <summary>
/// Observes actual validation attempts. The Infrastructure validator returns the same result
/// instance for cached reads, so result reference identity prevents startup and Settings from
/// recording the same failure again (including when they observe it concurrently).
/// </summary>
public sealed class LoggedFfmpegValidationService : IFfmpegValidationService
{
    private readonly IFfmpegValidationService _inner;
    private readonly IErrorPresentationService _presentations;
    private readonly IApplicationDiagnosticLogger _logger;
    private readonly ConditionalWeakTable<FfmpegValidationResult, object> _recordedResults = new();
    private readonly object _recordGate = new();

    public LoggedFfmpegValidationService(
        IFfmpegValidationService inner,
        IErrorPresentationService presentations,
        IApplicationDiagnosticLogger logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _presentations = presentations ?? throw new ArgumentNullException(nameof(presentations));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FfmpegValidationResult> ValidateAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        FfmpegValidationResult result;
        try
        {
            result = await _inner.ValidateAsync(forceRefresh, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested
            && _presentations.IsRecoverable(
            ErrorPresentationContext.ToolValidation, exception))
        {
            ErrorPresentation? presentation = _presentations.FromException(
                ErrorPresentationContext.ToolValidation, exception);
            Record(presentation);
            throw;
        }

        FfmpegToolValidationResult? failedTool = result.Ffmpeg.IsAvailable
            ? result.Ffprobe.IsAvailable ? null : result.Ffprobe
            : result.Ffmpeg;
        if (failedTool is not null && !cancellationToken.IsCancellationRequested)
        {
            lock (_recordGate)
            {
                if (!_recordedResults.TryGetValue(result, out _))
                {
                    _recordedResults.Add(result, new object());
                    Record(_presentations.From(failedTool));
                }
            }
        }

        return result;
    }

    private void Record(ErrorPresentation? presentation)
    {
        if (presentation is not null)
        {
            _logger.Record(presentation.Code, presentation.Context);
        }
    }
}
