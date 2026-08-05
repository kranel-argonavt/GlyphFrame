using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Tests;

public sealed class MediaInputStateTests
{
    [Fact]
    public void Empty_IsTheSafeMediaInputDefault()
    {
        Assert.Equal(MediaInputStatus.Empty, MediaInputState.Empty.Status);
        Assert.False(MediaInputState.Empty.HasSelection);
        Assert.Equal(MediaSelectionKind.None, MediaInputState.Empty.SelectedMediaType);
    }

    [Fact]
    public void NewerRequest_PreventsStaleCompletionFromReplacingSelection()
    {
        MediaInputState firstLoading = MediaInputState.Empty.BeginLoading(1, "first.png");
        MediaInputState secondLoading = firstLoading.BeginLoading(2, "second.png");

        MediaInputState afterStaleResult = secondLoading.Complete(1, ImageResult("first.png"));
        MediaInputState completed = afterStaleResult.Complete(2, ImageResult("second.png"));

        Assert.Same(secondLoading, afterStaleResult);
        Assert.Equal("second.png", completed.CurrentSelection?.ImageMetadata?.FileName);
        Assert.Equal(MediaInputStatus.ImageSelected, completed.Status);
    }

    [Fact]
    public void FailedReplacement_RetainsPreviousValidSelection()
    {
        MediaInputState selected = MediaInputState.Empty
            .BeginLoading(1, "valid.png")
            .Complete(1, ImageResult("valid.png"));
        MediaInputState failed = selected
            .BeginLoading(2, "broken.png")
            .Complete(2, MediaInputResult.Failure(MediaInputErrorCategory.InvalidImage, "Unreadable image."));

        Assert.Equal(MediaInputStatus.Failed, failed.Status);
        Assert.Equal("valid.png", failed.CurrentSelection?.ImageMetadata?.FileName);
        Assert.Equal(MediaInputErrorCategory.InvalidImage, failed.LastFailure?.ErrorCategory);
    }

    [Fact]
    public void CancelledReplacement_RestoresPreviousSelectionStatus()
    {
        MediaInputState selected = MediaInputState.Empty
            .BeginLoading(1, "valid.png")
            .Complete(1, ImageResult("valid.png"));
        MediaInputState cancelled = selected
            .BeginLoading(2, "replacement.png")
            .Complete(2, MediaInputResult.Failure(MediaInputErrorCategory.Cancelled, "Cancelled."));

        Assert.Equal(MediaInputStatus.ImageSelected, cancelled.Status);
        Assert.Null(cancelled.LastFailure);
        Assert.Equal("valid.png", cancelled.CurrentSelection?.ImageMetadata?.FileName);
    }

    private static MediaInputResult ImageResult(string fileName) => MediaInputResult.ForImage(
        new ImageMetadata(
            fileName,
            fileName,
            3,
            2,
            3,
            2,
            "PNG",
            100,
            ImageOrientation.Unspecified,
            null));
}
