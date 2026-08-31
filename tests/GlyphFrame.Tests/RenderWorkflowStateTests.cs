using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Tests;

public sealed class RenderWorkflowStateTests
{
    [Fact]
    public void ImageSelection_StartsMediaReadyWithRenderButNotExportAvailable()
    {
        RenderWorkflowState state = ReadyImage();

        Assert.Equal(RenderWorkflowStatus.MediaReady, state.Status);
        Assert.True(state.CanRenderImage);
        Assert.False(state.CanExportImage);
        Assert.False(state.HasImageRender);
    }

    [Fact]
    public void SuccessfulImageRender_EntersRenderReadyAndEnablesExport()
    {
        RenderWorkflowState rendering = ReadyImage().BeginOperation(1, RenderWorkflowOperation.RenderImage);

        RenderWorkflowState ready = rendering.CompleteImageRender(1, rendering.CurrentIdentity!).EndOperation(1);

        Assert.Equal(RenderWorkflowStatus.RenderReady, ready.Status);
        Assert.True(ready.HasImageRender);
        Assert.True(ready.CanExportImage);
        Assert.False(ready.IsImageRenderStale);
    }

    [Theory]
    [InlineData(ImageRenderSetting.CharacterPreset)]
    [InlineData(ImageRenderSetting.FontFamily)]
    [InlineData(ImageRenderSetting.FontSize)]
    [InlineData(ImageRenderSetting.OutputResolution)]
    [InlineData(ImageRenderSetting.SymbolPolarity)]
    [InlineData(ImageRenderSetting.ApplicationDefaults)]
    public void RenderingSettingChange_RetainsImageResultButMarksItStale(ImageRenderSetting setting)
    {
        RenderWorkflowState state = WithImageRender().RenderingSettingChanged(setting);

        Assert.Equal(RenderWorkflowStatus.RenderStale, state.Status);
        Assert.True(state.HasImageRender);
        Assert.True(state.IsImageRenderStale);
        Assert.False(state.CanExportImage);
        Assert.True(state.CanRenderImage);
    }

    [Fact]
    public void OutputFormatChange_DoesNotInvalidateImageRender()
    {
        RenderWorkflowState state = WithImageRender();
        long settingsVersion = state.SettingsVersion;

        RenderWorkflowState unchanged = state.OutputFormatChanged();

        Assert.Same(state, unchanged);
        Assert.Equal(settingsVersion, unchanged.SettingsVersion);
        Assert.True(unchanged.CanExportImage);
    }

    [Fact]
    public void RenderAgain_ReplacesIdentityWithCurrentSettings()
    {
        RenderWorkflowState stale = WithImageRender().RenderingSettingChanged(ImageRenderSetting.FontSize);
        PreviewIdentity previous = stale.ImageRenderIdentity!;
        RenderWorkflowState rendering = stale.BeginOperation(2, RenderWorkflowOperation.RenderImage);

        RenderWorkflowState ready = rendering.CompleteImageRender(2, rendering.CurrentIdentity!).EndOperation(2);

        Assert.NotEqual(previous, ready.ImageRenderIdentity);
        Assert.False(ready.IsImageRenderStale);
        Assert.True(ready.CanExportImage);
    }

    [Fact]
    public void SupersededRender_RejectsLateCompletion()
    {
        RenderWorkflowState first = ReadyImage().BeginOperation(10, RenderWorkflowOperation.RenderImage);
        PreviewIdentity firstIdentity = first.CurrentIdentity!;
        RenderWorkflowState second = first.BeginOperation(11, RenderWorkflowOperation.RenderImage);

        RenderWorkflowState rejected = second.CompleteImageRender(10, firstIdentity);

        Assert.Same(second, rejected);
        Assert.False(rejected.HasImageRender);
        Assert.Equal(11, rejected.ActiveOperationId);
    }

