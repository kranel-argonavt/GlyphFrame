using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Tests;

public sealed class ImageWorkflowStateTests
{
    [Fact]
    public void ValidImageSelection_EnablesPreviewAndExport()
    {
        ImageWorkflowState state = ImageWorkflowState.Empty.SelectMedia(
            MediaSelectionKind.Image,
            mediaVersion: 1,
            hasOriginal: true);

        Assert.True(state.HasOriginal);
        Assert.True(state.CanPreview);
        Assert.True(state.CanExport);
        Assert.True(state.RequiresRenderForExport);
    }

    [Fact]
    public void VideoSelection_WaitsForAFrameAndNeverEnablesImageExport()
    {
        ImageWorkflowState state = ImageWorkflowState.Empty.SelectMedia(
            MediaSelectionKind.Video,
            mediaVersion: 1,
            hasOriginal: false);

        Assert.False(state.HasOriginal);
        Assert.False(state.CanPreview);
        Assert.False(state.CanExport);
        Assert.False(state.HasSymbolizedPreview);
    }

    [Fact]
    public void DecodedVideoFrame_EnablesFramePreviewButNotImageExport()
    {
        ImageWorkflowState state = ReadyVideo();

        Assert.True(state.HasOriginal);
        Assert.True(state.CanPreview);
        Assert.False(state.CanExport);
        Assert.Equal(MediaSelectionKind.Video, state.CurrentIdentity?.MediaType);
    }

    [Fact]
    public void VideoPositionChange_MarksExistingSymbolizedFrameStale()
    {
        ImageWorkflowState state = WithVideoPreview();
        PreviewIdentity previousIdentity = state.SymbolizedPreviewIdentity!;

        state = state.SourceChanged();

        Assert.True(state.HasSymbolizedPreview);
        Assert.True(state.IsPreviewStale);
        Assert.NotEqual(previousIdentity.SourceVersion, state.CurrentIdentity?.SourceVersion);
    }

    [Fact]
    public void OldVideoFrameResult_CannotReplacePreviewForNewPosition()
    {
        ImageWorkflowState rendering = ReadyVideo().BeginOperation(70);
        PreviewIdentity oldFrameIdentity = rendering.CurrentIdentity!;
        ImageWorkflowState moved = rendering.SourceChanged().BeginOperation(71);

        ImageWorkflowState rejected = moved.CompletePreview(70, oldFrameIdentity);
        ImageWorkflowState accepted = rejected.CompletePreview(71, moved.CurrentIdentity!);

        Assert.Same(moved, rejected);
        Assert.False(rejected.HasSymbolizedPreview);
        Assert.True(accepted.HasSymbolizedPreview);
        Assert.False(accepted.IsPreviewStale);
    }

    [Fact]
    public void RenderSettingChange_MakesVideoFramePreviewIdentityStale()
    {
        ImageWorkflowState state = WithVideoPreview();

        state = state.RenderingSettingChanged(ImageRenderSetting.SymbolPolarity);

        Assert.True(state.IsPreviewStale);
        Assert.NotEqual(
            state.SymbolizedPreviewIdentity?.SettingsVersion,
            state.CurrentIdentity?.SettingsVersion);
    }

    [Fact]
    public void SuccessfulPreview_ProducesCurrentSymbolizedResult()
    {
        ImageWorkflowState state = ReadyImage().BeginOperation(10);

        state = state.CompletePreview(10, state.MediaVersion, state.SettingsVersion);

        Assert.True(state.HasSymbolizedPreview);
        Assert.False(state.IsPreviewStale);
        Assert.False(state.RequiresRenderForExport);
    }

    [Theory]
    [InlineData(ImageRenderSetting.CharacterPreset)]
    [InlineData(ImageRenderSetting.FontFamily)]
    [InlineData(ImageRenderSetting.FontSize)]
    [InlineData(ImageRenderSetting.OutputResolution)]
    [InlineData(ImageRenderSetting.SymbolPolarity)]
    public void RenderingSettingChange_MarksExistingPreviewStale(ImageRenderSetting setting)
    {
        ImageWorkflowState state = WithPreview();

        state = state.RenderingSettingChanged(setting);

        Assert.True(state.HasSymbolizedPreview);
        Assert.True(state.IsPreviewStale);
        Assert.True(state.RequiresRenderForExport);
    }

    [Fact]
    public void OutputImageFormatChange_DoesNotInvalidateSymbolRendering()
    {
        ImageWorkflowState state = WithPreview();
        long version = state.SettingsVersion;

        state = state.OutputFormatChanged();

        Assert.Equal(version, state.SettingsVersion);
        Assert.False(state.IsPreviewStale);
        Assert.False(state.RequiresRenderForExport);
    }

