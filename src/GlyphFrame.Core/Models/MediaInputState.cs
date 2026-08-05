using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

public sealed record MediaInputState
{
    private MediaInputState(
        long activeRequestId,
        MediaInputStatus status,
        MediaInputResult? currentSelection,
        string? pendingFileName,
        MediaInputResult? lastFailure)
    {
        ActiveRequestId = activeRequestId;
        Status = status;
        CurrentSelection = currentSelection;
        PendingFileName = pendingFileName;
        LastFailure = lastFailure;
    }

    public static MediaInputState Empty { get; } = new(0, MediaInputStatus.Empty, null, null, null);

    public long ActiveRequestId { get; }

    public MediaInputStatus Status { get; }

    public MediaInputResult? CurrentSelection { get; }

    public string? PendingFileName { get; }

    public MediaInputResult? LastFailure { get; }

    public bool HasSelection => CurrentSelection?.Success == true;

    public MediaSelectionKind SelectedMediaType => CurrentSelection?.MediaType ?? MediaSelectionKind.None;

    public MediaInputState BeginLoading(long requestId, string? pendingFileName)
    {
        if (requestId <= ActiveRequestId)
        {
            throw new ArgumentOutOfRangeException(nameof(requestId), "Request identifiers must increase.");
        }

        return new(requestId, MediaInputStatus.Loading, CurrentSelection, pendingFileName, null);
    }

    public MediaInputState Complete(long requestId, MediaInputResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (requestId != ActiveRequestId || Status != MediaInputStatus.Loading)
        {
            return this;
        }

        if (result.Success)
        {
            MediaInputStatus status = result.MediaType == MediaSelectionKind.Image
                ? MediaInputStatus.ImageSelected
                : MediaInputStatus.VideoSelected;
            return new(requestId, status, result, null, null);
        }

        if (result.ErrorCategory == MediaInputErrorCategory.Cancelled)
        {
            return RestoreSelectionStatus(requestId);
        }

        return new(requestId, MediaInputStatus.Failed, CurrentSelection, null, result);
    }

    private MediaInputState RestoreSelectionStatus(long requestId)
    {
        MediaInputStatus status = SelectedMediaType switch
        {
            MediaSelectionKind.Image => MediaInputStatus.ImageSelected,
            MediaSelectionKind.Video => MediaInputStatus.VideoSelected,
            _ => MediaInputStatus.Empty
        };

        return new(requestId, status, CurrentSelection, null, null);
    }
}