    [Fact]
    public void CancelledRender_PreservesPreviousResultAndOriginal()
    {
        RenderWorkflowState state = WithImageRender()
            .BeginOperation(20, RenderWorkflowOperation.RenderImage)
            .BeginCancellation()
            .CancelActiveOperation();

        Assert.Equal(RenderWorkflowStatus.Cancelled, state.Status);
        Assert.True(state.HasOriginal);
        Assert.True(state.HasImageRender);
        Assert.True(state.CanExportImage);
        Assert.Equal(RenderWorkflowOperation.RenderImage, state.OutcomeOperation);
    }

    [Fact]
    public void ResultReturnedAfterCancellationRequest_IsRejected()
    {
        RenderWorkflowState rendering = ReadyImage()
            .BeginOperation(21, RenderWorkflowOperation.RenderImage);
        PreviewIdentity identity = rendering.CurrentIdentity!;
        RenderWorkflowState cancelling = rendering.BeginCancellation();

        RenderWorkflowState rejected = cancelling.CompleteImageRender(21, identity);

        Assert.Same(cancelling, rejected);
        Assert.False(rejected.HasImageRender);
        Assert.Equal(RenderWorkflowStatus.Cancelling, rejected.Status);
    }

    [Fact]
    public void FailedExport_PreservesCurrentRenderedResult()
    {
        RenderWorkflowState exporting = WithImageRender()
            .BeginOperation(30, RenderWorkflowOperation.ExportImage);

        RenderWorkflowState failed = exporting.FailActiveOperation(30).EndOperation(30);

        Assert.Equal(RenderWorkflowStatus.Failed, failed.Status);
        Assert.Equal(RenderWorkflowOperation.ExportImage, failed.OutcomeOperation);
        Assert.True(failed.HasImageRender);
        Assert.True(failed.CanExportImage);
    }

    [Fact]
    public void SuccessfulImageExport_IsDistinctFromRenderReadiness()
    {
        RenderWorkflowState exporting = WithImageRender()
            .BeginOperation(31, RenderWorkflowOperation.ExportImage);

        RenderWorkflowState exported = exporting.CompleteExport(31).EndOperation(31);

        Assert.Equal(RenderWorkflowStatus.Exported, exported.Status);
        Assert.Equal(RenderWorkflowOperation.ExportImage, exported.OutcomeOperation);
        Assert.True(exported.HasImageRender);
        Assert.True(exported.CanExportImage);
    }

    [Fact]
    public void FailedInitialRender_RecordsRenderOutcomeWithoutCreatingResult()
    {
        RenderWorkflowState rendering = ReadyImage()
            .BeginOperation(32, RenderWorkflowOperation.RenderImage);

        RenderWorkflowState failed = rendering.FailActiveOperation(32).EndOperation(32);

        Assert.Equal(RenderWorkflowStatus.Failed, failed.Status);
        Assert.Equal(RenderWorkflowOperation.RenderImage, failed.OutcomeOperation);
        Assert.False(failed.HasImageRender);
        Assert.False(failed.CanExportImage);
    }

    [Fact]
    public void CancelledExport_RecordsExportOutcomeAndPreservesRetryableResult()
    {
        RenderWorkflowState exporting = WithImageRender()
            .BeginOperation(33, RenderWorkflowOperation.ExportImage);

        RenderWorkflowState cancelled = exporting.BeginCancellation().CancelActiveOperation();

        Assert.Equal(RenderWorkflowStatus.Cancelled, cancelled.Status);
        Assert.Equal(RenderWorkflowOperation.ExportImage, cancelled.OutcomeOperation);
        Assert.True(cancelled.HasImageRender);
        Assert.True(cancelled.CanExportImage);
    }