    [Fact]
    public void ExportWithStalePreview_AcceptsOnlyResultRenderedWithCurrentSettings()
    {
        ImageWorkflowState stale = WithPreview().RenderingSettingChanged(ImageRenderSetting.FontSize);
        long oldSettingsVersion = stale.SettingsVersion - 1;
        ImageWorkflowState exporting = stale.BeginOperation(20);

        ImageWorkflowState afterOldResult = exporting.CompletePreview(
            20,
            exporting.MediaVersion,
            oldSettingsVersion);
        ImageWorkflowState afterCurrentResult = afterOldResult.CompletePreview(
            20,
            exporting.MediaVersion,
            exporting.SettingsVersion);

        Assert.Same(exporting, afterOldResult);
        Assert.NotSame(afterOldResult, afterCurrentResult);
        Assert.False(afterCurrentResult.IsPreviewStale);
        Assert.False(afterCurrentResult.RequiresRenderForExport);
        Assert.Equal(afterCurrentResult.SettingsVersion, afterCurrentResult.PreviewSettingsVersion);
    }

    [Fact]
    public void PolarityChange_RejectsOldPolarityResultAndRequiresCurrentRenderForExport()
    {
        ImageWorkflowState stale = WithPreview().RenderingSettingChanged(ImageRenderSetting.SymbolPolarity);
        long oldPolarityVersion = stale.SettingsVersion - 1;
        ImageWorkflowState rendering = stale.BeginOperation(21);

        ImageWorkflowState rejected = rendering.CompletePreview(
            21,
            rendering.MediaVersion,
            oldPolarityVersion);
        ImageWorkflowState accepted = rejected.CompletePreview(
            21,
            rendering.MediaVersion,
            rendering.SettingsVersion);

        Assert.Same(rendering, rejected);
        Assert.True(rejected.RequiresRenderForExport);
        Assert.False(accepted.RequiresRenderForExport);
        Assert.Equal(accepted.SettingsVersion, accepted.PreviewSettingsVersion);
    }

    [Fact]
    public void SelectingNewMedia_InvalidatesPreviousSymbolizedPreview()
    {
        ImageWorkflowState previous = WithPreview();

        ImageWorkflowState replacement = previous.SelectMedia(
            MediaSelectionKind.Image,
            mediaVersion: 2,
            hasOriginal: true);

        Assert.True(replacement.HasOriginal);
        Assert.False(replacement.HasSymbolizedPreview);
        Assert.True(replacement.RequiresRenderForExport);
    }

    [Fact]
    public void LateResultFromObsoleteOperation_CannotReplaceCurrentPreview()
    {
        ImageWorkflowState state = ReadyImage().BeginOperation(30);
        state = state.BeginOperation(31);

        ImageWorkflowState obsoleteCompletion = state.CompletePreview(
            30,
            state.MediaVersion,
            state.SettingsVersion);

        Assert.Same(state, obsoleteCompletion);
        Assert.False(obsoleteCompletion.HasSymbolizedPreview);
    }

    [Fact]
    public void CancelledPreview_PreservesValidOriginalPreview()
    {
        ImageWorkflowState state = ReadyImage().BeginOperation(40);

        state = state.CancelActiveOperation();

        Assert.True(state.HasOriginal);
        Assert.True(state.CanPreview);
        Assert.False(state.HasSymbolizedPreview);
    }

    [Fact]
    public void PreviewFailure_PreservesValidOriginalPreview()
    {
        ImageWorkflowState state = ReadyImage().BeginOperation(50);

        state = state.EndOperation(50);

        Assert.True(state.HasOriginal);
        Assert.True(state.CanPreview);
        Assert.False(state.HasSymbolizedPreview);
    }

    [Fact]
    public void ExportFailure_DoesNotDiscardExistingSymbolizedPreview()
    {
        ImageWorkflowState state = WithPreview().BeginOperation(60);

        state = state.EndOperation(60);

        Assert.True(state.HasOriginal);
        Assert.True(state.HasSymbolizedPreview);
        Assert.False(state.IsPreviewStale);
    }

    private static ImageWorkflowState ReadyImage() => ImageWorkflowState.Empty.SelectMedia(
        MediaSelectionKind.Image,
        mediaVersion: 1,
        hasOriginal: true);

    private static ImageWorkflowState WithPreview()
    {
        ImageWorkflowState state = ReadyImage().BeginOperation(1);
        return state.CompletePreview(1, state.MediaVersion, state.SettingsVersion).EndOperation(1);
    }

    private static ImageWorkflowState ReadyVideo() => ImageWorkflowState.Empty
        .SelectMedia(MediaSelectionKind.Video, mediaVersion: 1, hasOriginal: false)
        .SourceChanged()
        .OriginalAvailable(mediaVersion: 1);

    private static ImageWorkflowState WithVideoPreview()
    {
        ImageWorkflowState state = ReadyVideo().BeginOperation(1);
        return state.CompletePreview(1, state.CurrentIdentity!).EndOperation(1);
    }
}
