using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Tests;

public sealed class UiPresentationStateTests
{
    [Fact]
    public void Empty_IsTheSafeDefaultState()
    {
        UiPresentationState state = UiPresentationState.Empty;

        Assert.True(state.IsNoMediaSelected);
        Assert.False(state.IsMediaSelected);
        Assert.False(state.IsImageSelected);
        Assert.False(state.IsVideoSelected);
        Assert.False(state.IsExportActive);
        Assert.False(state.CanUseMediaActions);
        Assert.Equal("Media File", state.InputLabel);
    }

    [Theory]
    [InlineData(MediaSelectionKind.Image, "Render Image", "", "Export Image")]
    [InlineData(MediaSelectionKind.Video, "", "Preview Frame", "Export Video")]
    public void MediaState_ProvidesModeSpecificRules(
        MediaSelectionKind mediaType,
        string imageRenderLabel,
        string videoPreviewLabel,
        string exportLabel)
    {
        UiPresentationState state = UiPresentationState.ForMedia(mediaType);

        Assert.True(state.IsMediaSelected);
        Assert.True(state.CanUseMediaActions);
        Assert.Equal(mediaType == MediaSelectionKind.Image, state.IsImageSelected);
        Assert.Equal(mediaType == MediaSelectionKind.Video, state.IsVideoSelected);
        Assert.Equal(imageRenderLabel, state.ImageRenderActionLabel);
        Assert.Equal(videoPreviewLabel, state.VideoFramePreviewActionLabel);
        Assert.Equal(exportLabel, state.ExportActionLabel);
    }

    [Fact]
    public void Exporting_DisablesConflictingMediaActionsUntilExportEnds()
    {
        UiPresentationState mediaState = UiPresentationState.ForMedia(MediaSelectionKind.Video);

        UiPresentationState exportingState = mediaState.BeginExport();
        UiPresentationState completedState = exportingState.EndExport();

        Assert.True(exportingState.IsExportActive);
        Assert.False(exportingState.CanUseMediaActions);
        Assert.False(completedState.IsExportActive);
        Assert.True(completedState.CanUseMediaActions);
    }

    [Fact]
    public void EmptyState_CannotBeginExport()
    {
        Assert.Same(UiPresentationState.Empty, UiPresentationState.Empty.BeginExport());
    }
}