    [Fact]
    public void CancelledStaleRerender_PreservesReviewButDoesNotEnableExport()
    {
        RenderWorkflowState stale = WithImageRender()
            .RenderingSettingChanged(ImageRenderSetting.FontSize);
        RenderWorkflowState rendering = stale.BeginOperation(34, RenderWorkflowOperation.RenderImage);

        RenderWorkflowState cancelled = rendering.BeginCancellation().CancelActiveOperation();

        Assert.Equal(RenderWorkflowStatus.Cancelled, cancelled.Status);
        Assert.Equal(RenderWorkflowOperation.RenderImage, cancelled.OutcomeOperation);
        Assert.True(cancelled.HasImageRender);
        Assert.True(cancelled.IsImageRenderStale);
        Assert.False(cancelled.CanExportImage);
    }

    [Fact]
    public void SelectingDifferentMedia_ClearsImageAndVideoResults()
    {
        RenderWorkflowState image = WithImageRender();

        RenderWorkflowState video = image.SelectMedia(MediaSelectionKind.Video, 2, hasOriginal: false);

        Assert.False(video.HasImageRender);
        Assert.False(video.HasVideoFramePreview);
        Assert.False(video.CanExportImage);
        Assert.Equal(RenderWorkflowStatus.MediaReady, video.Status);
    }

    [Fact]
    public void VideoSelection_WaitsForOriginalFrame()
    {
        RenderWorkflowState state = RenderWorkflowState.Empty.SelectMedia(
            MediaSelectionKind.Video,
            1,
            hasOriginal: false);

        Assert.False(state.CanPreviewVideoFrame);
        Assert.False(state.CanExportImage);
    }

    [Fact]
    public void VideoFramePreview_UsesSeparateIdentityAndBecomesStaleAfterSeek()
    {
        RenderWorkflowState ready = ReadyVideo();
        RenderWorkflowState preview = ready
            .BeginOperation(40, RenderWorkflowOperation.PreviewVideoFrame)
            .CompleteVideoFramePreview(40, ready.CurrentIdentity!)
            .EndOperation(40);

        RenderWorkflowState moved = preview.SourceChanged();

        Assert.Null(moved.ImageRenderIdentity);
        Assert.NotNull(moved.VideoFramePreviewIdentity);
        Assert.True(moved.IsVideoFramePreviewStale);
        Assert.False(moved.CanExportImage);
    }

    [Fact]
    public void VideoPreviewCannotCompleteAsImageRender()
    {
        RenderWorkflowState previewing = ReadyVideo()
            .BeginOperation(50, RenderWorkflowOperation.PreviewVideoFrame);

        RenderWorkflowState rejected = previewing.CompleteImageRender(50, previewing.CurrentIdentity!);

        Assert.Same(previewing, rejected);
        Assert.False(rejected.HasImageRender);
    }

    [Fact]
    public void InvalidOperationKind_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ReadyImage().BeginOperation(
            1,
            (RenderWorkflowOperation)999));
    }

    [Fact]
    public void ImageCannotBeginVideoPreviewOrExportBeforeRender()
    {
        RenderWorkflowState state = ReadyImage();

        Assert.Throws<InvalidOperationException>(() => state.BeginOperation(
            1,
            RenderWorkflowOperation.PreviewVideoFrame));
        Assert.Throws<InvalidOperationException>(() => state.BeginOperation(
            2,
            RenderWorkflowOperation.ExportImage));
    }

    private static RenderWorkflowState ReadyImage() => RenderWorkflowState.Empty.SelectMedia(
        MediaSelectionKind.Image,
        1,
        hasOriginal: true);

    private static RenderWorkflowState WithImageRender()
    {
        RenderWorkflowState state = ReadyImage().BeginOperation(1, RenderWorkflowOperation.RenderImage);
        return state.CompleteImageRender(1, state.CurrentIdentity!).EndOperation(1);
    }

    private static RenderWorkflowState ReadyVideo() => RenderWorkflowState.Empty
        .SelectMedia(MediaSelectionKind.Video, 1, hasOriginal: false)
        .SourceChanged()
        .OriginalAvailable(1);
}
