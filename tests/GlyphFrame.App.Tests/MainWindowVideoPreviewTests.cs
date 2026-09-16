using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using GlyphFrame.App.Services;
using GlyphFrame.App.ViewModels;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Core.Presets;
using GlyphFrame.Core.Settings;
using ImageMetadata = GlyphFrame.Core.Models.ImageMetadata;

namespace GlyphFrame.App.Tests;

public sealed class MainWindowVideoPreviewTests
{
    [Fact]
    public void Constructor_RejectsNullVideoFrameReader()
    {
        TestHarness harness = new();

        Assert.Throws<ArgumentNullException>(() => harness.CreateViewModel(videoFrameReader: null, allowNull: true));
    }

    [Fact]
    public void Constructor_RejectsNullExportStatusPresentationService()
    {
        TestHarness harness = new();

        Assert.Throws<ArgumentNullException>(() => harness.CreateViewModel(
            exportStatusPresentationService: null,
            allowNullExportStatusPresentationService: true));
    }

    [Fact]
    public void Constructor_RejectsNullFolderLauncher()
    {
        TestHarness harness = new();

        Assert.Throws<ArgumentNullException>(() => harness.CreateViewModel(
            folderLauncher: null,
            allowNullFolderLauncher: true));
    }

    [Fact]
    public void Constructor_RejectsNullManagedVideoArtifactSession()
    {
        TestHarness harness = new();

        Assert.Throws<ArgumentNullException>(() => harness.CreateViewModel(
            managedVideoArtifactSession: null,
            allowNullManagedVideoArtifactSession: true));
    }

    [Fact]
    public async Task SelectingVideo_LoadsMiddleFrameAndEnablesVideoOnlyPositionControls()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("middle.mp4");
        TestHarness harness = new();
        DecodedImage middleFrame = SolidFrame(12, 34, 56);
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.VideoFrames.Handler = (_, position, metadata, _) =>
            Task.FromResult(FrameResult(middleFrame, position, metadata));
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        Assert.True(viewModel.IsVideoSelected);
        Assert.True(viewModel.HasVideoFrame);
        Assert.True(viewModel.ShowImagePreview);
        Assert.True(viewModel.IsVideoSeekEnabled);
        Assert.Equal(VideoPreviewDefaults.MiddlePosition, viewModel.TimelinePosition, 3);
        Assert.Equal(middleFrame, harness.ImageSources.LastDecodedImage);
        Assert.False(viewModel.RenderImageCommand.CanExecute(null));
        Assert.True(viewModel.PreviewFrameCommand.CanExecute(null));
        Assert.False(viewModel.ExportImageCommand.CanExecute(null));
        Assert.False(viewModel.ExportVideoCommand.CanExecute(null));
        Assert.Equal("Preview Frame", viewModel.PreviewFrameActionLabel);
        Assert.Contains("0:05", viewModel.TimelineText, StringComparison.Ordinal);
        Assert.Equal(1, harness.ManagedArtifacts.ClearCalls);
    }

    [Fact]
    public async Task FailedMediaReplacement_DoesNotClearManagedArtifactSession()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("current.mp4");
        string invalidPath = files.Create("unsupported.txt");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(
            path == videoPath
                ? VideoResult(path)
                : MediaInputResult.Failure(
                    MediaInputErrorCategory.UnsupportedExtension,
                    "Choose a supported media file."));
        string artifactPath = files.PathFor("current-artifact.mp4");
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(artifactPath, job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);
        int closeCalls = harness.RenderedVideoPlayer.CloseCalls;
        await viewModel.DropMediaCommand.ExecuteAsync([invalidPath]);

        Assert.Equal(1, harness.ManagedArtifacts.ClearCalls);
        Assert.Equal(videoPath, viewModel.VideoMetadata?.FilePath);
        Assert.Equal(artifactPath, harness.RenderedVideoPlayer.SourcePath);
        Assert.Equal(closeCalls, harness.RenderedVideoPlayer.CloseCalls);
    }

    [Fact]
    public async Task VideoExport_UsesAcceptedArtifactAndRestoresUiAfterSuccess()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("export.mp4");
        string outputPath = files.PathFor("символи output.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = outputPath;
        harness.VideoPublications.Handler = (job, _, _) => Task.FromResult(VideoExportResult.Completed(
            job.Destination.FilePath,
            12,
            new PixelDimensions(64, 48),
            10));
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.PathFor("review.mp4"), job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        viewModel.SelectedAudioMode = AudioExportMode.RemoveAudio;
        await viewModel.RenderVideoCommand.ExecuteAsync(null);

        await viewModel.ExportVideoCommand.ExecuteAsync(null);

        Assert.NotNull(harness.VideoPublications.LastJob);
        Assert.Equal(videoPath, harness.VideoPublications.LastJob!.OriginalInputPath);
        Assert.Equal(outputPath, harness.VideoPublications.LastJob.Destination.FilePath);
        Assert.Equal(viewModel.SelectedPreset.Kind, harness.VideoPublications.LastJob.Artifact.Identity.CharacterPreset);
        Assert.Equal(AudioExportMode.RemoveAudio, harness.VideoPublications.LastJob.Artifact.AudioMode);
        Assert.Equal(100, viewModel.ExportProgress);
        Assert.False(viewModel.IsExportActive);
        Assert.Contains("Reviewed video saved", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Equal(outputPath, viewModel.VideoExportDestinationText);
    }

    [Theory]
    [InlineData(AudioMuxOutcome.StreamCopied, "Original", "Original audio preserved")]
    [InlineData(AudioMuxOutcome.TranscodedToAac, "AAC", "Audio transcoded to AAC")]
    [InlineData(AudioMuxOutcome.Removed, "Removed", "Audio removed from rendered video")]
    [InlineData(AudioMuxOutcome.NoSourceAudio, "No source", "Source video contains no audio")]
    public async Task VideoResult_MapsAcceptedArtifactAudioOutcome(
        AudioMuxOutcome outcome,
        string expectedValue,
        string expectedDescription)
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("audio-outcome.mp4");
        string outputPath = files.PathFor("audio-outcome-output.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = outputPath;
        harness.VideoPublications.Handler = (job, _, _) => Task.FromResult(VideoExportResult.Completed(
            job.Destination.FilePath,
            12,
            new PixelDimensions(64, 48),
            10,
            outcome));
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.PathFor("review.mp4"), job.Identity) with { AudioOutcome = outcome }));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);

        await viewModel.ExportVideoCommand.ExecuteAsync(null);

        Assert.Equal(expectedValue, viewModel.VideoAudioOutcomeText);
        Assert.Equal(expectedDescription, viewModel.VideoAudioOutcomeDescription);
        Assert.Equal("Ready for review and another export.", viewModel.VideoResultSummary);
        Assert.False(viewModel.IsExportActive);
        Assert.True(viewModel.ExportVideoCommand.CanExecute(null));
    }

    [Fact]
    public async Task VideoExport_FailureRestoresUiAndShowsFriendlyMessage()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("failure.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = files.PathFor("failure-output.mp4");
        harness.VideoPublications.Handler = (_, _, _) => Task.FromResult(VideoExportResult.Failure(
            VideoExportErrorCategory.EncoderFailed,
            "GlyphFrame could not finish encoding the MP4 video.",
            "raw encoder detail"));
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.PathFor("review.mp4"), job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);

        await viewModel.ExportVideoCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsExportActive);
        Assert.True(viewModel.ExportVideoCommand.CanExecute(null));
        Assert.Contains("could not finish", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw encoder detail", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VideoExport_PreventsConcurrentActionsUntilCompletion()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("concurrent.mp4");
        string outputPath = files.PathFor("concurrent-output.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = outputPath;
        TaskCompletionSource<bool> started = NewCompletion<bool>();
        TaskCompletionSource<VideoExportResult> completion = NewCompletion<VideoExportResult>();
        harness.VideoPublications.Handler = (_, _, _) =>
        {
            started.TrySetResult(true);
            return completion.Task;
        };
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.PathFor("review.mp4"), job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);

        Task export = viewModel.ExportVideoCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.True(viewModel.IsExportActive);
        Assert.False(viewModel.ExportVideoCommand.CanExecute(null));
        Assert.False(viewModel.BrowseMediaCommand.CanExecute(null));
        Assert.False(viewModel.AreSettingsEnabled);

        completion.SetResult(VideoExportResult.Completed(
            outputPath,
            12,
            new PixelDimensions(64, 48),
            10));
        await export;
        Assert.False(viewModel.IsExportActive);
        Assert.True(viewModel.ExportVideoCommand.CanExecute(null));
    }

    [Fact]
    public async Task VideoExport_CapturesAudioModeBeforeAsynchronousExportBegins()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("capture-audio.mp4");
        string outputPath = files.PathFor("capture-audio-output.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = outputPath;
        TaskCompletionSource<object?> started = NewCompletion<object?>();
        TaskCompletionSource<VideoExportResult> completion = NewCompletion<VideoExportResult>();
        harness.VideoPublications.Handler = (_, _, _) =>
        {
            started.TrySetResult(null);
            return completion.Task;
        };
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.PathFor("review.mp4"), job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);
        viewModel.SelectedAudioMode = AudioExportMode.KeepAudio;

        Task export = viewModel.ExportVideoCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        viewModel.SelectedAudioMode = AudioExportMode.RemoveAudio;

        Assert.Equal(AudioExportMode.KeepAudio, harness.VideoPublications.LastJob!.Artifact.AudioMode);
        Assert.False(viewModel.AreSettingsEnabled);
        completion.SetResult(VideoExportResult.Completed(
            outputPath,
            12,
            new PixelDimensions(64, 48),
            10,
            AudioMuxOutcome.StreamCopied));
        await export;
        Assert.False(viewModel.IsExportActive);
    }

    [Fact]
    public async Task VideoExport_CopyProgressShowsElapsedWithoutInventingFramesOrEta()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("live-progress.mp4");
        string outputPath = files.PathFor("live-progress-output.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = outputPath;
        TaskCompletionSource progressReported = NewCompletion();
        TaskCompletionSource<VideoExportResult> completion = NewCompletion<VideoExportResult>();
        harness.VideoPublications.Handler = (job, _, progress) =>
        {
            progress!.Report(ExportProgress(
                VideoExportStage.CopyingArtifact,
                0,
                null,
                49.5,
                TimeSpan.FromSeconds(12),
                null,
                "Copying reviewed video..."));
            progressReported.TrySetResult();
            return completion.Task;
        };
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.PathFor("review.mp4"), job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);

        Task export = viewModel.ExportVideoCommand.ExecuteAsync(null);
        await progressReported.Task;

        Assert.False(viewModel.IsExportProgressIndeterminate);
        Assert.True(viewModel.AreExportAnimationsEnabled);
        Assert.True(viewModel.IsExportActive);
        Assert.Equal(VideoExportStage.CopyingArtifact, viewModel.ExportStage);
        Assert.Equal(49.5, viewModel.ExportProgress);
        Assert.Equal("50%", viewModel.ExportPercentageText);
        Assert.Empty(viewModel.ExportFrameText);
        Assert.Equal("Elapsed 00:00:12", viewModel.ElapsedText);
        Assert.Empty(viewModel.RemainingText);
        completion.SetResult(VideoExportResult.Completed(
            outputPath,
            10,
            new PixelDimensions(64, 48),
            10));
        await export;

        Assert.Equal(100, viewModel.ExportProgress);
        Assert.False(viewModel.IsExportProgressIndeterminate);
    }

    [Fact]
    public async Task VideoExport_SuccessShowsPresentationOnlyCompletionAfterResourcesAreReleased()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("completion-state.mp4");
        string outputPath = files.PathFor("completion-state-output.mp4");
        TestHarness harness = new();
        TaskCompletionSource completedStateReleased = NewCompletion();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = outputPath;
        harness.ExportStatusPresentation.Handler = token => completedStateReleased.Task.WaitAsync(token);
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.PathFor("review.mp4"), job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);

        await viewModel.ExportVideoCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsExportActive);
        Assert.True(viewModel.IsExportCompletionVisible);
        Assert.True(viewModel.IsExportStatusVisible);
        Assert.Equal(VideoExportStage.Completed, viewModel.ExportStage);
        Assert.Equal(100, viewModel.ExportProgress);
        Assert.True(viewModel.AreSettingsEnabled);
        completedStateReleased.SetResult();
        await WaitUntilAsync(viewModel, () => !viewModel.IsExportCompletionVisible);
        Assert.False(viewModel.IsExportStatusVisible);
    }

    [Fact]
    public async Task VideoExport_FailureDoesNotEnterCompletedPresentation()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("failed-completion-state.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = files.PathFor("failed-completion-state-output.mp4");
        harness.VideoPublications.Handler = (_, _, _) => Task.FromResult(VideoExportResult.Failure(
            VideoExportErrorCategory.EncoderFailed,
            "GlyphFrame could not encode the video."));
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.PathFor("review.mp4"), job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);

        await viewModel.ExportVideoCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsExportCompletionVisible);
        Assert.False(viewModel.IsExportStatusVisible);
        Assert.Equal(1, harness.ExportStatusPresentation.Calls); // The preceding render, not the failed publication.
    }

    [Fact]
    public async Task VideoExport_NewOperationCancelsCompletionAndResetsVisualProgress()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("completion-reset.mp4");
        string outputPath = files.PathFor("completion-reset-output.mp4");
        TestHarness harness = new();
        TaskCompletionSource firstPresentation = NewCompletion();
        TaskCompletionSource secondExportStarted = NewCompletion();
        TaskCompletionSource<VideoExportResult> secondExportFinished = NewCompletion<VideoExportResult>();
        int exportCall = 0;
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = outputPath;
        harness.ExportStatusPresentation.Handler = token => firstPresentation.Task.WaitAsync(token);
        harness.VideoPublications.Handler = (job, _, _) => Interlocked.Increment(ref exportCall) == 1
            ? Task.FromResult(VideoExportResult.Completed(job.Destination.FilePath, 3, new PixelDimensions(64, 48), 10))
            : WaitForSecondExportAsync();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.PathFor("review.mp4"), job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);
        await viewModel.ExportVideoCommand.ExecuteAsync(null);
        long completedGeneration = viewModel.ExportPresentationGeneration;

        Task secondExport = viewModel.ExportVideoCommand.ExecuteAsync(null);
        await secondExportStarted.Task;

        Assert.False(viewModel.IsExportCompletionVisible);
        Assert.True(viewModel.IsExportActive);
        Assert.Equal(VideoExportStage.Preparing, viewModel.ExportStage);
        Assert.Equal(0, viewModel.ExportProgress);
        Assert.True(viewModel.ExportPresentationGeneration > completedGeneration);
        Assert.True(harness.ExportStatusPresentation.LastCancellationToken.IsCancellationRequested);

        secondExportFinished.SetResult(VideoExportResult.Failure(
            VideoExportErrorCategory.EncoderFailed,
            "Controlled failure."));
        await secondExport;

        async Task<VideoExportResult> WaitForSecondExportAsync()
        {
            secondExportStarted.SetResult();
            return await secondExportFinished.Task;
        }
    }

    [Fact]
    public async Task VideoExport_StaleCompletionCallbackCannotHideNewerOperation()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("stale-completion.mp4");
        string outputPath = files.PathFor("stale-completion-output.mp4");
        TestHarness harness = new();
        TaskCompletionSource stalePresentation = NewCompletion();
        TaskCompletionSource secondExportStarted = NewCompletion();
        TaskCompletionSource<VideoExportResult> secondExportFinished = NewCompletion<VideoExportResult>();
        int exportCall = 0;
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = outputPath;
        harness.ExportStatusPresentation.Handler = _ => stalePresentation.Task;
        harness.VideoPublications.Handler = (job, _, _) => Interlocked.Increment(ref exportCall) == 1
            ? Task.FromResult(VideoExportResult.Completed(job.Destination.FilePath, 3, new PixelDimensions(64, 48), 10))
            : WaitForSecondExportAsync();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.PathFor("review.mp4"), job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);
        await viewModel.ExportVideoCommand.ExecuteAsync(null);

        Task secondExport = viewModel.ExportVideoCommand.ExecuteAsync(null);
        await secondExportStarted.Task;
        stalePresentation.SetResult();
        await Task.Yield();

        Assert.True(viewModel.IsExportActive);
        Assert.True(viewModel.IsExportStatusVisible);
        Assert.False(viewModel.IsExportCompletionVisible);

        secondExportFinished.SetResult(VideoExportResult.Failure(
            VideoExportErrorCategory.EncoderFailed,
            "Controlled failure."));
        await secondExport;

        async Task<VideoExportResult> WaitForSecondExportAsync()
        {
            secondExportStarted.SetResult();
            return await secondExportFinished.Task;
        }
    }

    [Fact]
    public async Task VideoExport_DisposeCancelsPendingCompletionPresentation()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("dispose-completion.mp4");
        TestHarness harness = new();
        TaskCompletionSource presentationStarted = NewCompletion();
        TaskCompletionSource never = NewCompletion();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = files.PathFor("dispose-completion-output.mp4");
        harness.ExportStatusPresentation.Handler = async token =>
        {
            presentationStarted.TrySetResult();
            await never.Task.WaitAsync(token);
        };
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.PathFor("review.mp4"), job.Identity)));
        MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);
        await viewModel.ExportVideoCommand.ExecuteAsync(null);
        await presentationStarted.Task;

        viewModel.Dispose();

        Assert.True(harness.ExportStatusPresentation.LastCancellationToken.CanBeCanceled);
        Assert.True(harness.ExportStatusPresentation.LastCancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task VideoExport_ReducedMotionKeepsCompletionInformationWithoutDecorativeAnimation()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("reduced-motion.mp4");
        TestHarness harness = new();
        TaskCompletionSource completedStateReleased = NewCompletion();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = files.PathFor("reduced-motion-output.mp4");
        harness.ExportStatusPresentation.AnimationsEnabled = false;
        harness.ExportStatusPresentation.Handler = token => completedStateReleased.Task.WaitAsync(token);
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.PathFor("review.mp4"), job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);

        await viewModel.ExportVideoCommand.ExecuteAsync(null);

        Assert.False(viewModel.AreExportAnimationsEnabled);
        Assert.True(viewModel.IsExportCompletionVisible);
        Assert.Equal("Reviewed video exported.", viewModel.ExportProgressText);
        completedStateReleased.SetResult();
    }

    [Fact]
    public async Task VideoExport_FinalizationShowsTruthfulPublicationStage()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("mux-progress.mp4");
        string outputPath = files.PathFor("mux-progress-output.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = outputPath;
        TaskCompletionSource stageReported = NewCompletion();
        TaskCompletionSource<VideoExportResult> completion = NewCompletion<VideoExportResult>();
        harness.VideoPublications.Handler = (_, _, progress) =>
        {
            progress!.Report(ExportProgress(
                VideoExportStage.Finalizing,
                0,
                null,
                null,
                TimeSpan.FromSeconds(20),
                null,
                "Publishing reviewed video..."));
            stageReported.TrySetResult();
            return completion.Task;
        };
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.PathFor("review.mp4"), job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);

        Task export = viewModel.ExportVideoCommand.ExecuteAsync(null);
        await stageReported.Task;

        Assert.True(viewModel.IsExportProgressIndeterminate);
        Assert.Equal("...", viewModel.ExportPercentageText);
        Assert.Equal("Publishing reviewed video...", viewModel.ExportProgressText);
        Assert.Empty(viewModel.ExportFrameText);
        Assert.Equal("Elapsed 00:00:20", viewModel.ElapsedText);
        Assert.Empty(viewModel.RemainingText);
        completion.SetResult(VideoExportResult.Completed(
            outputPath,
            10,
            new PixelDimensions(64, 48),
            10,
            AudioMuxOutcome.StreamCopied));
        await export;
    }

    [Fact]
    public async Task VideoExport_CancelImmediatelyEntersCleanupStateAndDisablesRepeatedCancellation()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("cancel-progress.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = files.PathFor("cancel-progress-output.mp4");
        TaskCompletionSource started = NewCompletion();
        TaskCompletionSource cancellationObserved = NewCompletion();
        TaskCompletionSource cleanupFinished = NewCompletion();
        harness.VideoPublications.Handler = async (_, token, _) =>
        {
            started.TrySetResult();
            try
            {
                TaskCompletionSource never = NewCompletion();
                await never.Task.WaitAsync(token);
                throw new InvalidOperationException("The controlled export should be cancelled.");
            }
            catch (OperationCanceledException)
            {
                cancellationObserved.TrySetResult();
                await cleanupFinished.Task;
                return VideoExportResult.Cancelled();
            }
        };
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.PathFor("review.mp4"), job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);

        Task export = viewModel.ExportVideoCommand.ExecuteAsync(null);
        await started.Task;
        viewModel.CancelExportCommand.Execute(null);
        await cancellationObserved.Task;

        Assert.True(viewModel.IsExportActive);
        Assert.True(viewModel.IsExportCancellationRequested);
        Assert.False(viewModel.CancelExportCommand.CanExecute(null));
        Assert.True(viewModel.IsExportProgressIndeterminate);
        Assert.Equal(VideoExportStage.Cancelling, viewModel.ExportStage);
        Assert.False(viewModel.IsExportCompletionVisible);
        Assert.Contains("Cancelling", viewModel.ExportProgressText, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.BrowseMediaCommand.CanExecute(null));
        Assert.False(viewModel.AreSettingsEnabled);

        cleanupFinished.SetResult();
        await export;

        Assert.False(viewModel.IsExportActive);
        Assert.False(viewModel.IsExportCancellationRequested);
        Assert.False(viewModel.IsExportCompletionVisible);
        Assert.True(viewModel.BrowseMediaCommand.CanExecute(null));
        Assert.True(viewModel.AreSettingsEnabled);
        Assert.Contains("cancelled", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VideoExport_LateProgressAfterCompletionCannotCorruptStableUiState()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("stale-progress.mp4");
        string outputPath = files.PathFor("stale-progress-output.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = outputPath;
        IProgress<VideoExportProgress>? capturedProgress = null;
        harness.VideoPublications.Handler = (job, _, progress) =>
        {
            capturedProgress = progress;
            progress!.Report(ExportProgress(
                VideoExportStage.Completed,
                10,
                10,
                100,
                TimeSpan.FromSeconds(3),
                null,
                "Export completed."));
            return Task.FromResult(VideoExportResult.Completed(
                job.Destination.FilePath,
                10,
                new PixelDimensions(64, 48),
                10));
        };
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.PathFor("review.mp4"), job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);
        await viewModel.ExportVideoCommand.ExecuteAsync(null);
        string completedStatus = viewModel.StatusMessage;

        capturedProgress!.Report(ExportProgress(
            VideoExportStage.ProcessingFrames,
            1,
            10,
            9.9,
            TimeSpan.FromSeconds(99),
            TimeSpan.FromSeconds(999),
            "obsolete progress"));

        Assert.Equal(100, viewModel.ExportProgress);
        Assert.Equal(completedStatus, viewModel.StatusMessage);
        Assert.False(viewModel.IsExportActive);
    }

    [Fact]
    public async Task VideoExport_ProgressFromPreviousOperationCannotReplaceCurrentOperation()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("replaced-progress.mp4");
        string outputPath = files.PathFor("replaced-progress-output.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = outputPath;
        IProgress<VideoExportProgress>? oldProgress = null;
        TaskCompletionSource secondStarted = NewCompletion();
        TaskCompletionSource<VideoExportResult> secondCompletion = NewCompletion<VideoExportResult>();
        int call = 0;
        harness.VideoPublications.Handler = (job, _, progress) =>
        {
            if (Interlocked.Increment(ref call) == 1)
            {
                oldProgress = progress;
                return Task.FromResult(VideoExportResult.Completed(
                    job.Destination.FilePath,
                    10,
                    new PixelDimensions(64, 48),
                    10));
            }

            progress!.Report(ExportProgress(
                VideoExportStage.ProcessingFrames,
                2,
                10,
                19.8,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(8),
                "current progress"));
            secondStarted.TrySetResult();
            return secondCompletion.Task;
        };
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.PathFor("review.mp4"), job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);
        await viewModel.ExportVideoCommand.ExecuteAsync(null);

        Task secondExport = viewModel.ExportVideoCommand.ExecuteAsync(null);
        await secondStarted.Task;
        oldProgress!.Report(ExportProgress(
            VideoExportStage.Completed,
            10,
            10,
            100,
            TimeSpan.FromSeconds(10),
            null,
            "obsolete completed progress"));

        Assert.Equal(19.8, viewModel.ExportProgress);
        Assert.Equal("current progress", viewModel.ExportProgressText);
        secondCompletion.SetResult(VideoExportResult.Completed(
            outputPath,
            10,
            new PixelDimensions(64, 48),
            10));
        await secondExport;
    }

    [Fact]
    public async Task VideoExport_DeclinedOverwritePreservesFileAndSkipsService()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("source-overwrite.mp4");
        string outputPath = files.Create("existing-output.mp4");
        byte[] original = await File.ReadAllBytesAsync(outputPath);
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = outputPath;
        harness.UserDialogs.ConfirmResult = false;
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.PathFor("review.mp4"), job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);

        await viewModel.ExportVideoCommand.ExecuteAsync(null);

        Assert.Equal(0, harness.VideoPublications.Calls);
        Assert.Equal(original, await File.ReadAllBytesAsync(outputPath));
        Assert.Equal(RenderWorkflowStatus.RenderReady, viewModel.WorkflowStatus);
    }

    [Fact]
    public async Task MiddleFrameFailure_PreservesVideoMetadataAndShowsFriendlyRetryState()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("broken-preview.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.VideoFrames.Handler = (_, position, _, _) => Task.FromResult(
            VideoFrameReadResult.Failure(
                VideoFrameReadStatus.Unavailable,
                VideoFrameReadErrorCategory.DecodeFailed,
                position,
                "GlyphFrame could not decode a frame at this position.",
                "decoder detail"));
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        Assert.True(viewModel.IsVideoSelected);
        Assert.NotNull(viewModel.VideoMetadata);
        Assert.False(viewModel.HasVideoFrame);
        Assert.True(viewModel.HasVideoFrameError);
        Assert.True(viewModel.ShowVideoFrameError);
        Assert.Contains("could not decode", viewModel.VideoFrameStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("decoder detail", viewModel.VideoFrameStatusMessage, StringComparison.Ordinal);
        Assert.Equal("decoder detail", viewModel.VideoFrameTechnicalDetail);
    }

    [Fact]
    public async Task RapidSliderChanges_CoalesceAndOnlyLatestPositionUpdatesOriginalFrame()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("seeking.mp4");
        TestHarness harness = new();
        DecodedImage initial = SolidFrame(255, 0, 0);
        DecodedImage final = SolidFrame(0, 0, 255);
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.VideoFrames.Handler = (_, position, metadata, _) =>
            Task.FromResult(FrameResult(initial, position, metadata));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        TaskCompletionSource<double> seekStarted = NewCompletion<double>();
        int seekCalls = 0;
        harness.VideoFrames.Handler = (_, position, metadata, _) =>
        {
            Interlocked.Increment(ref seekCalls);
            seekStarted.TrySetResult(position);
            return Task.FromResult(FrameResult(final, position, metadata));
        };

        viewModel.TimelinePosition = 0.2d;
        viewModel.TimelinePosition = 0.4d;
        viewModel.TimelinePosition = 0.8d;

        double requestedPosition = await seekStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await WaitUntilAsync(viewModel, () => !viewModel.IsVideoFrameLoading);

        Assert.Equal(0.8d, requestedPosition, 3);
        Assert.Equal(1, seekCalls);
        Assert.Equal(0.8d, viewModel.TimelinePosition, 3);
        Assert.Equal(final, harness.ImageSources.LastDecodedImage);
        Assert.False(viewModel.HasVideoFrameError);
    }

    [Fact]
    public async Task ObsoleteCancelledFrameRequest_CannotRestoreOlderPositionOrStatus()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("superseded-cancelled-frame.mp4");
        TestHarness harness = ReadyVideoHarness();
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        TaskCompletionSource firstStarted = NewCompletion();
        TaskCompletionSource releaseFirst = NewCompletion();
        DecodedImage latestFrame = SolidFrame(20, 40, 220);
        harness.VideoFrames.Handler = async (_, position, metadata, _) =>
        {
            if (Math.Abs(position - 0.2d) < 0.001d)
            {
                firstStarted.TrySetResult();
                await releaseFirst.Task;
                return VideoFrameReadResult.Cancelled(position);
            }

            return FrameResult(latestFrame, position, metadata);
        };

        viewModel.TimelinePosition = 0.2d;
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        viewModel.TimelinePosition = 0.8d;
        releaseFirst.SetResult();
        await WaitUntilAsync(
            viewModel,
            () => !viewModel.IsVideoFrameLoading
                && ReferenceEquals(harness.ImageSources.LastDecodedImage, latestFrame));

        Assert.Equal(0.8d, viewModel.TimelinePosition, 3);
        Assert.Same(latestFrame, harness.ImageSources.LastDecodedImage);
        Assert.Equal("Video frame ready at 0:08.", viewModel.StatusMessage);
        Assert.False(viewModel.HasVideoFrameError);
    }

    [Fact]
    public async Task LateFrameFromPreviousVideo_CannotReplaceCurrentVideoFrame()
    {
        using TestMediaFiles files = new();
        string firstPath = files.Create("first.mp4");
        string secondPath = files.Create("second.mp4");
        TestHarness harness = new();
        DecodedImage firstFrame = SolidFrame(255, 0, 0);
        DecodedImage secondFrame = SolidFrame(0, 0, 255);
        TaskCompletionSource<VideoFrameReadResult> firstPending = NewCompletion<VideoFrameReadResult>();
        TaskCompletionSource<bool> firstStarted = NewCompletion<bool>();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.VideoFrames.Handler = (path, position, metadata, _) =>
        {
            if (path.Equals(firstPath, StringComparison.OrdinalIgnoreCase))
            {
                firstStarted.TrySetResult(true);
                return firstPending.Task;
            }

            return Task.FromResult(FrameResult(secondFrame, position, metadata));
        };
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        Task firstSelection = viewModel.DropMediaCommand.ExecuteAsync([firstPath]);
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Task secondSelection = viewModel.DropMediaCommand.ExecuteAsync([secondPath]);
        await WaitUntilAsync(
            viewModel,
            () => viewModel.VideoMetadata?.FilePath.Equals(secondPath, StringComparison.OrdinalIgnoreCase) == true);
        firstPending.SetResult(FrameResult(firstFrame, 0.5d, VideoMetadataFor(firstPath)));
        await Task.WhenAll(firstSelection, secondSelection);

        Assert.Equal(secondPath, viewModel.VideoMetadata?.FilePath);
        Assert.Equal(secondFrame, harness.ImageSources.LastDecodedImage);
        Assert.False(viewModel.HasVideoFrameError);
    }

    [Fact]
    public async Task SymbolizedVideoPreview_UsesDisplayedFrameAndBecomesStaleAfterSeeking()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("symbolize.mp4");
        TestHarness harness = new();
        DecodedImage firstFrame = SolidFrame(255, 0, 0);
        DecodedImage secondFrame = SolidFrame(0, 0, 255);
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.VideoFrames.Handler = (_, position, metadata, _) =>
            Task.FromResult(FrameResult(position < 0.7d ? firstFrame : secondFrame, position, metadata));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        viewModel.SelectedPreset = CharacterPresetOption.BuiltIn[2];
        viewModel.SelectedFont = "Consolas";
        viewModel.SelectedFontSize = 12;
        viewModel.SelectedOutputResolution = "720p";
        viewModel.SelectedSymbolPolarity = SymbolPolarity.DarkOnLight;

        await viewModel.PreviewFrameCommand.ExecuteAsync(null);

        Assert.Equal(firstFrame, harness.ImagePipeline.LastRenderedSource);
        Assert.Equal(CharacterPresetKind.Blocks, harness.ImagePipeline.LastRenderedPreset?.Kind);
        Assert.Equal("Consolas", harness.ImagePipeline.LastRenderSettings?.FontFamily);
        Assert.Equal(12, harness.ImagePipeline.LastRenderSettings?.FontSize);
        Assert.Equal(OutputResolution.Hd720, harness.ImagePipeline.LastRenderSettings?.OutputResolution);
        Assert.Equal(SymbolPolarity.DarkOnLight, harness.ImagePipeline.LastRenderSettings?.SymbolPolarity);
        Assert.True(viewModel.HasSymbolizedPreview);
        Assert.False(viewModel.IsSymbolizedPreviewStale);
        Assert.Equal(PreviewMode.Symbolized, viewModel.PreviewMode);

        viewModel.TimelinePosition = 0.8d;
        Assert.True(viewModel.IsSymbolizedPreviewStale);
        Assert.Equal(PreviewMode.Original, viewModel.PreviewMode);
        await WaitUntilAsync(viewModel, () => !viewModel.IsVideoFrameLoading);

        await viewModel.PreviewFrameCommand.ExecuteAsync(null);

        Assert.Equal(secondFrame, harness.ImagePipeline.LastRenderedSource);
        Assert.False(viewModel.IsSymbolizedPreviewStale);
        Assert.Equal(PreviewMode.Symbolized, viewModel.PreviewMode);
    }

    [Fact]
    public async Task RenderSettingChange_InvalidatesVideoPreviewButOutputFormatDoesNot()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("settings.mp4");
        TestHarness harness = ReadyVideoHarness();
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.PreviewFrameCommand.ExecuteAsync(null);

        viewModel.SelectedFontSize = 12;

        Assert.True(viewModel.IsSymbolizedPreviewStale);
        await viewModel.PreviewFrameCommand.ExecuteAsync(null);
        Assert.False(viewModel.IsSymbolizedPreviewStale);

        viewModel.SelectedImageOutputFormat = "JPG/JPEG";

        Assert.False(viewModel.IsSymbolizedPreviewStale);
    }

    [Fact]
    public async Task SymbolizationResultFromOldFrame_IsRejectedAfterSliderMoves()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("race.mp4");
        TestHarness harness = ReadyVideoHarness();
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        TaskCompletionSource<ImagePipelineResult> oldRender = NewCompletion<ImagePipelineResult>();
        TaskCompletionSource<bool> renderStarted = NewCompletion<bool>();
        harness.ImagePipeline.RenderHandler = (_, _, _, _) =>
        {
            renderStarted.TrySetResult(true);
            return oldRender.Task;
        };

        Task preview = viewModel.PreviewFrameCommand.ExecuteAsync(null);
        await renderStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        viewModel.TimelinePosition = 0.8d;
        await WaitUntilAsync(viewModel, () => !viewModel.IsVideoFrameLoading);
        oldRender.SetResult(RenderResult());
        await preview;

        Assert.False(viewModel.HasSymbolizedPreview);
        Assert.Equal(PreviewMode.Original, viewModel.PreviewMode);
    }

    [Fact]
    public async Task ImageRender_UsesDedicatedCommandStoresResultAndSelectsSymbolizedReview()
    {
        using TestMediaFiles files = new();
        string imagePath = files.Create("render.png");
        TestHarness harness = ReadyImageHarness();
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        await viewModel.DropMediaCommand.ExecuteAsync([imagePath]);

        Assert.True(viewModel.RenderImageCommand.CanExecute(null));
        Assert.False(viewModel.PreviewFrameCommand.CanExecute(null));
        Assert.False(viewModel.ExportImageCommand.CanExecute(null));
        Assert.False(viewModel.HasSymbolizedPreview);
        Assert.Equal("Render Image", viewModel.ImageRenderActionLabel);
        Assert.False(viewModel.ShowImageExportControls);

        await viewModel.RenderImageCommand.ExecuteAsync(null);

        Assert.Equal(RenderWorkflowStatus.RenderReady, viewModel.WorkflowStatus);
        Assert.True(viewModel.HasImageRender);
        Assert.True(viewModel.HasSymbolizedPreview);
        Assert.Equal(PreviewMode.Symbolized, viewModel.PreviewMode);
        Assert.True(viewModel.ShowImageResultPanel);
        Assert.True(viewModel.ShowImageExportControls);
        Assert.True(viewModel.ShowImageRenderMetrics);
        Assert.Equal("1 × 1", viewModel.ImageGridDimensionsText);
        Assert.Equal("2 × 1", viewModel.ImageOutputDimensionsText);
        Assert.True(viewModel.ExportImageCommand.CanExecute(null));
        Assert.Equal("Render Again", viewModel.ImageRenderActionLabel);
    }

    [Fact]
    public async Task ImageRender_SettingsMakeResultStaleButExportOnlyAndReviewChangesDoNot()
    {
        using TestMediaFiles files = new();
        string imagePath = files.Create("stale.png");
        TestHarness harness = ReadyImageHarness();
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([imagePath]);
        await viewModel.RenderImageCommand.ExecuteAsync(null);

        viewModel.SelectedFontSize = viewModel.SelectedFontSize;
        viewModel.SelectedImageOutputFormat = "JPG/JPEG";
        viewModel.SelectedFitMode = "100%";
        viewModel.SelectOriginalCommand.Execute(null);

        Assert.False(viewModel.IsImageRenderStale);
        Assert.Equal(RenderWorkflowStatus.RenderReady, viewModel.WorkflowStatus);
        Assert.True(viewModel.ExportImageCommand.CanExecute(null));

        viewModel.SelectedFontSize = 12;

        Assert.Equal(RenderWorkflowStatus.RenderStale, viewModel.WorkflowStatus);
        Assert.True(viewModel.IsImageRenderStale);
        Assert.True(viewModel.HasSymbolizedPreview);
        Assert.True(viewModel.ShowImageExportControls);
        Assert.True(viewModel.ShowImageRenderMetrics);
        Assert.True(viewModel.SelectSymbolizedCommand.CanExecute(null));
        Assert.False(viewModel.ExportImageCommand.CanExecute(null));
        Assert.Contains("settings changed", viewModel.ImageResultSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImageExport_ReusesRetainedRenderAcrossFormatsWithoutRerendering()
    {
        using TestMediaFiles files = new();
        string imagePath = files.Create("reuse.png");
        TestHarness harness = ReadyImageHarness();
        harness.SaveDialogs.ImageOutputPaths.Enqueue(files.PathFor("first.png"));
        harness.SaveDialogs.ImageOutputPaths.Enqueue(files.PathFor("second.jpg"));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([imagePath]);
        await viewModel.RenderImageCommand.ExecuteAsync(null);
        int renderCalls = harness.ImagePipeline.RenderCalls;

        await viewModel.ExportImageCommand.ExecuteAsync(null);
        RenderedImage firstExport = harness.ImageExporter.LastImage!;
        viewModel.SelectedImageOutputFormat = "JPG/JPEG";
        await viewModel.ExportImageCommand.ExecuteAsync(null);

        Assert.Equal(renderCalls, harness.ImagePipeline.RenderCalls);
        Assert.Equal(2, harness.ImageExporter.Calls);
        Assert.Same(firstExport, harness.ImageExporter.LastImage);
        Assert.False(firstExport.IsDisposed);
        Assert.Equal(ImageExportFormat.Jpeg, harness.ImageExporter.LastFormat);
        Assert.Equal(RenderWorkflowStatus.Exported, viewModel.WorkflowStatus);
        Assert.True(viewModel.CanOpenExportFolder);
        Assert.True(viewModel.ExportImageCommand.CanExecute(null));

        await viewModel.OpenExportFolderCommand.ExecuteAsync(null);
        Assert.Equal(Path.GetDirectoryName(files.PathFor("second.jpg")), harness.FolderLauncher.LastFolder);
    }

    [Fact]
    public async Task ImageExport_CancelledDialogAndFailurePreserveRetainedRenderForRetry()
    {
        using TestMediaFiles files = new();
        string imagePath = files.Create("failure.png");
        TestHarness harness = ReadyImageHarness();
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([imagePath]);
        await viewModel.RenderImageCommand.ExecuteAsync(null);

        await viewModel.ExportImageCommand.ExecuteAsync(null);

        Assert.Equal(0, harness.ImageExporter.Calls);
        Assert.Equal(RenderWorkflowStatus.RenderReady, viewModel.WorkflowStatus);
        Assert.True(viewModel.ExportImageCommand.CanExecute(null));

        harness.SaveDialogs.ImageOutputPaths.Enqueue(files.PathFor("failure-output.png"));
        harness.ImageExporter.Handler = (_, _, _, _, _) => throw new IOException("raw export failure");
        await viewModel.ExportImageCommand.ExecuteAsync(null);

        Assert.Equal(RenderWorkflowStatus.Failed, viewModel.WorkflowStatus);
        Assert.Equal("Image Export Failed", viewModel.ImageResultTitle);
        Assert.True(viewModel.HasImageRender);
        Assert.True(viewModel.ShowImageExportControls);
        Assert.True(viewModel.ExportImageCommand.CanExecute(null));
        Assert.DoesNotContain("raw export failure", viewModel.StatusMessage, StringComparison.Ordinal);

        harness.SaveDialogs.ImageOutputPaths.Enqueue(files.PathFor("retry-output.png"));
        harness.ImageExporter.Handler = (_, _, _, _, _) => Task.CompletedTask;
        int renderCalls = harness.ImagePipeline.RenderCalls;

        await viewModel.ExportImageCommand.ExecuteAsync(null);

        Assert.Equal(RenderWorkflowStatus.Exported, viewModel.WorkflowStatus);
        Assert.Equal(renderCalls, harness.ImagePipeline.RenderCalls);
        Assert.Equal(2, harness.ImageExporter.Calls);
    }

    [Fact]
    public async Task InitialRenderFailure_ShowsRenderOutcomeWithoutExportControls()
    {
        using TestMediaFiles files = new();
        string imagePath = files.Create("initial-render-failure.png");
        TestHarness harness = ReadyImageHarness();
        harness.ImagePipeline.RenderHandler = (_, _, _, _) => throw new InvalidOperationException("raw render failure");
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([imagePath]);

        await viewModel.RenderImageCommand.ExecuteAsync(null);

        Assert.Equal(RenderWorkflowStatus.Failed, viewModel.WorkflowStatus);
        Assert.Equal("Image Render Failed", viewModel.ImageResultTitle);
        Assert.True(viewModel.ShowImageResultPanel);
        Assert.False(viewModel.HasImageRender);
        Assert.False(viewModel.ShowImageExportControls);
        Assert.False(viewModel.ShowImageRenderMetrics);
        Assert.False(viewModel.ExportImageCommand.CanExecute(null));
        Assert.Equal("Render Image", viewModel.ImageRenderActionLabel);
        Assert.Equal(PreviewMode.Original, viewModel.PreviewMode);
        Assert.DoesNotContain("raw render failure", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedRenderAgain_PreservesPreviousRenderedResultAndCurrentExportability()
    {
        using TestMediaFiles files = new();
        string imagePath = files.Create("rerender-failure.png");
        TestHarness harness = ReadyImageHarness();
        RenderedImage retained = new(2, 1, [31, 32, 33, 255, 31, 32, 33, 255]);
        int call = 0;
        harness.ImagePipeline.RenderHandler = (_, _, _, _) => Interlocked.Increment(ref call) == 1
            ? Task.FromResult(RenderResult(retained))
            : throw new InvalidOperationException("raw rerender failure");
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([imagePath]);
        await viewModel.RenderImageCommand.ExecuteAsync(null);

        await viewModel.RenderImageCommand.ExecuteAsync(null);

        Assert.Equal(RenderWorkflowStatus.Failed, viewModel.WorkflowStatus);
        Assert.Equal("Image Render Failed", viewModel.ImageResultTitle);
        Assert.True(viewModel.HasImageRender);
        Assert.True(viewModel.ShowImageExportControls);
        Assert.True(viewModel.ExportImageCommand.CanExecute(null));
        Assert.False(retained.IsDisposed);
        Assert.Equal("Render Again", viewModel.ImageRenderActionLabel);
    }

    [Fact]
    public async Task InitialRenderCancellation_ShowsRenderOutcomeWithoutExportControls()
    {
        using TestMediaFiles files = new();
        string imagePath = files.Create("initial-render-cancel.png");
        TestHarness harness = ReadyImageHarness();
        TaskCompletionSource started = NewCompletion();
        harness.ImagePipeline.RenderHandler = async (_, _, _, cancellationToken) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        };
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([imagePath]);

        Task render = viewModel.RenderImageCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        viewModel.CancelExportCommand.Execute(null);
        await render;

        Assert.Equal(RenderWorkflowStatus.Cancelled, viewModel.WorkflowStatus);
        Assert.Equal("Image Rendering Cancelled", viewModel.ImageResultTitle);
        Assert.True(viewModel.ShowImageResultPanel);
        Assert.False(viewModel.HasImageRender);
        Assert.False(viewModel.ShowImageExportControls);
        Assert.False(viewModel.ShowImageRenderMetrics);
        Assert.False(viewModel.ExportImageCommand.CanExecute(null));
        Assert.Equal("Render Image", viewModel.ImageRenderActionLabel);
    }

    [Fact]
    public async Task CancelledImageExport_PreservesRetainedRenderAndAllowsRetry()
    {
        using TestMediaFiles files = new();
        string imagePath = files.Create("export-cancel.png");
        TestHarness harness = ReadyImageHarness();
        harness.SaveDialogs.ImageOutputPaths.Enqueue(files.PathFor("cancelled-output.png"));
        harness.SaveDialogs.ImageOutputPaths.Enqueue(files.PathFor("retry-output.png"));
        TaskCompletionSource started = NewCompletion();
        harness.ImageExporter.Handler = async (_, _, _, _, cancellationToken) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        };
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([imagePath]);
        await viewModel.RenderImageCommand.ExecuteAsync(null);
        int renderCalls = harness.ImagePipeline.RenderCalls;

        Task export = viewModel.ExportImageCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        viewModel.CancelExportCommand.Execute(null);
        await export;

        Assert.Equal(RenderWorkflowStatus.Cancelled, viewModel.WorkflowStatus);
        Assert.Equal("Image Export Cancelled", viewModel.ImageResultTitle);
        Assert.True(viewModel.HasImageRender);
        Assert.True(viewModel.ShowImageExportControls);
        Assert.True(viewModel.ExportImageCommand.CanExecute(null));

        harness.ImageExporter.Handler = (_, _, _, _, _) => Task.CompletedTask;
        await viewModel.ExportImageCommand.ExecuteAsync(null);

        Assert.Equal(RenderWorkflowStatus.Exported, viewModel.WorkflowStatus);
        Assert.Equal(renderCalls, harness.ImagePipeline.RenderCalls);
    }

    [Fact]
    public async Task RenderAgain_DisposesPreviousResultExactlyOnceAndMediaReplacementDisposesCurrent()
    {
        using TestMediaFiles files = new();
        string firstPath = files.Create("first.png");
        string secondPath = files.Create("second.png");
        TestHarness harness = ReadyImageHarness();
        RenderedImage first = new(2, 1, [1, 2, 3, 255, 1, 2, 3, 255]);
        RenderedImage second = new(2, 1, [4, 5, 6, 255, 4, 5, 6, 255]);
        Queue<RenderedImage> results = new([first, second]);
        harness.ImagePipeline.RenderHandler = (_, _, _, _) => Task.FromResult(RenderResult(results.Dequeue()));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([firstPath]);

        await viewModel.RenderImageCommand.ExecuteAsync(null);
        await viewModel.RenderImageCommand.ExecuteAsync(null);

        Assert.True(first.IsDisposed);
        Assert.False(second.IsDisposed);

        await viewModel.DropMediaCommand.ExecuteAsync([secondPath]);

        Assert.True(second.IsDisposed);
        Assert.False(viewModel.HasImageRender);
        Assert.False(viewModel.ShowImageRenderMetrics);
        Assert.Equal(string.Empty, viewModel.ImageGridDimensionsText);
        Assert.Equal(string.Empty, viewModel.ImageOutputDimensionsText);
    }

    [Fact]
    public async Task CancellingRenderAgain_PreservesPreviousReviewUntilCleanupCompletes()
    {
        using TestMediaFiles files = new();
        string imagePath = files.Create("cancel-rerender.png");
        TestHarness harness = ReadyImageHarness();
        RenderedImage retained = new(2, 1, [11, 12, 13, 255, 11, 12, 13, 255]);
        int call = 0;
        TaskCompletionSource started = NewCompletion();
        harness.ImagePipeline.RenderHandler = async (_, _, _, cancellationToken) =>
        {
            if (Interlocked.Increment(ref call) == 1)
            {
                return RenderResult(retained);
            }

            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        };
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([imagePath]);
        await viewModel.RenderImageCommand.ExecuteAsync(null);

        Task rerender = viewModel.RenderImageCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(viewModel.IsExportActive);

        viewModel.CancelExportCommand.Execute(null);

        Assert.True(viewModel.IsExportCancellationRequested);
        Assert.False(viewModel.CancelExportCommand.CanExecute(null));
        await rerender;

        Assert.Equal(RenderWorkflowStatus.Cancelled, viewModel.WorkflowStatus);
        Assert.Equal("Image Rendering Cancelled", viewModel.ImageResultTitle);
        Assert.False(viewModel.IsExportActive);
        Assert.True(viewModel.HasImageRender);
        Assert.True(viewModel.ShowImageExportControls);
        Assert.False(retained.IsDisposed);
        Assert.True(viewModel.ExportImageCommand.CanExecute(null));
    }

    [Fact]
    public async Task SupersededImageRender_DisposesLateResultWithoutReplacingCurrentReview()
    {
        using TestMediaFiles files = new();
        string imagePath = files.Create("late.png");
        TestHarness harness = ReadyImageHarness();
        TaskCompletionSource<ImagePipelineResult> pending = NewCompletion<ImagePipelineResult>();
        TaskCompletionSource started = NewCompletion();
        harness.ImagePipeline.RenderHandler = (_, _, _, _) =>
        {
            started.TrySetResult();
            return pending.Task;
        };
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([imagePath]);

        Task render = viewModel.RenderImageCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        viewModel.SelectedFontSize = 12;
        RenderedImage late = new(2, 1, [7, 8, 9, 255, 7, 8, 9, 255]);
        pending.SetResult(RenderResult(late));
        await render;

        Assert.True(late.IsDisposed);
        Assert.False(viewModel.HasImageRender);
        Assert.False(viewModel.ExportImageCommand.CanExecute(null));
    }

    [Fact]
    public async Task DisposingViewModel_ReleasesRetainedImageRender()
    {
        using TestMediaFiles files = new();
        string imagePath = files.Create("dispose-render.png");
        TestHarness harness = ReadyImageHarness();
        RenderedImage retained = new(2, 1, [21, 22, 23, 255, 21, 22, 23, 255]);
        harness.ImagePipeline.RenderHandler = (_, _, _, _) => Task.FromResult(RenderResult(retained));
        MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([imagePath]);
        await viewModel.RenderImageCommand.ExecuteAsync(null);

        viewModel.Dispose();

        Assert.True(retained.IsDisposed);
    }

    [Fact]
    public async Task SwitchingBetweenImageAndVideo_ClearsIncompatiblePreviewState()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("mode.mp4");
        string imagePath = files.Create("mode.png");
        TestHarness harness = ReadyVideoHarness();
        DecodedImage image = SolidFrame(25, 50, 75);
        harness.ImagePipeline.DecodeResult = image;
        harness.MediaInput.Handler = (path, _) => Task.FromResult(
            Path.GetExtension(path!).Equals(".png", StringComparison.OrdinalIgnoreCase)
                ? ImageResult(path!)
                : VideoResult(path!));
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.PreviewFrameCommand.ExecuteAsync(null);
        await viewModel.DropMediaCommand.ExecuteAsync([imagePath]);

        Assert.True(viewModel.IsImageSelected);
        Assert.False(viewModel.IsVideoSelected);
        Assert.False(viewModel.HasSymbolizedPreview);
        Assert.Equal(0d, viewModel.TimelinePosition);
        Assert.False(viewModel.IsVideoSeekEnabled);
        Assert.Equal(image, harness.ImageSources.LastDecodedImage);
        Assert.True(viewModel.RenderImageCommand.CanExecute(null));
        Assert.False(viewModel.PreviewFrameCommand.CanExecute(null));
        Assert.False(viewModel.ExportImageCommand.CanExecute(null));
    }

    [Fact]
    public async Task UnexpectedFrameReaderFailure_BecomesStableFriendlyState()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("native-failure.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.VideoFrames.Handler = (_, _, _, _) => throw new InvalidOperationException("raw native failure");
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        Assert.True(viewModel.HasVideoFrameError);
        Assert.Equal("GlyphFrame could not load this video frame.", viewModel.VideoFrameStatusMessage);
        Assert.DoesNotContain("raw native failure", viewModel.VideoFrameStatusMessage, StringComparison.Ordinal);
        Assert.Contains("raw native failure", viewModel.VideoFrameTechnicalDetail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NearestDecodedPosition_ReconcilesSliderAndFormattedTime()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("nearest.mp4");
        TestHarness harness = ReadyVideoHarness();
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        harness.VideoFrames.Handler = (_, requested, metadata, _) => Task.FromResult(
            FrameResult(SolidFrame(0, 0, 255), requested, metadata, actualPosition: 0.75d));

        viewModel.TimelinePosition = 0.8d;
        await WaitUntilAsync(viewModel, () => !viewModel.IsVideoFrameLoading);

        Assert.Equal(0.75d, viewModel.TimelinePosition, 3);
        Assert.Contains("0:07", viewModel.TimelineText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisposingViewModel_CancelsAndRejectsLateFrameCompletion()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("closing.mp4");
        TestHarness harness = new();
        TaskCompletionSource<bool> started = NewCompletion<bool>();
        TaskCompletionSource<VideoFrameReadResult> pending = NewCompletion<VideoFrameReadResult>();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.VideoFrames.Handler = (_, _, _, _) =>
        {
            started.TrySetResult(true);
            return pending.Task;
        };
        MainWindowViewModel viewModel = harness.CreateViewModel();

        Task selection = viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        viewModel.Dispose();
        pending.SetResult(FrameResult(SolidFrame(255, 0, 0), 0.5d, VideoMetadataFor(videoPath)));
        await selection;

        Assert.Null(harness.ImageSources.LastDecodedImage);
        Assert.False(viewModel.HasVideoFrame);
    }

    [Fact]
    public async Task VideoMode_ExposesPreviewFrameAndPrimaryRenderVideoActions()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("video-actions.mp4");
        TestHarness harness = ReadyVideoHarness();
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        Assert.True(viewModel.PreviewFrameCommand.CanExecute(null));
        Assert.True(viewModel.RenderVideoCommand.CanExecute(null));
        Assert.Equal("Preview Frame", viewModel.PreviewFrameActionLabel);
        Assert.Equal("Render Video", viewModel.VideoRenderActionLabel);
        Assert.False(viewModel.RenderImageCommand.CanExecute(null));
    }

    [Fact]
    public async Task VideoRender_RemainsAvailableWhenMiddleFramePreviewFails()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("preview-failed-renderable.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.VideoFrames.Handler = (_, position, _, _) => Task.FromResult(VideoFrameReadResult.Failure(
            VideoFrameReadStatus.Failed,
            VideoFrameReadErrorCategory.DecodeFailed,
            position,
            "Frame preview failed."));
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        Assert.True(viewModel.HasVideoFrameError);
        Assert.False(viewModel.PreviewFrameCommand.CanExecute(null));
        Assert.True(viewModel.RenderVideoCommand.CanExecute(null));

        string artifactPath = files.PathFor("preview-failed-artifact.mp4");
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(artifactPath, job.Identity)));
        await viewModel.RenderVideoCommand.ExecuteAsync(null);

        Assert.True(viewModel.ShowRenderedVideoPlayer);
        Assert.False(viewModel.ShowVideoFrameError);
        Assert.Equal(RenderWorkflowStatus.RenderReady, viewModel.WorkflowStatus);
    }

    [Fact]
    public async Task VideoRender_IsDisabledWhenBundledToolsAreUnavailable()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("tools-unavailable.mp4");
        TestHarness harness = ReadyVideoHarness();
        harness.FfmpegLocator.Available = false;
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        Assert.False(viewModel.RenderVideoCommand.CanExecute(null));
        Assert.True(viewModel.PreviewFrameCommand.CanExecute(null));
    }

    [Fact]
    public async Task VideoRender_CapturesImmutableJobAndSliderMovementDoesNotRejectResult()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("captured-job.mp4");
        string artifactPath = files.PathFor("managed-review.mp4");
        TestHarness harness = ReadyVideoHarness();
        TaskCompletionSource<ManagedVideoRenderResult> completion = NewCompletion<ManagedVideoRenderResult>();
        harness.ManagedVideoRenders.Handler = (_, _, _) => completion.Task;
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        viewModel.SelectedPreset = CharacterPresetOption.BuiltIn[2];
        viewModel.SelectedFont = "Consolas";
        viewModel.SelectedFontSize = 12;
        viewModel.SelectedOutputResolution = "720p";
        viewModel.SelectedSymbolPolarity = SymbolPolarity.DarkOnLight;
        viewModel.SelectedAudioMode = AudioExportMode.RemoveAudio;

        Task render = viewModel.RenderVideoCommand.ExecuteAsync(null);
        await WaitUntilAsync(viewModel, () => harness.ManagedVideoRenders.LastJob is not null);
        ManagedVideoRenderJob captured = harness.ManagedVideoRenders.LastJob!;
        viewModel.TimelinePosition = 0.9d;
        completion.SetResult(ManagedVideoRenderResult.Completed(Artifact(artifactPath, captured.Identity)));
        await render;

        Assert.Equal(videoPath, captured.InputPath);
        Assert.Equal(viewModel.SelectedPreset.Characters, captured.CharacterPreset.Characters);
        Assert.Equal("Consolas", captured.RenderSettings.FontFamily);
        Assert.Equal(12, captured.RenderSettings.FontSize);
        Assert.Equal(OutputResolution.Hd720, captured.RenderSettings.OutputResolution);
        Assert.Equal(SymbolPolarity.DarkOnLight, captured.RenderSettings.SymbolPolarity);
        Assert.Equal(AudioExportMode.RemoveAudio, captured.AudioMode);
        Assert.DoesNotContain("Position", captured.Identity.GetType().GetProperties().Select(property => property.Name));
        Assert.Equal(1, harness.ManagedArtifacts.AcceptCalls);
        Assert.Equal(0, harness.ManagedArtifacts.RejectCalls);
        Assert.Equal(RenderWorkflowStatus.RenderReady, viewModel.WorkflowStatus);
    }

    [Fact]
    public async Task SuccessfulVideoRender_PreflightsExactArtifactThenAcceptsAndSelectsSymbolized()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("successful-managed-render.mp4");
        string artifactPath = files.PathFor("accepted-managed.mp4");
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(artifactPath, job.Identity)));
        harness.ManagedArtifacts.AcceptHandler = (artifact, _) =>
        {
            Assert.True(harness.RenderedVideoPlayer.IsOpen);
            Assert.Equal(artifact.FilePath, harness.RenderedVideoPlayer.SourcePath);
            Assert.Equal(artifactPath, artifact.FilePath);
            return Task.CompletedTask;
        };
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        await viewModel.RenderVideoCommand.ExecuteAsync(null);

        Assert.Equal(1, harness.ManagedArtifacts.AcceptCalls);
        Assert.Equal(1, harness.RenderedVideoPlayer.OpenCalls);
        Assert.Equal(artifactPath, harness.RenderedVideoPlayer.SourcePath);
        Assert.Equal(PreviewMode.Symbolized, viewModel.PreviewMode);
        Assert.Equal(RenderWorkflowStatus.RenderReady, viewModel.WorkflowStatus);
        Assert.True(viewModel.HasVideoArtifact);
        Assert.True(viewModel.ShowRenderedVideoPlayer);
        Assert.True(viewModel.ToggleRenderedVideoPlaybackCommand.CanExecute(null));
        Assert.True(viewModel.FullscreenCommand.CanExecute(null));
        Assert.Equal("Video Ready", viewModel.VideoResultTitle);
        Assert.Equal("Render Again", viewModel.VideoRenderActionLabel);
        Assert.True(viewModel.ShowVideoRenderMetrics);
        Assert.Equal("100", viewModel.VideoRenderedFrameCountText);
        Assert.Equal("64 × 48", viewModel.VideoOutputDimensionsText);
        Assert.Equal("No source", viewModel.VideoAudioOutcomeText);
        Assert.Equal("Source video contains no audio", viewModel.VideoAudioOutcomeDescription);
        Assert.Equal("Ready for review and export.", viewModel.VideoResultSummary);
        Assert.False(viewModel.PreviewFrameCommand.CanExecute(null));
    }

    [Fact]
    public async Task FullVideoRender_ImmediatelyReplacesFooterAndRejectsObsoleteProgress()
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        TaskCompletionSource started = NewCompletion();
        TaskCompletionSource<ManagedVideoRenderResult> finished = NewCompletion<ManagedVideoRenderResult>();
        IProgress<VideoExportProgress>? capturedProgress = null;
        harness.ManagedVideoRenders.Handler = (job, _, progress) =>
        {
            capturedProgress = progress;
            started.SetResult();
            return finished.Task;
        };
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([files.Create("footer-source.mp4")]);

        Task render = viewModel.RenderVideoCommand.ExecuteAsync(null);
        await started.Task;

        Assert.Equal("Rendering the complete symbolized video...", viewModel.StatusMessage);
        FullVideoRenderIdentity identity = harness.ManagedVideoRenders.LastJob!.Identity;
        finished.SetResult(ManagedVideoRenderResult.Completed(Artifact(files.Create("footer-review.mp4"), identity)));
        await render;
        string completed = viewModel.StatusMessage;

        capturedProgress!.Report(ExportProgress(VideoExportStage.ProcessingFrames, 1, 100, 1,
            TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(10), "Obsolete progress"));
        Assert.Equal(completed, viewModel.StatusMessage);
        Assert.DoesNotContain("Video frame ready", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VideoMetrics_AppearOnlyAfterArtifactAcceptanceAndRemainAfterPublication()
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.Create("metrics-review.mp4"), job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([files.Create("metrics-source.mp4")]);
        Assert.False(viewModel.ShowVideoRenderMetrics);
        Assert.Empty(viewModel.VideoRenderedFrameCountText);
        Assert.Empty(viewModel.VideoOutputDimensionsText);
        Assert.Empty(viewModel.VideoAudioOutcomeText);
        Assert.Null(viewModel.VideoAudioOutcomeDescription);

        await viewModel.RenderVideoCommand.ExecuteAsync(null);
        Assert.True(viewModel.ShowVideoRenderMetrics);
        harness.SaveDialogs.VideoOutputPath = files.PathFor("metrics-export.mp4");
        await viewModel.ExportVideoCommand.ExecuteAsync(null);

        Assert.Equal("Video Exported", viewModel.VideoResultTitle);
        Assert.Equal("100", viewModel.VideoRenderedFrameCountText);
        Assert.Equal("64 × 48", viewModel.VideoOutputDimensionsText);
        Assert.Equal("No source", viewModel.VideoAudioOutcomeText);
        Assert.Equal("Source video contains no audio", viewModel.VideoAudioOutcomeDescription);
        Assert.Equal("Ready for review and another export.", viewModel.VideoResultSummary);
    }

    [Fact]
    public async Task VideoAudioMetric_RetainsAcceptedOutcomeWhenStaleAndRefreshesAfterReplacement()
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        int render = 0;
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.Create($"audio-metric-{render}.mp4"), job.Identity) with
            {
                AudioOutcome = render++ == 0 ? AudioMuxOutcome.StreamCopied : AudioMuxOutcome.Removed
            }));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([files.Create("audio-metric-source.mp4")]);

        await viewModel.RenderVideoCommand.ExecuteAsync(null);
        Assert.Equal("Original", viewModel.VideoAudioOutcomeText);
        Assert.Equal("Original audio preserved", viewModel.VideoAudioOutcomeDescription);

        viewModel.SelectedAudioMode = AudioExportMode.RemoveAudio;
        Assert.Equal(RenderWorkflowStatus.RenderStale, viewModel.WorkflowStatus);
        Assert.Equal("Original", viewModel.VideoAudioOutcomeText);
        Assert.False(viewModel.ExportVideoCommand.CanExecute(null));

        await viewModel.RenderVideoCommand.ExecuteAsync(null);
        Assert.Equal(RenderWorkflowStatus.RenderReady, viewModel.WorkflowStatus);
        Assert.Equal("Removed", viewModel.VideoAudioOutcomeText);
        Assert.Equal("Audio removed from rendered video", viewModel.VideoAudioOutcomeDescription);
    }

    [Fact]
    public async Task ManagedCompletedProgress_DoesNotShowSuccessBeforeAcceptanceAndPlayerOpen()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("delayed-player.mp4");
        string artifactPath = files.PathFor("delayed-player-artifact.mp4");
        TestHarness harness = ReadyVideoHarness();
        TaskCompletionSource<RenderedVideoOpenResult> playerOpened = NewCompletion<RenderedVideoOpenResult>();
        harness.ManagedVideoRenders.Handler = (job, _, progress) =>
        {
            progress?.Report(ExportProgress(
                VideoExportStage.Completed,
                10,
                10,
                100,
                TimeSpan.FromSeconds(1),
                TimeSpan.Zero,
                "Export completed."));
            return Task.FromResult(ManagedVideoRenderResult.Completed(Artifact(artifactPath, job.Identity)));
        };
        harness.RenderedVideoPlayer.Handler = (_, _) => playerOpened.Task;
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        Task render = viewModel.RenderVideoCommand.ExecuteAsync(null);
        await WaitUntilAsync(viewModel, () => harness.RenderedVideoPlayer.OpenCalls == 1);

        Assert.Equal(RenderWorkflowStatus.Rendering, viewModel.WorkflowStatus);
        Assert.False(viewModel.IsVideoResultSuccess);
        Assert.NotEqual(100d, viewModel.ExportProgress);

        playerOpened.SetResult(RenderedVideoOpenResult.Opened());
        await render;

        Assert.Equal(RenderWorkflowStatus.RenderReady, viewModel.WorkflowStatus);
        Assert.Equal(100d, viewModel.ExportProgress);
        Assert.True(viewModel.IsVideoResultSuccess);
    }

    [Fact]
    public async Task RenderSettingChangeRejectsObsoleteManagedCandidate()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("obsolete-settings.mp4");
        string artifactPath = files.PathFor("obsolete-settings-artifact.mp4");
        TestHarness harness = ReadyVideoHarness();
        TaskCompletionSource<ManagedVideoRenderResult> completion = NewCompletion<ManagedVideoRenderResult>();
        harness.ManagedVideoRenders.Handler = (_, _, _) => completion.Task;
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        Task render = viewModel.RenderVideoCommand.ExecuteAsync(null);
        await WaitUntilAsync(viewModel, () => harness.ManagedVideoRenders.LastJob is not null);
        ManagedVideoRenderJob job = harness.ManagedVideoRenders.LastJob!;
        viewModel.SelectedFontSize = 12;
        Assert.Equal(10, job.RenderSettings.FontSize);
        completion.SetResult(ManagedVideoRenderResult.Completed(Artifact(artifactPath, job.Identity)));
        await render;

        Assert.Equal(0, harness.ManagedArtifacts.AcceptCalls);
        Assert.Equal(1, harness.ManagedArtifacts.RejectCalls);
        Assert.False(viewModel.HasVideoArtifact);
        Assert.NotEqual(PreviewMode.Symbolized, viewModel.PreviewMode);
    }

    [Fact]
    public async Task AudioModeChangeRejectsObsoleteManagedCandidateWithoutInvalidatingFramePreview()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("obsolete-audio.mp4");
        string artifactPath = files.PathFor("obsolete-audio-artifact.mp4");
        TestHarness harness = ReadyVideoHarness();
        TaskCompletionSource<ManagedVideoRenderResult> completion = NewCompletion<ManagedVideoRenderResult>();
        harness.ManagedVideoRenders.Handler = (_, _, _) => completion.Task;
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.PreviewFrameCommand.ExecuteAsync(null);

        Task render = viewModel.RenderVideoCommand.ExecuteAsync(null);
        await WaitUntilAsync(viewModel, () => harness.ManagedVideoRenders.LastJob is not null);
        ManagedVideoRenderJob job = harness.ManagedVideoRenders.LastJob!;
        viewModel.SelectedAudioMode = AudioExportMode.RemoveAudio;
        completion.SetResult(ManagedVideoRenderResult.Completed(Artifact(artifactPath, job.Identity)));
        await render;

        Assert.Equal(1, harness.ManagedArtifacts.RejectCalls);
        Assert.False(viewModel.HasVideoArtifact);
        Assert.True(viewModel.HasSymbolizedPreview);
        Assert.False(viewModel.IsSymbolizedPreviewStale);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrCancelledReplacement_PreservesPreviousArtifactAndPlayer(bool cancelled)
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("replacement-preserves.mp4");
        string firstPath = files.PathFor("first-artifact.mp4");
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(firstPath, job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);
        ManagedVideoArtifact first = harness.ManagedArtifacts.Current!;
        int closeCalls = harness.RenderedVideoPlayer.CloseCalls;
        harness.ManagedVideoRenders.Handler = (_, _, _) => Task.FromResult(cancelled
            ? ManagedVideoRenderResult.Cancelled()
            : ManagedVideoRenderResult.Failure(
                ManagedVideoArtifactErrorCategory.RenderFailed,
                "The replacement render failed."));

        await viewModel.RenderVideoCommand.ExecuteAsync(null);

        Assert.Same(first, harness.ManagedArtifacts.Current);
        Assert.Equal(firstPath, harness.RenderedVideoPlayer.SourcePath);
        Assert.Equal(closeCalls, harness.RenderedVideoPlayer.CloseCalls);
        Assert.True(viewModel.HasVideoArtifact);
        Assert.Equal(cancelled ? RenderWorkflowStatus.Cancelled : RenderWorkflowStatus.Failed, viewModel.WorkflowStatus);
        Assert.Equal("Render Again", viewModel.VideoRenderActionLabel);
    }

    [Fact]
    public async Task SuccessfulReplacement_ClosesOldPlayerBeforeSessionAcceptsCandidate()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("replacement-order.mp4");
        string firstPath = files.PathFor("first-review.mp4");
        string secondPath = files.PathFor("second-review.mp4");
        TestHarness harness = ReadyVideoHarness();
        int renderNumber = 0;
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(
                ++renderNumber == 1 ? firstPath : secondPath,
                job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);
        harness.ManagedArtifacts.AcceptHandler = (_, _) =>
        {
            Assert.True(harness.RenderedVideoPlayer.IsOpen);
            Assert.Equal(secondPath, harness.RenderedVideoPlayer.SourcePath);
            Assert.NotEqual(firstPath, harness.RenderedVideoPlayer.SourcePath);
            return Task.CompletedTask;
        };

        await viewModel.RenderVideoCommand.ExecuteAsync(null);

        Assert.Equal(2, harness.ManagedArtifacts.AcceptCalls);
        Assert.Equal(secondPath, harness.ManagedArtifacts.Current?.FilePath);
        Assert.Equal(secondPath, harness.RenderedVideoPlayer.SourcePath);
    }

    [Fact]
    public async Task CandidateAcceptanceFailure_RejectsCandidateAndRestoresPreviousReview()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("acceptance-failure.mp4");
        string firstPath = files.Create("first-accepted.mp4");
        string rejectedPath = files.PathFor("rejected-candidate.mp4");
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(firstPath, job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);
        ManagedVideoArtifact retained = harness.ManagedArtifacts.Current!;
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(rejectedPath, job.Identity)));
        harness.ManagedArtifacts.AcceptHandler = (artifact, _) => artifact.FilePath == rejectedPath
            ? throw new InvalidOperationException("candidate rejected")
            : Task.CompletedTask;

        await viewModel.RenderVideoCommand.ExecuteAsync(null);

        Assert.Same(retained, harness.ManagedArtifacts.Current);
        Assert.Equal(2, harness.ManagedArtifacts.AcceptCalls);
        Assert.Equal(1, harness.ManagedArtifacts.RejectCalls);
        Assert.True(harness.RenderedVideoPlayer.IsOpen);
        Assert.Equal(firstPath, harness.RenderedVideoPlayer.SourcePath);
        Assert.Equal(RenderWorkflowStatus.Failed, viewModel.WorkflowStatus);
        Assert.True(viewModel.HasVideoArtifact);
    }

    [Fact]
    public async Task PlayerOpenFailure_RetainsAcceptedArtifactWithoutReportingVideoReady()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("player-failure.mp4");
        string artifactPath = files.PathFor("retained-unplayable.mp4");
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(artifactPath, job.Identity)));
        harness.RenderedVideoPlayer.Handler = (_, _) => Task.FromResult(
            RenderedVideoOpenResult.Failed("Review could not be opened.", "codec failure"));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        await viewModel.RenderVideoCommand.ExecuteAsync(null);

        Assert.NotNull(harness.ManagedArtifacts.Current);
        Assert.True(viewModel.HasVideoArtifact);
        Assert.False(viewModel.IsVideoResultSuccess);
        Assert.True(viewModel.IsVideoResultFailure);
        Assert.Equal("Video Review Unavailable", viewModel.VideoResultTitle);
        Assert.Equal("codec failure", viewModel.VideoRenderTechnicalDetail);
        Assert.False(viewModel.ShowRenderedVideoPlayer);
    }

    [Fact]
    public async Task SuccessfulMediaReplacement_ClosesPlayerBeforeClearingArtifactSession()
    {
        using TestMediaFiles files = new();
        string firstVideo = files.Create("first-selected.mp4");
        string secondVideo = files.Create("second-selected.mp4");
        string artifactPath = files.PathFor("selected-review.mp4");
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(artifactPath, job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([firstVideo]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);
        harness.ManagedArtifacts.ClearHandler = _ =>
        {
            Assert.False(harness.RenderedVideoPlayer.IsOpen);
            Assert.Null(harness.RenderedVideoPlayer.SourcePath);
            return Task.CompletedTask;
        };

        await viewModel.DropMediaCommand.ExecuteAsync([secondVideo]);

        Assert.Equal(secondVideo, viewModel.VideoMetadata?.FilePath);
        Assert.Null(harness.ManagedArtifacts.Current);
        Assert.False(viewModel.HasVideoArtifact);
    }

    [Fact]
    public async Task ManagedVideoRenderCancellation_RemainsActiveUntilBackendCleanupCompletes()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("cancel-managed-render.mp4");
        TestHarness harness = ReadyVideoHarness();
        TaskCompletionSource started = NewCompletion();
        TaskCompletionSource cleanupReleased = NewCompletion();
        harness.ManagedVideoRenders.Handler = async (_, cancellationToken, _) =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await cleanupReleased.Task;
            }

            return ManagedVideoRenderResult.Cancelled();
        };
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        Task render = viewModel.RenderVideoCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        viewModel.CancelExportCommand.Execute(null);

        Assert.True(viewModel.IsExportActive);
        Assert.True(viewModel.IsExportCancellationRequested);
        Assert.Equal(RenderWorkflowStatus.Cancelling, viewModel.WorkflowStatus);
        Assert.False(viewModel.CancelExportCommand.CanExecute(null));
        Assert.False(viewModel.RenderVideoCommand.CanExecute(null));

        cleanupReleased.SetResult();
        await render;

        Assert.False(viewModel.IsExportActive);
        Assert.Equal(RenderWorkflowStatus.Cancelled, viewModel.WorkflowStatus);
        Assert.True(viewModel.RenderVideoCommand.CanExecute(null));
        Assert.Null(harness.ManagedArtifacts.Current);
    }

    [Fact]
    public async Task RenderedVideoPlayer_PreservesArtifactAcrossTabsAndAudioModeMakesItStale()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("player-controls.mp4");
        string artifactPath = files.PathFor("player-controls-artifact.mp4");
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(artifactPath, job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);

        viewModel.ToggleRenderedVideoPlaybackCommand.Execute(null);
        Assert.True(harness.RenderedVideoPlayer.IsPlaying);

        viewModel.SelectOriginalCommand.Execute(null);
        Assert.False(harness.RenderedVideoPlayer.IsPlaying);
        Assert.Equal(artifactPath, harness.RenderedVideoPlayer.SourcePath);
        Assert.Equal(1, harness.RenderedVideoPlayer.OpenCalls);

        viewModel.SelectSymbolizedCommand.Execute(null);
        Assert.Equal(artifactPath, harness.RenderedVideoPlayer.SourcePath);
        Assert.Equal(1, harness.RenderedVideoPlayer.OpenCalls);

        viewModel.SelectedAudioMode = AudioExportMode.RemoveAudio;
        Assert.True(viewModel.IsSymbolizedPreviewStale);
        Assert.True(viewModel.ShowRenderedVideoPlayer);
        Assert.Equal(RenderWorkflowStatus.RenderStale, viewModel.WorkflowStatus);
    }

    [Fact]
    public async Task DisposingViewModel_ClosesRenderedVideoBeforeSessionLifetimeEnds()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("dispose-player.mp4");
        string artifactPath = files.PathFor("dispose-player-artifact.mp4");
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(artifactPath, job.Identity)));
        MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.RenderVideoCommand.ExecuteAsync(null);
        Assert.True(harness.RenderedVideoPlayer.IsOpen);

        viewModel.Dispose();

        Assert.False(harness.RenderedVideoPlayer.IsOpen);
        Assert.Null(harness.RenderedVideoPlayer.SourcePath);
        Assert.NotNull(harness.ManagedArtifacts.Current);
    }

    [Fact]
    public async Task CancellationDuringOpening_RestoresOldReviewAndRejectsCandidate()
    {
        using TestMediaFiles files = new();
        string source = files.Create("opening-cancel.mp4");
        string oldPath = files.Create("old-review.mp4");
        string newPath = files.Create("new-review.mp4");
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(oldPath, job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([source]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        ManagedVideoArtifact old = harness.ManagedArtifacts.Current!;
        TaskCompletionSource started = NewCompletion();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(newPath, job.Identity)));
        harness.RenderedVideoPlayer.Handler = async (path, token) =>
        {
            if (path == newPath)
            {
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            return RenderedVideoOpenResult.Opened();
        };
        Task render = vm.RenderVideoCommand.ExecuteAsync(null);
        await started.Task;
        Assert.True(vm.CanCancelExport);
        vm.CancelExportCommand.Execute(null);
        await render;
        Assert.Same(old, harness.ManagedArtifacts.Current);
        Assert.Equal(oldPath, harness.RenderedVideoPlayer.SourcePath);
        Assert.Equal(1, harness.ManagedArtifacts.RejectCalls);
        Assert.Equal(RenderWorkflowStatus.Cancelled, vm.WorkflowStatus);
        Assert.NotEqual(100, vm.ExportProgress);
    }

    [Fact]
    public async Task DelayedCommit_DisablesCancelAndRecordsInvalidatedAcceptedIdentityAsStale()
    {
        using TestMediaFiles files = new();
        string source = files.Create("commit.mp4");
        TestHarness harness = ReadyVideoHarness();
        TaskCompletionSource entered = NewCompletion();
        TaskCompletionSource release = NewCompletion();
        harness.ManagedArtifacts.AcceptHandler = async (_, _) =>
        {
            entered.SetResult();
            await release.Task;
        };
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.Create("commit-review.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([source]);
        Task render = vm.RenderVideoCommand.ExecuteAsync(null);
        await entered.Task;
        Assert.False(vm.CanCancelExport);
        Assert.Contains("cannot cancel", vm.ExportProgressText, StringComparison.Ordinal);
        vm.CancelExportCommand.Execute(null);
        Assert.False(vm.IsExportCancellationRequested);
        vm.SelectedFontSize = 12;
        release.SetResult();
        await render;
        Assert.True(vm.HasVideoArtifact);
        Assert.True(vm.IsSymbolizedPreviewStale);
        Assert.Equal(RenderWorkflowStatus.RenderStale, vm.WorkflowStatus);
        Assert.True(harness.RenderedVideoPlayer.IsOpen);
        Assert.NotEqual(PreviewMode.Symbolized, vm.PreviewMode);
        Assert.NotEqual(100, vm.ExportProgress);
    }

    [Fact]
    public async Task DisposeDuringOpening_RejectsLateCandidateWithoutReopeningOrSuccess()
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        TaskCompletionSource<RenderedVideoOpenResult> opened = NewCompletion<RenderedVideoOpenResult>();
        TaskCompletionSource entered = NewCompletion();
        harness.RenderedVideoPlayer.Handler = (_, _) =>
        {
            entered.SetResult();
            return opened.Task;
        };
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.Create("dispose-candidate.mp4"), job.Identity)));
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("dispose-source.mp4")]);
        Task render = vm.RenderVideoCommand.ExecuteAsync(null);
        await entered.Task;
        vm.Dispose();
        opened.SetResult(RenderedVideoOpenResult.Opened());
        await render;
        Assert.Equal(0, harness.ManagedArtifacts.AcceptCalls);
        Assert.Equal(1, harness.ManagedArtifacts.RejectCalls);
        Assert.False(harness.RenderedVideoPlayer.IsOpen);
        Assert.NotEqual(100, vm.ExportProgress);
    }

    [Fact]
    public async Task RuntimePlaybackFailure_NotifiesAllResultBindingsAndKeepsArtifact()
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.Create("runtime-review.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("runtime-source.mp4")]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        HashSet<string?> notifications = [];
        vm.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        harness.RenderedVideoPlayer.FailPlayback();
        Assert.True(vm.HasVideoArtifact);
        Assert.True(vm.ShowVideoResultPanel);
        Assert.True(vm.IsVideoResultFailure);
        Assert.False(vm.IsVideoResultSuccess);
        Assert.Equal("Video Review Unavailable", vm.VideoResultTitle);
        foreach (string property in new[]
        {
            nameof(vm.ShowVideoResultPanel), nameof(vm.VideoResultTitle), nameof(vm.VideoResultSummary),
            nameof(vm.IsVideoResultSuccess), nameof(vm.IsVideoResultFailure), nameof(vm.IsVideoResultWarning),
            nameof(vm.ShowRenderedVideoReviewError), nameof(vm.WorkflowStatus)
        })
        {
            Assert.Contains(property, notifications);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrInvalidatedOpening_PreservesPreviousReview(bool invalidate)
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        string previousPath = files.Create("previous-open-review.mp4");
        string candidatePath = files.Create("candidate-open-review.mp4");
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(previousPath, job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("open-source.mp4")]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        ManagedVideoArtifact previous = harness.ManagedArtifacts.Current!;
        TaskCompletionSource entered = NewCompletion();
        TaskCompletionSource<RenderedVideoOpenResult> opened = NewCompletion<RenderedVideoOpenResult>();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(candidatePath, job.Identity)));
        harness.RenderedVideoPlayer.Handler = (path, _) =>
        {
            if (path != candidatePath)
            {
                return Task.FromResult(RenderedVideoOpenResult.Opened());
            }
            entered.SetResult();
            return opened.Task;
        };
        Task render = vm.RenderVideoCommand.ExecuteAsync(null);
        await entered.Task;
        if (invalidate)
        {
            vm.SelectedAudioMode = AudioExportMode.RemoveAudio;
        }
        opened.SetResult(invalidate ? RenderedVideoOpenResult.Opened() : RenderedVideoOpenResult.Failed("Cannot open."));
        await render;
        Assert.Same(previous, harness.ManagedArtifacts.Current);
        Assert.Equal(previousPath, harness.RenderedVideoPlayer.SourcePath);
        Assert.True(harness.RenderedVideoPlayer.IsOpen);
        Assert.Equal(1, harness.ManagedArtifacts.AcceptCalls);
        Assert.Equal(1, harness.ManagedArtifacts.RejectCalls);
        Assert.False(vm.IsVideoResultSuccess);
        Assert.NotEqual(100, vm.ExportProgress);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedAcceptance_CompletesOnlyForLiveOperation(bool dispose)
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        TaskCompletionSource entered = NewCompletion();
        TaskCompletionSource release = NewCompletion();
        harness.ManagedArtifacts.AcceptHandler = async (_, token) =>
        {
            Assert.False(token.CanBeCanceled);
            entered.SetResult();
            await release.Task;
        };
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.Create("accept-review.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("accept-source.mp4")]);
        Task render = vm.RenderVideoCommand.ExecuteAsync(null);
        await entered.Task;
        Assert.NotEqual(100, vm.ExportProgress);
        Assert.False(vm.CanCancelExport);
        if (dispose)
        {
            vm.Dispose();
        }
        release.SetResult();
        await render;
        Assert.NotNull(harness.ManagedArtifacts.Current);
        Assert.Equal(0, harness.ManagedArtifacts.RejectCalls);
        Assert.Equal(!dispose, harness.RenderedVideoPlayer.IsOpen);
        if (dispose)
        {
            Assert.NotEqual(100, vm.ExportProgress);
            Assert.NotEqual(PreviewMode.Symbolized, vm.PreviewMode);
        }
        else
        {
            Assert.Equal(100, vm.ExportProgress);
            Assert.Equal(PreviewMode.Symbolized, vm.PreviewMode);
            Assert.Equal("Video Ready", vm.VideoResultTitle);
        }
    }

    [Theory]
    [InlineData("success", false)]
    [InlineData("failure", false)]
    [InlineData("cancelled", false)]
    [InlineData("success", true)]
    [InlineData("failure", true)]
    [InlineData("cancelled", true)]
    public async Task ArtifactExportOutcome_DoesNotReplaceOrMislabelReview(string outcome, bool stale)
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.Create("direct-review.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("direct-source.mp4")]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        ManagedVideoArtifact retained = harness.ManagedArtifacts.Current!;
        harness.SaveDialogs.VideoOutputPath = files.PathFor("direct-destination.mp4");
        harness.VideoPublications.Handler = (job, _, _) => Task.FromResult(outcome switch
        {
            "failure" => VideoExportResult.Failure(VideoExportErrorCategory.EncoderFailed, "Export failed."),
            "cancelled" => VideoExportResult.Cancelled(),
            _ => VideoExportResult.Completed(job.Destination.FilePath, 100, new PixelDimensions(64, 48), 10)
        });
        await vm.ExportVideoCommand.ExecuteAsync(null);
        if (stale) vm.SelectedFontSize = 12;
        Assert.Same(retained, harness.ManagedArtifacts.Current);
        Assert.True(vm.ShowVideoResultPanel);
        Assert.True(vm.ShowRenderedVideoPlayer);
        Assert.Equal(stale, vm.IsSymbolizedPreviewStale);
        Assert.Equal(stale ? "Render Stale" : outcome switch
        {
            "failure" => "Video Export Failed",
            "cancelled" => "Video Export Cancelled",
            _ => "Video Exported"
        }, vm.VideoResultTitle);
    }

    [Fact]
    public async Task ViewUnloadClosesAndReloadReopensSameArtifactPaused()
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.Create("reload-review.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("reload-source.mp4")]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        string? retained = harness.RenderedVideoPlayer.SourcePath;
        vm.RenderedVideoPositionSeconds = 0.5;
        vm.UnloadReviewView();
        Assert.False(harness.RenderedVideoPlayer.IsOpen);
        await vm.LoadReviewViewAsync(null);
        Assert.Equal(retained, harness.RenderedVideoPlayer.SourcePath);
        Assert.Equal(0.5, vm.RenderedVideoPositionSeconds);
        Assert.False(harness.RenderedVideoPlayer.IsPlaying);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task ViewReloadAcrossCommit_RecoversExactAcceptedArtifact(bool reloadBeforeCommit, bool stale)
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        int renders = 0;
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(ManagedVideoRenderResult.Completed(
            Artifact(files.Create($"review-{++renders}.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("source.mp4")]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        TaskCompletionSource entered = NewCompletion();
        TaskCompletionSource release = NewCompletion();
        harness.ManagedArtifacts.AcceptHandler = async (_, _) => { entered.SetResult(); await release.Task; };
        Task render = vm.RenderVideoCommand.ExecuteAsync(null);
        await entered.Task;
        vm.UnloadReviewView();
        if (reloadBeforeCommit)
        {
            await vm.LoadReviewViewAsync(null);
            await vm.LoadReviewViewAsync(null); // Repeated Loaded notifications must coalesce.
        }
        if (stale)
        {
            vm.SelectedAudioMode = AudioExportMode.RemoveAudio;
        }
        Assert.False(vm.IsVideoResultSuccess);
        release.SetResult();
        await render;
        ManagedVideoArtifact accepted = harness.ManagedArtifacts.Current!;
        if (!reloadBeforeCommit)
        {
            Assert.False(harness.RenderedVideoPlayer.IsOpen);
            Assert.Equal("Video Review Suspended", vm.VideoResultTitle);
            Assert.False(vm.IsVideoResultSuccess);
            Assert.NotEqual(100, vm.ExportProgress);
            await vm.LoadReviewViewAsync(null);
        }
        Assert.True(vm.HasVideoArtifact);
        Assert.Equal(accepted.FilePath, harness.RenderedVideoPlayer.SourcePath);
        Assert.True(harness.RenderedVideoPlayer.IsOpen);
        Assert.Equal(stale, vm.IsSymbolizedPreviewStale);
        Assert.Equal(stale ? "Render Stale" : "Video Ready", vm.VideoResultTitle);
        Assert.Equal(!stale, vm.IsVideoResultSuccess);
        Assert.False(vm.IsVideoResultFailure);
        Assert.False(vm.ShowRenderedVideoReviewError);
        Assert.Equal(stale ? RenderWorkflowStatus.RenderStale : RenderWorkflowStatus.RenderReady, vm.WorkflowStatus);
        Assert.Equal(3, harness.RenderedVideoPlayer.OpenCalls); // Initial, preflight, one recovery.
    }

    [Fact]
    public async Task CommitRecoveryFailure_RetainsArtifactWithoutFalseSuccess()
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(ManagedVideoRenderResult.Completed(
            Artifact(files.Create("recovery-failure.mp4"), job.Identity)));
        TaskCompletionSource entered = NewCompletion();
        TaskCompletionSource release = NewCompletion();
        harness.ManagedArtifacts.AcceptHandler = async (_, _) => { entered.SetResult(); await release.Task; };
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("source.mp4")]);
        Task render = vm.RenderVideoCommand.ExecuteAsync(null);
        await entered.Task;
        vm.UnloadReviewView();
        await vm.LoadReviewViewAsync(null);
        harness.RenderedVideoPlayer.Handler = (_, _) => Task.FromResult(RenderedVideoOpenResult.Failed("Review failed.", "recovery codec detail"));
        release.SetResult();
        await render;
        Assert.True(vm.HasVideoArtifact);
        Assert.NotNull(harness.ManagedArtifacts.Current);
        Assert.Equal("Video Review Unavailable", vm.VideoResultTitle);
        Assert.Equal(PreviewMode.Original, vm.PreviewMode);
        vm.SelectSymbolizedCommand.Execute(null);
        Assert.True(vm.ShowRenderedVideoReviewError);
        Assert.True(vm.IsVideoResultFailure);
        Assert.False(vm.IsVideoResultSuccess);
        Assert.False(vm.IsVideoResultWarning);
        Assert.False(vm.IsSymbolizedPreviewStale);
        Assert.Equal("recovery codec detail", vm.VideoRenderTechnicalDetail);
        Assert.NotEqual(100, vm.ExportProgress);
        Assert.Equal(2, harness.RenderedVideoPlayer.OpenCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SupersededOrDisposedRecovery_CannotMutateNewerReview(bool dispose)
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        int renders = 0;
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(ManagedVideoRenderResult.Completed(
            Artifact(files.Create($"superseded-{++renders}.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("source.mp4")]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        string previous = harness.ManagedArtifacts.Current!.FilePath;
        TaskCompletionSource entered = NewCompletion();
        TaskCompletionSource<RenderedVideoOpenResult> release = NewCompletion<RenderedVideoOpenResult>();
        harness.RenderedVideoPlayer.Handler = (path, _) =>
        {
            if (path != previous) return Task.FromResult(RenderedVideoOpenResult.Opened());
            entered.SetResult();
            return release.Task; // Intentionally completes after cancellation/supersession.
        };
        vm.UnloadReviewView();
        Task reload = vm.LoadReviewViewAsync(null);
        await entered.Task;
        if (dispose) vm.Dispose();
        else await vm.RenderVideoCommand.ExecuteAsync(null);
        int closes = harness.RenderedVideoPlayer.CloseCalls;
        List<string?> changes = [];
        vm.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        release.SetResult(RenderedVideoOpenResult.Failed("Obsolete failure.", "old recovery"));
        await reload;
        Assert.Empty(changes);
        Assert.Equal(closes, harness.RenderedVideoPlayer.CloseCalls);
        if (dispose)
        {
            Assert.False(harness.RenderedVideoPlayer.IsOpen);
            Assert.Null(harness.RenderedVideoPlayer.SourcePath);
        }
        else
        {
            Assert.Equal(harness.ManagedArtifacts.Current!.FilePath, harness.RenderedVideoPlayer.SourcePath);
            Assert.NotEqual(previous, harness.RenderedVideoPlayer.SourcePath);
            Assert.True(harness.RenderedVideoPlayer.IsOpen);
            Assert.Equal("Video Ready", vm.VideoResultTitle);
            Assert.True(vm.IsVideoResultSuccess);
            Assert.False(vm.IsVideoResultFailure);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnavailableReview_SurvivesFailedOrCancelledRenderRetry(bool cancel)
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(ManagedVideoRenderResult.Completed(
            Artifact(files.Create("unavailable.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("source.mp4")]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        ManagedVideoArtifact retained = harness.ManagedArtifacts.Current!;
        harness.RenderedVideoPlayer.FailPlayback();
        HashSet<string?> changes = [];
        vm.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        harness.ManagedVideoRenders.Handler = (_, _, _) =>
        {
            Assert.Equal("Video Review Unavailable", vm.VideoResultTitle);
            Assert.Contains("Controlled playback failure", vm.VideoRenderTechnicalDetail);
            return Task.FromResult(cancel ? ManagedVideoRenderResult.Cancelled() : ManagedVideoRenderResult.Failure(
                ManagedVideoArtifactErrorCategory.RenderFailed, "Render failed.", "render diagnostic"));
        };
        await vm.RenderVideoCommand.ExecuteAsync(null);
        Assert.Same(retained, harness.ManagedArtifacts.Current);
        Assert.Equal(1, harness.RenderedVideoPlayer.OpenCalls);
        Assert.False(harness.RenderedVideoPlayer.IsOpen);
        Assert.Equal("Video Review Unavailable", vm.VideoResultTitle);
        Assert.Contains("playback is unavailable", vm.VideoResultSummary);
        Assert.True(vm.IsVideoResultFailure);
        Assert.False(vm.IsVideoResultSuccess);
        Assert.False(vm.IsVideoResultWarning);
        Assert.Contains("Controlled playback failure", vm.VideoRenderTechnicalDetail);
        if (!cancel) Assert.Contains("render diagnostic", vm.VideoRenderTechnicalDetail);
        foreach (string name in new[] { nameof(vm.VideoResultTitle), nameof(vm.VideoResultSummary), nameof(vm.IsVideoResultFailure), nameof(vm.IsVideoResultWarning), nameof(vm.VideoRenderTechnicalDetail) })
        {
            Assert.Contains(name, changes);
        }
    }

    [Theory]
    [InlineData("replace")]
    [InlineData("reload")]
    [InlineData("media")]
    public async Task ReviewErrorClearsOnlyOnSuccessfulOpenOrMediaReplacement(string action)
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        int renders = 0;
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(ManagedVideoRenderResult.Completed(
            Artifact(files.Create($"clear-{++renders}.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("source.mp4")]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        harness.RenderedVideoPlayer.FailPlayback();
        if (action == "replace") await vm.RenderVideoCommand.ExecuteAsync(null);
        else if (action == "media") await vm.DropMediaCommand.ExecuteAsync([files.Create("other.mp4")]);
        else { vm.UnloadReviewView(); await vm.LoadReviewViewAsync(null); }
        Assert.Null(vm.VideoRenderTechnicalDetail);
        Assert.False(vm.ShowRenderedVideoReviewError);
        Assert.False(vm.IsVideoResultFailure);
        if (action == "media")
        {
            Assert.False(vm.HasVideoArtifact);
            Assert.False(harness.RenderedVideoPlayer.IsOpen);
        }
        else
        {
            Assert.True(harness.RenderedVideoPlayer.IsOpen);
            Assert.True(vm.IsVideoResultSuccess);
            Assert.Equal("Video Ready", vm.VideoResultTitle);
        }
    }

    [Fact]
    public async Task QueuedReloads_AttemptCurrentRecoveryOnceEvenWhenItFails()
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(ManagedVideoRenderResult.Completed(
            Artifact(files.Create("queued-review.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("source.mp4")]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        TaskCompletionSource entered = NewCompletion();
        TaskCompletionSource<RenderedVideoOpenResult> release = NewCompletion<RenderedVideoOpenResult>();
        int attempts = 0;
        harness.RenderedVideoPlayer.Handler = (_, _) =>
        {
            if (++attempts != 1) return Task.FromResult(RenderedVideoOpenResult.Failed("Recovery failed.", "current recovery"));
            entered.SetResult();
            return release.Task;
        };
        vm.UnloadReviewView();
        Task old = vm.LoadReviewViewAsync(null);
        await entered.Task;
        vm.UnloadReviewView();
        Task first = vm.LoadReviewViewAsync(null);
        Task duplicate = vm.LoadReviewViewAsync(null);
        release.SetResult(RenderedVideoOpenResult.Opened());
        await Task.WhenAll(old, first, duplicate);
        Assert.Equal(2, attempts);
        Assert.Equal(3, harness.RenderedVideoPlayer.OpenCalls);
        Assert.False(harness.RenderedVideoPlayer.IsOpen);
        Assert.True(vm.HasVideoArtifact);
        Assert.Equal("Video Review Unavailable", vm.VideoResultTitle);
        Assert.Equal("current recovery", vm.VideoRenderTechnicalDetail);
        Assert.False(vm.IsVideoResultSuccess);
    }

    [Fact]
    public async Task FailedReplacementPreflight_CanRestorePreviouslyUnavailableReviewWithoutErasingOperationFailure()
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        string oldPath = files.Create("previous-error.mp4");
        string candidatePath = files.Create("failed-replacement.mp4");
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(ManagedVideoRenderResult.Completed(Artifact(oldPath, job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("source.mp4")]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        harness.RenderedVideoPlayer.FailPlayback();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(ManagedVideoRenderResult.Completed(Artifact(candidatePath, job.Identity)));
        harness.RenderedVideoPlayer.Handler = (path, _) => Task.FromResult(path == candidatePath
            ? RenderedVideoOpenResult.Failed("Candidate failed.", "replacement diagnostic") : RenderedVideoOpenResult.Opened());
        await vm.RenderVideoCommand.ExecuteAsync(null);
        Assert.Equal(oldPath, harness.ManagedArtifacts.Current?.FilePath);
        Assert.Equal(oldPath, harness.RenderedVideoPlayer.SourcePath);
        Assert.True(harness.RenderedVideoPlayer.IsOpen);
        Assert.False(vm.ShowRenderedVideoReviewError);
        Assert.Equal("Video Render Failed", vm.VideoResultTitle);
        Assert.Equal("replacement diagnostic", vm.VideoRenderTechnicalDetail);
        Assert.True(vm.IsVideoResultFailure);
        Assert.False(vm.IsVideoResultSuccess);
        Assert.False(vm.IsVideoResultWarning);
    }

    [Theory]
    [InlineData("success", false)]
    [InlineData("failure", false)]
    [InlineData("cancelled", false)]
    [InlineData("success", true)]
    [InlineData("failure", true)]
    [InlineData("cancelled", true)]
    public async Task ArtifactExportCompletion_AwaitsDeferredReviewRecovery(string outcome, bool stale)
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        string artifactPath = files.Create("direct-deferred-review.mp4");
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(artifactPath, job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("source.mp4")]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        vm.RenderedVideoPositionSeconds = 2;
        ManagedVideoArtifact retained = harness.ManagedArtifacts.Current!;
        TaskCompletionSource started = NewCompletion();
        TaskCompletionSource cleanupFinished = NewCompletion();
        harness.SaveDialogs.VideoOutputPath = files.PathFor("direct-output.mp4");
        harness.VideoPublications.Handler = async (job, _, _) =>
        {
            started.SetResult();
            await cleanupFinished.Task;
            return outcome switch
            {
                "failure" => VideoExportResult.Failure(VideoExportErrorCategory.EncoderFailed, "Export failed."),
                "cancelled" => VideoExportResult.Cancelled(),
                _ => VideoExportResult.Completed(job.Destination.FilePath, 100, new PixelDimensions(64, 48), 10)
            };
        };
        Task export = vm.ExportVideoCommand.ExecuteAsync(null);
        await started.Task;
        if (stale) vm.SelectedAudioMode = AudioExportMode.RemoveAudio;
        vm.UnloadReviewView();
        Task load = vm.LoadReviewViewAsync(null);
        Task duplicateLoad = vm.LoadReviewViewAsync(null);
        Assert.Equal("Opening Video Review", vm.VideoResultTitle);
        cleanupFinished.SetResult();
        await Task.WhenAll(export, load, duplicateLoad);
        Assert.Same(retained, harness.ManagedArtifacts.Current);
        Assert.True(harness.RenderedVideoPlayer.IsOpen);
        Assert.Equal(artifactPath, harness.RenderedVideoPlayer.SourcePath);
        Assert.Equal(2, harness.RenderedVideoPlayer.OpenCalls);
        Assert.Equal(2, vm.RenderedVideoPositionSeconds);
        Assert.Equal(stale, vm.IsSymbolizedPreviewStale);
        Assert.False(vm.ShowRenderedVideoReviewError);
        Assert.False(vm.IsExportActive);
        Assert.Equal(outcome == "success" && !stale, vm.IsVideoResultSuccess);
        Assert.Equal(outcome == "failure", vm.IsVideoResultFailure);
        Assert.Equal(outcome switch
        {
            "failure" => RenderWorkflowStatus.Failed,
            "cancelled" => RenderWorkflowStatus.Cancelled,
            _ => RenderWorkflowStatus.Exported
        }, vm.WorkflowStatus);
        Assert.NotEqual("Opening Video Review", vm.VideoResultTitle);
        Assert.Equal(outcome switch
        {
            "failure" => "Video Export Failed",
            "cancelled" => "Video Export Cancelled",
            _ => stale ? "Render Stale" : "Video Exported"
        }, vm.VideoResultTitle);
    }

    [Fact]
    public async Task ArtifactExportCompletion_LeavesUnloadedReviewSuspendedUntilNextLoad()
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        string artifactPath = files.Create("late-load-review.mp4");
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(artifactPath, job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("source.mp4")]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        TaskCompletionSource started = NewCompletion();
        TaskCompletionSource finished = NewCompletion();
        harness.SaveDialogs.VideoOutputPath = files.PathFor("direct-output.mp4");
        harness.VideoPublications.Handler = async (_, _, _) =>
        {
            started.SetResult();
            await finished.Task;
            return VideoExportResult.Cancelled();
        };
        Task export = vm.ExportVideoCommand.ExecuteAsync(null);
        await started.Task;
        vm.UnloadReviewView();
        finished.SetResult();
        await export;
        Assert.False(harness.RenderedVideoPlayer.IsOpen);
        Assert.Equal("Video Review Suspended", vm.VideoResultTitle);
        Assert.NotEqual("Opening Video Review", vm.VideoResultTitle);
        Assert.Equal(1, harness.RenderedVideoPlayer.OpenCalls);
        await vm.LoadReviewViewAsync(null);
        Assert.True(harness.RenderedVideoPlayer.IsOpen);
        Assert.Equal(artifactPath, harness.RenderedVideoPlayer.SourcePath);
        Assert.Equal("Video Export Cancelled", vm.VideoResultTitle);
    }

    [Fact]
    public async Task DirectExportDeferredRecoveryFailure_IsTruthfulAndRetainsArtifact()
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        ManagedVideoArtifact? retained = null;
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(retained = Artifact(files.Create("failed-reopen-review.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("source.mp4")]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        TaskCompletionSource started = NewCompletion();
        TaskCompletionSource finished = NewCompletion();
        harness.SaveDialogs.VideoOutputPath = files.PathFor("direct-output.mp4");
        harness.VideoPublications.Handler = async (_, _, _) => { started.SetResult(); await finished.Task; return VideoExportResult.Cancelled(); };
        Task export = vm.ExportVideoCommand.ExecuteAsync(null);
        await started.Task;
        vm.UnloadReviewView();
        await vm.LoadReviewViewAsync(null);
        harness.RenderedVideoPlayer.Handler = (_, _) => Task.FromResult(
            RenderedVideoOpenResult.Failed("Review failed.", "direct-export recovery diagnostic"));
        finished.SetResult();
        await export;
        Assert.Same(retained, harness.ManagedArtifacts.Current);
        Assert.False(harness.RenderedVideoPlayer.IsOpen);
        Assert.Equal("Video Review Unavailable", vm.VideoResultTitle);
        Assert.True(vm.IsVideoResultFailure);
        Assert.False(vm.IsVideoResultSuccess);
        Assert.False(vm.IsVideoResultWarning);
        Assert.Contains("direct-export recovery diagnostic", vm.VideoRenderTechnicalDetail);
        Assert.NotEqual("Opening Video Review", vm.VideoResultTitle);
    }

    [Fact]
    public async Task AcceptedArtifact_DisablesFramePreviewAndSliderMovementDoesNotInvalidateIt()
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        string artifactPath = files.Create("frame-preview-review.mp4");
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(artifactPath, job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("source.mp4")]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        ManagedVideoArtifact retained = harness.ManagedArtifacts.Current!;

        Assert.False(vm.PreviewFrameCommand.CanExecute(null));
        int renderCalls = harness.ImagePipeline.RenderCalls;
        vm.TimelinePosition = 0.75;
        await WaitUntilAsync(vm, () => !vm.IsVideoFrameLoading);

        Assert.Same(retained, harness.ManagedArtifacts.Current);
        Assert.True(harness.RenderedVideoPlayer.IsOpen);
        Assert.Equal(artifactPath, harness.RenderedVideoPlayer.SourcePath);
        Assert.Equal(renderCalls, harness.ImagePipeline.RenderCalls);
        Assert.False(vm.IsSymbolizedPreviewStale);
        Assert.Equal(RenderWorkflowStatus.RenderReady, vm.WorkflowStatus);
    }

    [Fact]
    public async Task DisposalDuringDirectExportCleanup_DoesNotRunDeferredRecovery()
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.Create("dispose-review.mp4"), job.Identity)));
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("source.mp4")]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        TaskCompletionSource started = NewCompletion();
        TaskCompletionSource finished = NewCompletion();
        harness.SaveDialogs.VideoOutputPath = files.PathFor("direct-output.mp4");
        harness.VideoPublications.Handler = async (_, _, _) => { started.SetResult(); await finished.Task; return VideoExportResult.Cancelled(); };
        Task export = vm.ExportVideoCommand.ExecuteAsync(null);
        await started.Task;
        vm.UnloadReviewView();
        Task load = vm.LoadReviewViewAsync(null);
        vm.Dispose();
        finished.SetResult();
        await Task.WhenAll(export, load);
        Assert.Equal(1, harness.RenderedVideoPlayer.OpenCalls);
        Assert.False(harness.RenderedVideoPlayer.IsOpen);
        Assert.Null(harness.RenderedVideoPlayer.SourcePath);
    }

    [Fact]
    public async Task MediaReplacementDuringPostExportRecovery_RejectsLateReopenAndUiUpdates()
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.Create("obsolete-review.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("source.mp4")]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        TaskCompletionSource started = NewCompletion();
        TaskCompletionSource cleanup = NewCompletion();
        TaskCompletionSource opening = NewCompletion();
        TaskCompletionSource<RenderedVideoOpenResult> opened = NewCompletion<RenderedVideoOpenResult>();
        harness.SaveDialogs.VideoOutputPath = files.PathFor("direct-output.mp4");
        harness.VideoPublications.Handler = async (_, _, _) => { started.SetResult(); await cleanup.Task; return VideoExportResult.Cancelled(); };
        Task export = vm.ExportVideoCommand.ExecuteAsync(null);
        await started.Task;
        vm.UnloadReviewView();
        await vm.LoadReviewViewAsync(null);
        harness.RenderedVideoPlayer.Handler = (_, _) => { opening.SetResult(); return opened.Task; };
        cleanup.SetResult();
        await opening.Task;
        string nextMedia = files.Create("new-media.mp4");
        await vm.DropMediaCommand.ExecuteAsync([nextMedia]);
        int closes = harness.RenderedVideoPlayer.CloseCalls;
        List<string?> changes = [];
        vm.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        opened.SetResult(RenderedVideoOpenResult.Opened());
        await export;
        Assert.Empty(changes);
        Assert.Equal(closes, harness.RenderedVideoPlayer.CloseCalls);
        Assert.Equal(nextMedia, vm.VideoMetadata?.FilePath);
        Assert.Null(harness.ManagedArtifacts.Current);
        Assert.False(harness.RenderedVideoPlayer.IsOpen);
        Assert.False(vm.HasVideoArtifact);
        Assert.False(vm.ShowVideoResultPanel);
    }

    [Theory]
    [InlineData("Fit")]
    [InlineData("Fill")]
    [InlineData("100%")]
    public async Task ReviewScaling_UsesArtifactDimensions_WithoutInvalidatingOrRerendering(string mode)
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.PathFor("portrait-review.mp4"), job.Identity)
                with
            { OutputDimensions = new PixelDimensions(240, 320) }));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("source.mp4")]);
        Assert.Equal(2, vm.CurrentPreviewPixelWidth); // Owned decoded frame, not encoded metadata.
        Assert.Equal(1, vm.CurrentPreviewPixelHeight);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        ManagedVideoArtifact artifact = harness.ManagedArtifacts.Current!;
        vm.SelectedFitMode = mode;
        Assert.Equal(240, vm.RenderedVideoPixelWidth);
        Assert.Equal(320, vm.RenderedVideoPixelHeight);
        Assert.Same(artifact, harness.ManagedArtifacts.Current);
        Assert.Equal(RenderWorkflowStatus.RenderReady, vm.WorkflowStatus);
        Assert.Equal("Video Ready", vm.VideoResultTitle);
        Assert.Equal(1, harness.ManagedVideoRenders.Calls);
        Assert.Equal(1, harness.RenderedVideoPlayer.OpenCalls);
        Assert.Equal(0, harness.VideoPublications.Calls);
        Assert.Equal(0, harness.ImagePipeline.RenderCalls);
    }

    [Fact]
    public async Task PlayerPresentation_TracksPlayPauseMuteReplay_WithoutStartingMediaProcessing()
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.PathFor("review.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("source.mp4")]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        List<string?> changes = [];
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        Assert.Equal("Play", vm.RenderedVideoPlaybackActionLabel);
        Assert.False(vm.ShowRenderedVideoReplay);
        vm.ToggleRenderedVideoPlaybackCommand.Execute(null);
        Assert.True(vm.IsRenderedVideoPlaying);
        Assert.Equal("Pause", vm.RenderedVideoPlaybackActionLabel);
        vm.ToggleRenderedVideoPlaybackCommand.Execute(null);
        Assert.False(vm.ShowRenderedVideoReplay);
        vm.ToggleRenderedVideoMuteCommand.Execute(null);
        Assert.True(vm.IsRenderedVideoMuted);
        Assert.Equal("Unmute", vm.RenderedVideoMuteActionLabel);
        vm.RenderedVideoVolume = 0.3;
        Assert.True(vm.IsRenderedVideoMuted);
        vm.ToggleRenderedVideoPlaybackCommand.Execute(null);
        harness.RenderedVideoPlayer.EndPlayback();
        Assert.True(vm.ShowRenderedVideoReplay);
        Assert.Equal("Restart", vm.RenderedVideoPlaybackActionLabel);
        vm.RenderedVideoPositionSeconds = 0.25;
        Assert.False(vm.ShowRenderedVideoReplay);
        Assert.False(vm.IsRenderedVideoPlaying);
        Assert.Contains(nameof(vm.ShowRenderedVideoReplay), changes);
        Assert.Contains(nameof(vm.IsRenderedVideoPlaying), changes);
        Assert.Contains(nameof(vm.IsRenderedVideoMuted), changes);
        Assert.Contains(nameof(vm.RenderedVideoCurrentTimeText), changes);
        Assert.Equal(1, harness.ManagedVideoRenders.Calls);
        Assert.Equal(0, harness.VideoPublications.Calls);
        Assert.Equal(0, harness.ImagePipeline.RenderCalls);
    }

    [Fact]
    public async Task ArtifactPublication_RepeatedExportsBorrowExactAcceptedArtifactWithoutRendering()
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.Create("review.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("source.mp4")]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        ManagedVideoArtifact retained = harness.ManagedArtifacts.Current!;
        for (int index = 0; index < 2; index++)
        {
            harness.SaveDialogs.VideoOutputPath = files.PathFor($"export-{index}.mp4");
            vm.RenderedVideoPositionSeconds = index;
            vm.RenderedVideoVolume = 0.2;
            vm.SelectedFitMode = "100%";
            await vm.ExportVideoCommand.ExecuteAsync(null);
            Assert.Same(retained, harness.VideoPublications.LastJob!.Artifact);
            Assert.Same(retained, harness.ManagedArtifacts.Current);
            Assert.Equal(harness.SaveDialogs.VideoOutputPath, vm.VideoExportDestinationText);
            Assert.True(vm.CanOpenExportFolder);
            Assert.Equal("Export Again", vm.VideoExportActionLabel);
            Assert.Equal("Video Exported", vm.VideoResultTitle);
            Assert.True(vm.ShowVideoResultPanel);
            Assert.True(harness.RenderedVideoPlayer.IsOpen);
        }
        Assert.Equal(1, harness.ManagedVideoRenders.Calls);
        Assert.Equal(0, harness.ImagePipeline.RenderCalls);
        Assert.Equal(2, harness.VideoPublications.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ArtifactPublication_MissingOrStaleRenderCannotExport(bool stale)
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.Create("review.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("source.mp4")]);
        if (stale)
        {
            await vm.RenderVideoCommand.ExecuteAsync(null);
            vm.SelectedFontSize = 12;
            Assert.True(vm.ShowRenderedVideoPlayer);
            Assert.Contains("before exporting", vm.VideoResultSummary);
        }
        Assert.False(vm.ExportVideoCommand.CanExecute(null));
        await vm.ExportVideoCommand.ExecuteAsync(null);
        Assert.Equal(0, harness.SaveDialogs.VideoCalls);
        Assert.Equal(0, harness.VideoPublications.Calls);
    }

    [Fact]
    public async Task ArtifactPublication_CancelledDialogPreservesPreviousExportOutcome()
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.Create("review.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("source.mp4")]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        harness.SaveDialogs.VideoOutputPath = files.PathFor("first.mp4");
        await vm.ExportVideoCommand.ExecuteAsync(null);
        string message = vm.StatusMessage;
        harness.SaveDialogs.VideoOutputPath = null;
        await vm.ExportVideoCommand.ExecuteAsync(null);
        Assert.Equal(RenderWorkflowStatus.Exported, vm.WorkflowStatus);
        Assert.Equal(message, vm.StatusMessage);
        Assert.Equal(files.PathFor("first.mp4"), vm.VideoExportDestinationText);
        Assert.Equal(1, harness.VideoPublications.Calls);
    }

    [Theory]
    [InlineData("dialog")]
    [InlineData("preparation")]
    public async Task ArtifactPublication_IdentityChangeBeforeCopyRejectsCapturedArtifact(string boundary)
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.Create("review.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("source.mp4")]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        ManagedVideoArtifact retained = harness.ManagedArtifacts.Current!;
        harness.SaveDialogs.VideoOutputPath = files.PathFor("output.mp4");
        if (boundary == "dialog") harness.SaveDialogs.OnVideoDialog = () => vm.SelectedFontSize = 12;
        else harness.VideoPublications.Preparing = () => vm.SelectedFontSize = 12;
        await vm.ExportVideoCommand.ExecuteAsync(null);
        Assert.Same(retained, harness.ManagedArtifacts.Current);
        Assert.Equal(0, harness.VideoPublications.Calls);
        Assert.True(vm.IsSymbolizedPreviewStale);
        Assert.False(vm.IsExportActive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ArtifactPublication_SettingsInvalidationCancelsBeforeCommit_ButCommittedSuccessIsTruthful(bool committed)
    {
        using TestMediaFiles files = new();
        TestHarness harness = ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.Create("review.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("source.mp4")]);
        await vm.RenderVideoCommand.ExecuteAsync(null);
        TaskCompletionSource started = NewCompletion();
        TaskCompletionSource release = NewCompletion();
        CancellationToken active = default;
        harness.SaveDialogs.VideoOutputPath = files.PathFor("output.mp4");
        harness.VideoPublications.Handler = async (job, token, _) =>
        {
            active = token;
            started.SetResult();
            await release.Task;
            return committed ? VideoExportResult.Completed(job.Destination.FilePath, 100, new(64, 48), 10)
                : VideoExportResult.Cancelled();
        };
        Task export = vm.ExportVideoCommand.ExecuteAsync(null);
        await started.Task;
        vm.SelectedFontSize = 12;
        Assert.True(active.IsCancellationRequested);
        Assert.False(vm.ExportVideoCommand.CanExecute(null));
        release.SetResult();
        await export;
        Assert.True(vm.IsSymbolizedPreviewStale);
        Assert.NotNull(harness.ManagedArtifacts.Current);
        Assert.Equal(committed ? RenderWorkflowStatus.Exported : RenderWorkflowStatus.Cancelled, vm.WorkflowStatus);
        Assert.Equal(committed, vm.HasVideoExportDestination);
    }

    [Fact]
    public async Task AcceptedVideo_OpensOriginalPlaybackAndShowsContinuousPlayerWhenReady()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("original-ready.mp4");
        TestHarness harness = ReadyVideoHarness();
        harness.OriginalVideoPlayer.Handler = (_, _) => Task.FromResult(OriginalReady());
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await WaitUntilAsync(viewModel, () => viewModel.IsOriginalVideoPlaybackReady);

        Assert.Equal(videoPath, harness.OriginalVideoPlayer.SourcePath);
        Assert.True(viewModel.ShowOriginalVideoPlayer);
        Assert.False(viewModel.ShowVideoFrameControls);
        Assert.False(viewModel.ShowOriginalPlaybackFallback);
        Assert.True(viewModel.ToggleOriginalVideoPlaybackCommand.CanExecute(null));
        Assert.True(viewModel.PreviewFrameCommand.CanExecute(null));
    }

    [Fact]
    public async Task ReadyOriginalPlayer_SuppressesPlaceholderWithoutDecodedFrameAndNotifiesBinding()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("original-ready-without-frame.mp4");
        TestHarness harness = ReadyVideoHarness();
        harness.VideoFrames.Handler = (_, position, _, _) =>
            Task.FromResult(VideoFrameReadResult.Cancelled(position));
        harness.OriginalVideoPlayer.Handler = (_, _) => Task.FromResult(OriginalReady());
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        List<string?> changes = [];
        viewModel.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await WaitUntilAsync(viewModel, () => viewModel.IsOriginalVideoPlaybackReady);

        Assert.False(viewModel.HasVideoFrame);
        Assert.False(viewModel.HasVideoFrameError);
        Assert.True(viewModel.ShowOriginalVideoPlayer);
        Assert.False(viewModel.ShowMediaPlaceholder);
        Assert.Contains(nameof(MainWindowViewModel.ShowMediaPlaceholder), changes);

        changes.Clear();
        harness.OriginalVideoPlayer.FailPlayback();

        Assert.True(viewModel.ShowMediaPlaceholder);
        Assert.Contains(nameof(MainWindowViewModel.ShowMediaPlaceholder), changes);
    }

    [Fact]
    public async Task ReadyRenderedPlayer_SuppressesPlaceholderWithoutDecodedFrameAndNotifiesBinding()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("rendered-ready-without-frame.mp4");
        TestHarness harness = ReadyVideoHarness();
        harness.VideoFrames.Handler = (_, position, _, _) =>
            Task.FromResult(VideoFrameReadResult.Cancelled(position));
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.Create("managed-review.mp4"), job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await WaitUntilAsync(viewModel, () => viewModel.IsOriginalVideoPlaybackFallback);
        List<string?> changes = [];
        viewModel.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        await viewModel.RenderVideoCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasVideoFrame);
        Assert.True(viewModel.ShowRenderedVideoPlayer);
        Assert.False(viewModel.ShowMediaPlaceholder);
        Assert.Contains(nameof(MainWindowViewModel.ShowMediaPlaceholder), changes);

        changes.Clear();
        viewModel.SelectOriginalCommand.Execute(null);

        Assert.True(viewModel.ShowMediaPlaceholder);
        Assert.Contains(nameof(MainWindowViewModel.ShowMediaPlaceholder), changes);
    }

    [Fact]
    public async Task PlaybackFallbackWithoutDecodedFrame_ShowsPlaceholder()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("fallback-without-frame.mkv");
        TestHarness harness = ReadyVideoHarness();
        harness.VideoFrames.Handler = (_, position, _, _) =>
            Task.FromResult(VideoFrameReadResult.Cancelled(position));
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await WaitUntilAsync(viewModel, () => viewModel.IsOriginalVideoPlaybackFallback);

        Assert.False(viewModel.HasVideoFrame);
        Assert.False(viewModel.HasVideoFrameError);
        Assert.False(viewModel.ShowOriginalVideoPlayer);
        Assert.False(viewModel.ShowRenderedVideoPlayer);
        Assert.True(viewModel.ShowMediaPlaceholder);
    }

    [Fact]
    public async Task AcceptedVideo_ReportsOriginalOpeningWithoutBlankingDecodedFrame()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("original-opening.mp4");
        TestHarness harness = ReadyVideoHarness();
        TaskCompletionSource<OriginalVideoOpenResult> opening = NewCompletion<OriginalVideoOpenResult>();
        harness.OriginalVideoPlayer.Handler = (_, _) => opening.Task;
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await WaitUntilAsync(viewModel, () => viewModel.IsOriginalVideoPlaybackOpening);

        Assert.True(viewModel.ShowOriginalPlaybackOpening);
        Assert.True(viewModel.ShowImagePreview);
        Assert.True(viewModel.ShowVideoFrameControls);
        Assert.False(viewModel.ToggleOriginalVideoPlaybackCommand.CanExecute(null));
        opening.SetResult(OriginalReady());
        await WaitUntilAsync(viewModel, () => viewModel.IsOriginalVideoPlaybackReady);
    }

    [Fact]
    public async Task OriginalPlaybackFallback_PreservesFfmpegFrameAndProcessingCommands()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("original-fallback.mkv");
        TestHarness harness = ReadyVideoHarness();
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await WaitUntilAsync(viewModel, () => viewModel.IsOriginalVideoPlaybackFallback);

        Assert.True(viewModel.ShowOriginalPlaybackFallback);
        Assert.True(viewModel.ShowImagePreview);
        Assert.True(viewModel.ShowVideoFrameControls);
        Assert.False(viewModel.ShowOriginalVideoPlayer);
        Assert.False(viewModel.ToggleOriginalVideoPlaybackCommand.CanExecute(null));
        Assert.True(viewModel.PreviewFrameCommand.CanExecute(null));
        Assert.True(viewModel.RenderVideoCommand.CanExecute(null));
        Assert.Contains("Frame preview and rendering remain available", viewModel.OriginalVideoPlaybackLimitationMessage);
    }

    [Fact]
    public async Task OriginalPlaybackFailureAfterReadiness_FallsBackWithoutInvalidatingMedia()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("original-runtime-failure.mp4");
        TestHarness harness = ReadyVideoHarness();
        harness.OriginalVideoPlayer.Handler = (_, _) => Task.FromResult(OriginalReady());
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await WaitUntilAsync(viewModel, () => viewModel.IsOriginalVideoPlaybackReady);

        harness.OriginalVideoPlayer.FailPlayback();

        Assert.True(viewModel.IsVideoSelected);
        Assert.Equal(videoPath, viewModel.VideoMetadata?.FilePath);
        Assert.True(viewModel.ShowOriginalPlaybackFallback);
        Assert.True(viewModel.ShowVideoFrameControls);
        Assert.True(viewModel.PreviewFrameCommand.CanExecute(null));
        Assert.True(viewModel.RenderVideoCommand.CanExecute(null));
    }

    [Fact]
    public async Task FailedVideoReplacement_PreservesAcceptedOriginalPlayback()
    {
        using TestMediaFiles files = new();
        string firstPath = files.Create("accepted-original.mp4");
        string failedPath = files.Create("failed-original.avi");
        TestHarness harness = ReadyVideoHarness();
        harness.OriginalVideoPlayer.Handler = (_, _) => Task.FromResult(OriginalReady());
        harness.MediaInput.Handler = (path, _) => Task.FromResult(
            path == firstPath
                ? VideoResult(path)
                : MediaInputResult.Failure(MediaInputErrorCategory.MetadataUnavailable, "Video metadata could not be read."));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([firstPath]);
        await WaitUntilAsync(viewModel, () => viewModel.IsOriginalVideoPlaybackReady);
        int closeCalls = harness.OriginalVideoPlayer.CloseCalls;

        await viewModel.DropMediaCommand.ExecuteAsync([failedPath]);

        Assert.Equal(firstPath, viewModel.VideoMetadata?.FilePath);
        Assert.Equal(firstPath, harness.OriginalVideoPlayer.SourcePath);
        Assert.True(viewModel.IsOriginalVideoPlaybackReady);
        Assert.Equal(closeCalls, harness.OriginalVideoPlayer.CloseCalls);
    }

    [Fact]
    public async Task AcceptedImage_ClosesOriginalPlaybackAfterSelectionSucceeds()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("original-before-image.mp4");
        string imagePath = files.Create("replacement.png");
        TestHarness harness = ReadyVideoHarness();
        harness.OriginalVideoPlayer.Handler = (_, _) => Task.FromResult(OriginalReady());
        harness.MediaInput.Handler = (path, _) => Task.FromResult(
            path == videoPath ? VideoResult(path) : ImageResult(path!));
        harness.ImagePipeline.DecodeResult = SolidFrame(5, 10, 15);
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await WaitUntilAsync(viewModel, () => viewModel.IsOriginalVideoPlaybackReady);

        await viewModel.DropMediaCommand.ExecuteAsync([imagePath]);

        Assert.True(viewModel.IsImageSelected);
        Assert.Null(harness.OriginalVideoPlayer.SourcePath);
        Assert.False(harness.OriginalVideoPlayer.IsPlaybackReady);
    }

    [Fact]
    public async Task RapidVideoReplacement_RejectsObsoleteOriginalOpenCompletion()
    {
        using TestMediaFiles files = new();
        string firstPath = files.Create("first-original.mp4");
        string secondPath = files.Create("second-original.mp4");
        TestHarness harness = ReadyVideoHarness();
        TaskCompletionSource<OriginalVideoOpenResult> firstOpen = NewCompletion<OriginalVideoOpenResult>();
        harness.OriginalVideoPlayer.Handler = (path, _) => path == firstPath
            ? firstOpen.Task
            : Task.FromResult(OriginalReady());
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        await viewModel.DropMediaCommand.ExecuteAsync([firstPath]);
        await WaitUntilAsync(viewModel, () => harness.OriginalVideoPlayer.SourcePath == firstPath);
        await viewModel.DropMediaCommand.ExecuteAsync([secondPath]);
        await WaitUntilAsync(viewModel, () => viewModel.IsOriginalVideoPlaybackReady);
        firstOpen.SetResult(OriginalReady());
        await Task.Yield();

        Assert.Equal(secondPath, viewModel.VideoMetadata?.FilePath);
        Assert.Equal(secondPath, harness.OriginalVideoPlayer.SourcePath);
        Assert.True(viewModel.IsOriginalVideoPlaybackReady);
    }

    [Fact]
    public async Task PreviewFrame_WhenOriginalPlaybackReady_UsesPausedPlaybackPosition()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("preview-from-playback.mp4");
        TestHarness harness = ReadyVideoHarness();
        harness.OriginalVideoPlayer.Handler = (_, _) => Task.FromResult(OriginalReady());
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await WaitUntilAsync(viewModel, () => viewModel.IsOriginalVideoPlaybackReady);
        DecodedImage retainedFrame = harness.ImageSources.LastDecodedImage!;
        DecodedImage extractedFrame = SolidFrame(210, 120, 30);
        harness.VideoFrames.Handler = (_, position, metadata, _) =>
            Task.FromResult(FrameResult(extractedFrame, position, metadata));
        viewModel.OriginalVideoPositionSeconds = 7d;
        viewModel.ToggleOriginalVideoPlaybackCommand.Execute(null);
        int pauseCalls = harness.OriginalVideoPlayer.PauseCalls;

        await viewModel.PreviewFrameCommand.ExecuteAsync(null);

        Assert.True(harness.OriginalVideoPlayer.PauseCalls > pauseCalls);
        Assert.Equal(0.7d, harness.VideoFrames.LastNormalizedPosition, 3);
        Assert.False(harness.OriginalVideoPlayer.IsPlaying);
        Assert.Same(extractedFrame, harness.ImagePipeline.LastRenderedSource);
        Assert.NotSame(retainedFrame, harness.ImagePipeline.LastRenderedSource);
        Assert.Equal(PreviewMode.Symbolized, viewModel.PreviewMode);
    }

    [Fact]
    public async Task PreviewFrame_FailedCurrentExtraction_DoesNotRenderOrRelabelRetainedFrame()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("failed-current-preview.mp4");
        TestHarness harness = ReadyVideoHarness();
        harness.OriginalVideoPlayer.Handler = (_, _) => Task.FromResult(OriginalReady());
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await WaitUntilAsync(viewModel, () => viewModel.IsOriginalVideoPlaybackReady);
        viewModel.OriginalVideoPositionSeconds = 5d;
        await viewModel.PreviewFrameCommand.ExecuteAsync(null);
        DecodedImage retainedFrame = harness.ImageSources.LastDecodedImage!;
        ImageSource? retainedSymbolized = viewModel.SymbolizedImageSource;
        int renderCalls = harness.ImagePipeline.RenderCalls;
        harness.VideoFrames.Handler = (_, position, _, _) => Task.FromResult(
            VideoFrameReadResult.Failure(
                VideoFrameReadStatus.Failed,
                VideoFrameReadErrorCategory.DecodeFailed,
                position,
                "The requested frame could not be decoded.",
                "controlled decoder failure"));
        viewModel.OriginalVideoPositionSeconds = 7d;

        await viewModel.PreviewFrameCommand.ExecuteAsync(null);

        Assert.Equal(renderCalls, harness.ImagePipeline.RenderCalls);
        Assert.Same(retainedFrame, harness.ImageSources.LastDecodedImage);
        Assert.Same(retainedFrame, harness.ImagePipeline.LastRenderedSource);
        Assert.Same(retainedSymbolized, viewModel.SymbolizedImageSource);
        Assert.Equal(0.5d, viewModel.TimelinePosition, 3);
        Assert.True(viewModel.HasVideoFrameError);
        Assert.Equal("The requested frame could not be decoded.", viewModel.VideoFrameStatusMessage);
        Assert.Equal("controlled decoder failure", viewModel.VideoFrameTechnicalDetail);
        Assert.Equal("The requested frame could not be decoded.", viewModel.StatusMessage);
        Assert.False(viewModel.IsVideoFrameLoading);
        Assert.True(viewModel.PreviewFrameCommand.CanExecute(null));
    }

    [Fact]
    public async Task PreviewFrame_CancelledCurrentExtraction_DoesNotRenderRetainedFrame()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("cancelled-current-preview.mp4");
        TestHarness harness = ReadyVideoHarness();
        harness.OriginalVideoPlayer.Handler = (_, _) => Task.FromResult(OriginalReady());
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await WaitUntilAsync(viewModel, () => viewModel.IsOriginalVideoPlaybackReady);
        viewModel.OriginalVideoPositionSeconds = 5d;
        await viewModel.PreviewFrameCommand.ExecuteAsync(null);
        DecodedImage retainedFrame = harness.ImageSources.LastDecodedImage!;
        ImageSource? retainedSymbolized = viewModel.SymbolizedImageSource;
        int renderCalls = harness.ImagePipeline.RenderCalls;
        harness.VideoFrames.Handler = (_, position, _, _) =>
            Task.FromResult(VideoFrameReadResult.Cancelled(position));
        viewModel.OriginalVideoPositionSeconds = 7d;
        viewModel.ToggleOriginalVideoPlaybackCommand.Execute(null);

        await viewModel.PreviewFrameCommand.ExecuteAsync(null);

        Assert.Equal(renderCalls, harness.ImagePipeline.RenderCalls);
        Assert.Same(retainedFrame, harness.ImageSources.LastDecodedImage);
        Assert.Same(retainedFrame, harness.ImagePipeline.LastRenderedSource);
        Assert.Same(retainedSymbolized, viewModel.SymbolizedImageSource);
        Assert.True(viewModel.HasSymbolizedPreview);
        Assert.False(viewModel.IsSymbolizedPreviewStale);
        Assert.Equal(0.5d, viewModel.TimelinePosition, 3);
        Assert.False(viewModel.HasVideoFrameError);
        Assert.False(viewModel.IsVideoFrameLoading);
        Assert.True(viewModel.PreviewFrameCommand.CanExecute(null));
        Assert.False(harness.OriginalVideoPlayer.IsPlaying);
        Assert.Equal(PreviewMode.Original, viewModel.PreviewMode);
        Assert.Equal(
            "Frame preview was cancelled. The previous original frame remains available.",
            viewModel.StatusMessage);

        int frameReadCalls = harness.VideoFrames.Calls;
        harness.OriginalVideoPlayer.FailPlayback();
        await viewModel.PreviewFrameCommand.ExecuteAsync(null);

        Assert.Equal(frameReadCalls, harness.VideoFrames.Calls);
        Assert.Equal(renderCalls + 1, harness.ImagePipeline.RenderCalls);
        Assert.Same(retainedFrame, harness.ImagePipeline.LastRenderedSource);
        Assert.Equal(0.5d, viewModel.TimelinePosition, 3);
        Assert.Equal(PreviewMode.Symbolized, viewModel.PreviewMode);
        Assert.False(viewModel.IsSymbolizedPreviewStale);
    }

    [Fact]
    public async Task PlayerCoordination_PausesOutgoingAndNeverAutoResumesIncomingPlayer()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("coordinated-playback.mp4");
        TestHarness harness = ReadyVideoHarness();
        harness.OriginalVideoPlayer.Handler = (_, _) => Task.FromResult(OriginalReady());
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.PathFor("review.mp4"), job.Identity)));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await WaitUntilAsync(viewModel, () => viewModel.IsOriginalVideoPlaybackReady);
        viewModel.ToggleOriginalVideoPlaybackCommand.Execute(null);
        Assert.True(harness.OriginalVideoPlayer.IsPlaying);

        await viewModel.RenderVideoCommand.ExecuteAsync(null);
        Assert.False(harness.OriginalVideoPlayer.IsPlaying);
        viewModel.ToggleRenderedVideoPlaybackCommand.Execute(null);
        Assert.True(harness.RenderedVideoPlayer.IsPlaying);
        Assert.False(harness.OriginalVideoPlayer.IsPlaying);

        viewModel.SelectOriginalCommand.Execute(null);

        Assert.False(harness.RenderedVideoPlayer.IsPlaying);
        Assert.False(harness.OriginalVideoPlayer.IsPlaying);
        viewModel.ToggleOriginalVideoPlaybackCommand.Execute(null);
        Assert.True(harness.OriginalVideoPlayer.IsPlaying);
        Assert.False(harness.RenderedVideoPlayer.IsPlaying);
    }

    [Theory]
    [InlineData(0, 0, 64, 48)]
    [InlineData(90, 90, 48, 64)]
    [InlineData(180, 180, 64, 48)]
    [InlineData(270, 270, 48, 64)]
    [InlineData(-90, 270, 48, 64)]
    [InlineData(450, 90, 48, 64)]
    [InlineData(45, 0, 64, 48)]
    public async Task OriginalPlayback_UsesTrustedQuarterTurnAndDisplayDimensions(
        int metadataRotation,
        int expectedRotation,
        int expectedWidth,
        int expectedHeight)
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create($"rotation-{metadataRotation}.mov");
        TestHarness harness = ReadyVideoHarness();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(MediaInputResult.ForVideo(
            VideoMetadataFor(path!) with { RotationDegrees = metadataRotation }));
        harness.OriginalVideoPlayer.Handler = (_, _) => Task.FromResult(OriginalReady());
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await WaitUntilAsync(viewModel, () => viewModel.IsOriginalVideoPlaybackReady);

        Assert.Equal(expectedRotation, viewModel.OriginalVideoRotationDegrees);
        Assert.Equal(expectedWidth, viewModel.OriginalVideoPixelWidth);
        Assert.Equal(expectedHeight, viewModel.OriginalVideoPixelHeight);
    }

    [Fact]
    public async Task AudioModeChanges_DoNotMutateOriginalPlaybackAudioState()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("independent-source-audio.mp4");
        TestHarness harness = ReadyVideoHarness();
        harness.OriginalVideoPlayer.Handler = (_, _) => Task.FromResult(OriginalReady());
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await WaitUntilAsync(viewModel, () => viewModel.IsOriginalVideoPlaybackReady);
        viewModel.OriginalVideoVolume = 0.42d;
        viewModel.ToggleOriginalVideoMuteCommand.Execute(null);

        viewModel.SelectedAudioMode = AudioExportMode.RemoveAudio;

        Assert.Equal(videoPath, harness.OriginalVideoPlayer.SourcePath);
        Assert.Equal(0.42d, harness.OriginalVideoPlayer.Volume, 3);
        Assert.True(harness.OriginalVideoPlayer.IsMuted);
        Assert.True(viewModel.IsOriginalVideoPlaybackReady);
    }

    [Fact]
    public async Task OriginalCommands_AreDisabledAndPlaybackPausesDuringManagedRender()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("original-operation-lock.mp4");
        TestHarness harness = ReadyVideoHarness();
        harness.OriginalVideoPlayer.Handler = (_, _) => Task.FromResult(OriginalReady());
        TaskCompletionSource<ManagedVideoRenderResult> pending = NewCompletion<ManagedVideoRenderResult>();
        harness.ManagedVideoRenders.Handler = (_, _, _) => pending.Task;
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await WaitUntilAsync(viewModel, () => viewModel.IsOriginalVideoPlaybackReady);
        viewModel.ToggleOriginalVideoPlaybackCommand.Execute(null);

        Task render = viewModel.RenderVideoCommand.ExecuteAsync(null);

        Assert.False(harness.OriginalVideoPlayer.IsPlaying);
        Assert.False(viewModel.ToggleOriginalVideoPlaybackCommand.CanExecute(null));
        Assert.False(viewModel.OriginalFullscreenCommand.CanExecute(null));
        pending.SetResult(ManagedVideoRenderResult.Failure(
            ManagedVideoArtifactErrorCategory.RenderFailed,
            "Controlled render failure."));
        await render;
        Assert.True(viewModel.ToggleOriginalVideoPlaybackCommand.CanExecute(null));
    }

    [Fact]
    public async Task OriginalFullscreenCommand_ForwardsFitModeAndDisposeClosesTransport()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("original-fullscreen.mp4");
        TestHarness harness = ReadyVideoHarness();
        harness.OriginalVideoPlayer.Handler = (_, _) => Task.FromResult(OriginalReady());
        MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await WaitUntilAsync(viewModel, () => viewModel.IsOriginalVideoPlaybackReady);
        viewModel.SelectedFitMode = "Fill";

        viewModel.OriginalFullscreenCommand.Execute(null);

        Assert.Equal(1, harness.OriginalVideoPlayer.FullscreenCalls);
        Assert.Equal("Fill", harness.OriginalVideoPlayer.LastFullscreenFitMode);
        viewModel.Dispose();
        Assert.Null(harness.OriginalVideoPlayer.SourcePath);
        Assert.False(harness.OriginalVideoPlayer.IsPlaybackReady);
    }

    private static OriginalVideoOpenResult OriginalReady() => new(
        OriginalVideoOpenOutcome.Ready,
        "The original video is ready for playback.",
        null);

    private static TestHarness ReadyVideoHarness()
    {
        TestHarness harness = new();
        DecodedImage frame = SolidFrame(100, 120, 140);
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.VideoFrames.Handler = (_, position, metadata, _) =>
            Task.FromResult(FrameResult(frame, position, metadata));
        return harness;
    }

    private static TestHarness ReadyImageHarness()
    {
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(ImageResult(path!));
        harness.ImagePipeline.DecodeResult = SolidFrame(25, 50, 75);
        return harness;
    }

    private static MediaInputResult VideoResult(string path) => MediaInputResult.ForVideo(VideoMetadataFor(path));

    private static VideoMetadata VideoMetadataFor(string path) => new(
        path,
        Path.GetFileName(path),
        64,
        48,
        TimeSpan.FromSeconds(10),
        10,
        100,
        "MP4",
        new FileInfo(path).Length,
        "h264",
        false,
        null,
        null);

    private static ManagedVideoArtifact Artifact(
        string path,
        FullVideoRenderIdentity identity,
        Guid? artifactId = null) => new(
            artifactId ?? Guid.NewGuid(),
            path,
            identity,
            new PixelDimensions(64, 48),
            10,
            100,
            TimeSpan.FromSeconds(10),
            identity.AudioMode,
            identity.AudioMode == AudioExportMode.RemoveAudio
                ? AudioMuxOutcome.Removed
                : AudioMuxOutcome.NoSourceAudio,
            4096);

    private static MediaInputResult ImageResult(string path) => MediaInputResult.ForImage(new ImageMetadata(
        path,
        Path.GetFileName(path),
        2,
        1,
        2,
        1,
        "PNG",
        new FileInfo(path).Length,
        ImageOrientation.Normal,
        null));

    private static VideoFrameReadResult FrameResult(
        DecodedImage frame,
        double position,
        VideoMetadata? metadata,
        double? actualPosition = null) => VideoFrameReadResult.ForFrame(
            frame,
            position,
            actualPosition ?? position,
            metadata?.Duration is TimeSpan duration ? TimeSpan.FromTicks((long)(duration.Ticks * position)) : null,
            metadata?.Duration is TimeSpan actualDuration ? TimeSpan.FromTicks((long)(actualDuration.Ticks * (actualPosition ?? position))) : null,
            metadata?.FrameCount is long count ? (long)Math.Round((count - 1) * (actualPosition ?? position)) : null,
            metadata?.Duration,
            metadata?.FrameCount);

    private static DecodedImage SolidFrame(byte red, byte green, byte blue) => new(
        2,
        1,
        [red, green, blue, 255, red, green, blue, 255]);

    private static ImagePipelineResult RenderResult() => new(
        new RenderedImage(2, 1, [10, 20, 30, 255, 10, 20, 30, 255]),
        new SymbolGrid(1, 1, new PixelDimensions(2, 1)),
        new GlyphMetrics("Cascadia Mono", "Cascadia Mono", 10, 5, 10, 8, false));

    private static ImagePipelineResult RenderResult(RenderedImage image) => new(
        image,
        new SymbolGrid(1, 1, image.Dimensions),
        new GlyphMetrics("Cascadia Mono", "Cascadia Mono", 10, 5, 10, 8, false));

    private static VideoExportProgress ExportProgress(
        VideoExportStage stage,
        long processedFrames,
        long? totalFrames,
        double? percentage,
        TimeSpan elapsed,
        TimeSpan? estimatedRemaining,
        string description) => new(
            stage,
            processedFrames,
            totalFrames,
            percentage,
            elapsed,
            estimatedRemaining,
            percentage is null,
            description);

    private static TaskCompletionSource NewCompletion() => new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    private static TaskCompletionSource<T> NewCompletion<T>() => new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task WaitUntilAsync(MainWindowViewModel viewModel, Func<bool> predicate)
    {
        if (predicate())
        {
            return;
        }

        TaskCompletionSource<bool> completed = NewCompletion<bool>();
        PropertyChangedEventHandler? handler = null;
        handler = (_, _) =>
        {
            if (predicate())
            {
                viewModel.PropertyChanged -= handler;
                completed.TrySetResult(true);
            }
        };
        viewModel.PropertyChanged += handler;
        try
        {
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally
        {
            viewModel.PropertyChanged -= handler;
        }
    }

    private sealed class TestHarness
    {
        public FakeMediaInputService MediaInput { get; } = new();

        public FakeVideoFrameReader VideoFrames { get; } = new();

        public FakeImagePipelineService ImagePipeline { get; } = new();

        public FakeImageSourceFactory ImageSources { get; } = new();

        public FakeVideoArtifactPublisher VideoPublications { get; } = new();

        public FakeManagedVideoRenderService ManagedVideoRenders { get; } = new();

        public FakeManagedVideoArtifactSession ManagedArtifacts { get; } = new();

        public FakeRenderedVideoPlayer RenderedVideoPlayer { get; } = new();

        public FakeOriginalVideoPlayer OriginalVideoPlayer { get; } = new();

        public FakeBundledFfmpegLocator FfmpegLocator { get; } = new();

        public FakeImageSaveDialogService SaveDialogs { get; } = new();

        public FakeImageExporter ImageExporter { get; } = new();

        public FakeFolderLauncher FolderLauncher { get; } = new();

        public FakeUserDialogService UserDialogs { get; } = new();

        public FakeExportStatusPresentationService ExportStatusPresentation { get; } = new();

        public MainWindowViewModel CreateViewModel(
            IVideoFrameReader? videoFrameReader = null,
            bool allowNull = false,
            IExportStatusPresentationService? exportStatusPresentationService = null,
            bool allowNullExportStatusPresentationService = false,
            IFolderLauncher? folderLauncher = null,
            bool allowNullFolderLauncher = false,
            IManagedVideoArtifactSession? managedVideoArtifactSession = null,
            bool allowNullManagedVideoArtifactSession = false) => new(
            ApplicationInfo.Phase11B4,
            new FakeSettingsManager(),
            new FakeSettingsDialogService(),
            new FakeMediaDialogService(),
            MediaInput,
            allowNull ? videoFrameReader! : videoFrameReader ?? VideoFrames,
            VideoPublications,
            ManagedVideoRenders,
            allowNullManagedVideoArtifactSession
                ? managedVideoArtifactSession!
                : managedVideoArtifactSession ?? ManagedArtifacts,
            RenderedVideoPlayer,
            OriginalVideoPlayer,
            new VideoPlaybackCoordinator(OriginalVideoPlayer, RenderedVideoPlayer),
            FfmpegLocator,
            ImagePipeline,
            ImageExporter,
            SaveDialogs,
            ImageSources,
            UserDialogs,
            allowNullFolderLauncher ? folderLauncher! : folderLauncher ?? FolderLauncher,
            new CharacterPresetProvider(),
            allowNullExportStatusPresentationService
                ? exportStatusPresentationService!
                : exportStatusPresentationService ?? ExportStatusPresentation);
    }

    private sealed class FakeManagedVideoArtifactSession : IManagedVideoArtifactSession
    {
        public ManagedVideoArtifact? Current { get; private set; }

        public int PendingCleanupCount => 0;

        public IReadOnlyList<string> CleanupDiagnostics => [];

        public int ClearCalls { get; private set; }

        public int AcceptCalls { get; private set; }

        public int RejectCalls { get; private set; }

        public Func<ManagedVideoArtifact, CancellationToken, Task>? AcceptHandler { get; set; }

        public Func<ManagedVideoArtifact, CancellationToken, Task>? RejectHandler { get; set; }

        public Func<CancellationToken, Task>? ClearHandler { get; set; }

        public async Task AcceptAsync(ManagedVideoArtifact artifact, CancellationToken cancellationToken = default)
        {
            AcceptCalls++;
            if (AcceptHandler is not null)
            {
                await AcceptHandler(artifact, cancellationToken);
            }

            Current = artifact;
        }

        public async Task RejectAsync(ManagedVideoArtifact artifact, CancellationToken cancellationToken = default)
        {
            RejectCalls++;
            if (RejectHandler is not null)
            {
                await RejectHandler(artifact, cancellationToken);
            }
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            ClearCalls++;
            if (ClearHandler is not null)
            {
                await ClearHandler(cancellationToken);
            }

            Current = null;
        }

        public Task<ArtifactCleanupResult> RecoverOrphansAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ArtifactCleanupResult(0, []));

        public Task<T> UseCurrentAsync<T>(ManagedVideoArtifact artifact, Func<CancellationToken, Task<T>> borrower,
            CancellationToken cancellationToken = default) => borrower(cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeManagedVideoRenderService : IManagedVideoRenderService
    {
        public Func<ManagedVideoRenderJob, CancellationToken, IProgress<VideoExportProgress>?, Task<ManagedVideoRenderResult>> Handler { get; set; } =
            (_, _, _) => Task.FromResult(ManagedVideoRenderResult.Failure(
                ManagedVideoArtifactErrorCategory.RenderFailed,
                "Managed render was not configured for this test."));

        public ManagedVideoRenderJob? LastJob { get; private set; }

        public int Calls { get; private set; }

        public Task<ManagedVideoRenderResult> RenderAsync(
            ManagedVideoRenderJob job,
            CancellationToken cancellationToken = default,
            IProgress<VideoExportProgress>? progress = null)
        {
            Calls++;
            LastJob = job;
            return Handler(job, cancellationToken, progress);
        }
    }

    private sealed class FakeRenderedVideoPlayer : IRenderedVideoPlayer
    {
        private readonly DrawingImage _source = new();

        public event EventHandler? StateChanged;

        public ImageSource PlaybackSource => _source;

        public string? SourcePath { get; private set; }

        public TimeSpan Position { get; private set; }

        public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(1);

        public double Volume { get; private set; } = 0.75d;

        public bool IsMuted { get; private set; }

        public bool IsOpen { get; private set; }

        public bool IsPlaying { get; private set; }

        public bool HasEnded { get; private set; }

        public bool IsTimerRunning => IsPlaying;

        public string? FailureMessage { get; private set; }

        public string? TechnicalDetail { get; private set; }

        public int OpenCalls { get; private set; }

        public int CloseCalls { get; private set; }

        private long _openGeneration;

        public Func<string, CancellationToken, Task<RenderedVideoOpenResult>> Handler { get; set; } =
            (_, _) => Task.FromResult(RenderedVideoOpenResult.Opened());

        public async Task<RenderedVideoOpenResult> OpenAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            OpenCalls++;
            long generation = ++_openGeneration;
            RenderedVideoOpenResult result = await Handler(filePath, cancellationToken);
            // Mirror the production player's generation contract: obsolete callbacks cannot
            // mutate a transport that has been closed or replaced by another OpenAsync.
            if (generation != _openGeneration)
            {
                return RenderedVideoOpenResult.Failed("Superseded open.");
            }
            SourcePath = filePath;
            IsOpen = result.Success;
            FailureMessage = result.Success ? null : result.UserMessage;
            TechnicalDetail = result.TechnicalDetail;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return result;
        }

        public void Play()
        {
            IsPlaying = IsOpen;
            HasEnded = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Pause()
        {
            IsPlaying = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Restart()
        {
            Position = TimeSpan.Zero;
            Play();
        }

        public void Seek(TimeSpan position)
        {
            Position = position;
            HasEnded = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetVolume(double volume)
        {
            Volume = Math.Clamp(volume, 0d, 1d);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ToggleMute()
        {
            IsMuted = !IsMuted;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ShowFullscreen(string fitMode)
        {
        }

        public void Close()
        {
            _openGeneration++;
            CloseCalls++;
            SourcePath = null;
            IsOpen = false;
            IsPlaying = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() => Close();

        public void FailPlayback()
        {
            IsOpen = false;
            IsPlaying = false;
            HasEnded = false;
            FailureMessage = "The rendered video could not be played.";
            TechnicalDetail = "Controlled playback failure.";
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void EndPlayback()
        {
            Position = Duration;
            IsPlaying = false;
            HasEnded = true;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeOriginalVideoPlayer : IOriginalVideoPlayer
    {
        private readonly DrawingImage _source = new();
        private long _generation;

        public event EventHandler? StateChanged;
        public ImageSource PlaybackSource => _source;
        public string? SourcePath { get; private set; }
        public TimeSpan Position { get; private set; }
        public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(10);
        public int NaturalVideoWidth { get; set; } = 640;
        public int NaturalVideoHeight { get; set; } = 360;
        public double Volume { get; private set; } = 0.75d;
        public bool IsMuted { get; private set; }
        public bool IsOpening { get; private set; }
        public bool IsPlaybackReady { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool HasEnded { get; private set; }
        public bool IsTimerRunning => IsPlaying;
        public bool FallbackRequired { get; private set; }
        public string? FailureMessage { get; private set; }
        public string? TechnicalDetail { get; private set; }
        public int OpenCalls { get; private set; }
        public int CloseCalls { get; private set; }
        public int PauseCalls { get; private set; }
        public int FullscreenCalls { get; private set; }
        public string? LastFullscreenFitMode { get; private set; }
        public Func<string, CancellationToken, Task<OriginalVideoOpenResult>> Handler { get; set; } =
            (_, _) => Task.FromResult(new OriginalVideoOpenResult(
                OriginalVideoOpenOutcome.FallbackRequired,
                OriginalVideoPlayer.PlaybackUnavailableMessage,
                "Controlled fallback."));

        public async Task<OriginalVideoOpenResult> OpenAsync(string filePath, CancellationToken cancellationToken = default)
        {
            OpenCalls++;
            long generation = ++_generation;
            SourcePath = filePath;
            IsOpening = true;
            StateChanged?.Invoke(this, EventArgs.Empty);
            OriginalVideoOpenResult result = await Handler(filePath, cancellationToken);
            if (generation != _generation)
            {
                return new OriginalVideoOpenResult(OriginalVideoOpenOutcome.Superseded, "Superseded", null);
            }

            IsOpening = false;
            IsPlaybackReady = result.IsReady;
            FallbackRequired = result.FallbackRequired;
            FailureMessage = result.IsReady ? null : result.UserMessage;
            TechnicalDetail = result.TechnicalDetail;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return result;
        }

        public void Play()
        {
            IsPlaying = IsPlaybackReady;
            HasEnded = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Pause()
        {
            if (!IsPlaybackReady)
            {
                return;
            }

            PauseCalls++;
            IsPlaying = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Restart()
        {
            Position = TimeSpan.Zero;
            Play();
        }

        public void Seek(TimeSpan position)
        {
            Position = position < TimeSpan.Zero ? TimeSpan.Zero : position;
            HasEnded = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetVolume(double volume)
        {
            Volume = Math.Clamp(volume, 0d, 1d);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ToggleMute()
        {
            IsMuted = !IsMuted;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ShowFullscreen(string fitMode)
        {
            FullscreenCalls++;
            LastFullscreenFitMode = fitMode;
        }
        public void SetOwner(Window? owner) { }
        public void UpdateFitMode(string fitMode) { }

        public void Close()
        {
            _generation++;
            CloseCalls++;
            SourcePath = null;
            IsOpening = false;
            IsPlaybackReady = false;
            IsPlaying = false;
            FallbackRequired = false;
            FailureMessage = null;
            TechnicalDetail = null;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() => Close();

        public void FailPlayback(string detail = "Controlled Original playback failure.")
        {
            IsPlaybackReady = false;
            IsPlaying = false;
            FallbackRequired = true;
            FailureMessage = OriginalVideoPlayer.PlaybackUnavailableMessage;
            TechnicalDetail = detail;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeBundledFfmpegLocator : IBundledFfmpegLocator
    {
        public bool Available { get; set; } = true;

        public BundledFfmpegLocation Locate() => new(
            "C:\\GlyphFrame",
            "C:\\GlyphFrame\\Tools\\ffmpeg",
            new BundledToolPath(FfmpegToolKind.Ffmpeg, "C:\\GlyphFrame\\Tools\\ffmpeg\\ffmpeg.exe", Available, false),
            new BundledToolPath(FfmpegToolKind.Ffprobe, "C:\\GlyphFrame\\Tools\\ffmpeg\\ffprobe.exe", Available, false));
    }

    private sealed class FakeExportStatusPresentationService : IExportStatusPresentationService
    {
        public bool AnimationsEnabled { get; set; } = true;

        public Func<CancellationToken, Task> Handler { get; set; } = _ => Task.CompletedTask;

        public int Calls { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public Task HoldCompletedStateAsync(CancellationToken cancellationToken)
        {
            Calls++;
            LastCancellationToken = cancellationToken;
            return Handler(cancellationToken);
        }
    }

    private sealed class FakeVideoFrameReader : IVideoFrameReader
    {
        public int Calls { get; private set; }

        public double LastNormalizedPosition { get; private set; }

        public Func<string, double, VideoMetadata?, CancellationToken, Task<VideoFrameReadResult>> Handler { get; set; } =
            (_, position, metadata, _) => Task.FromResult(FrameResult(SolidFrame(1, 2, 3), position, metadata));

        public Task<VideoFrameReadResult> ReadAsync(
            string inputPath,
            double normalizedPosition,
            VideoMetadata? metadataContext = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastNormalizedPosition = normalizedPosition;
            return Handler(inputPath, normalizedPosition, metadataContext, cancellationToken);
        }
    }

    private sealed class FakeMediaInputService : IMediaInputService
    {
        public Func<string?, CancellationToken, Task<MediaInputResult>> Handler { get; set; } =
            (path, _) => Task.FromResult(VideoResult(path!));

        public Task<MediaInputResult> LoadAsync(string? inputPath, CancellationToken cancellationToken = default) =>
            Handler(inputPath, cancellationToken);
    }

    private sealed class FakeVideoArtifactPublisher : IVideoArtifactPublisher
    {
        public Action? Preparing { get; set; }
        public Func<VideoArtifactPublicationJob, CancellationToken, IProgress<VideoExportProgress>?, Task<VideoExportResult>> Handler { get; set; } =
            (job, _, _) => Task.FromResult(VideoExportResult.Completed(
                job.Destination.FilePath,
                1,
                new PixelDimensions(2, 2),
                30));

        public VideoArtifactPublicationJob? LastJob { get; private set; }

        public Task<VideoPublicationDestination> PrepareAsync(string outputPath, string originalInputPath, CancellationToken cancellationToken = default)
        {
            Preparing?.Invoke();
            return Task.FromResult(new VideoPublicationDestination(outputPath, File.Exists(outputPath) ? "existing" : null));
        }

        public int Calls { get; private set; }

        public Task<VideoExportResult> PublishAsync(
            VideoArtifactPublicationJob job,
            CancellationToken cancellationToken = default,
            IProgress<VideoExportProgress>? progress = null)
        {
            Calls++;
            LastJob = job;
            return Handler(job, cancellationToken, progress);
        }
    }

    private sealed class FakeImagePipelineService : IImagePipelineService
    {
        public DecodedImage DecodeResult { get; set; } = SolidFrame(4, 5, 6);

        public DecodedImage? LastRenderedSource { get; private set; }

        public ImageRenderSettings? LastRenderSettings { get; private set; }

        public CharacterPresetOption? LastRenderedPreset { get; private set; }

        public int RenderCalls { get; private set; }

        public Func<DecodedImage, ImageRenderSettings, CharacterPresetOption, CancellationToken, Task<ImagePipelineResult>> RenderHandler { get; set; } =
            (_, _, _, _) => Task.FromResult(RenderResult());

        public Task<DecodedImage> DecodeAsync(
            string inputPath,
            ImageOrientation orientation,
            CancellationToken cancellationToken = default) => Task.FromResult(DecodeResult);

        public Task<ImagePipelineResult> RenderAsync(
            DecodedImage source,
            ImageRenderSettings settings,
            CharacterPresetOption preset,
            CancellationToken cancellationToken = default)
        {
            RenderCalls++;
            LastRenderedSource = source;
            LastRenderSettings = settings;
            LastRenderedPreset = preset;
            return RenderHandler(source, settings, preset, cancellationToken);
        }
    }

    private sealed class FakeImageSourceFactory : IImageSourceFactory
    {
        public DecodedImage? LastDecodedImage { get; private set; }

        public ImageSource Create(DecodedImage image)
        {
            LastDecodedImage = image;
            return new DrawingImage();
        }

        public ImageSource Create(RenderedImage image) => new DrawingImage();
    }

    private sealed class FakeSettingsManager : IApplicationSettingsManager
    {
        public AppSettings Current { get; private set; } = new();

        public string? StartupStatusMessage => null;

        public event EventHandler<AppSettings>? SettingsChanged;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            Current = settings;
            SettingsChanged?.Invoke(this, settings);
            return Task.CompletedTask;
        }

        public Task<AppSettings> ResetAsync(CancellationToken cancellationToken = default)
        {
            Current = new AppSettings();
            SettingsChanged?.Invoke(this, Current);
            return Task.FromResult(Current);
        }
    }

    private sealed class FakeSettingsDialogService : ISettingsDialogService
    {
        public void Show()
        {
        }
    }

    private sealed class FakeMediaDialogService : IMediaFileDialogService
    {
        public string? SelectMediaFile() => null;
    }

    private sealed class FakeImageExporter : IImageExporter
    {
        public Func<RenderedImage, string, ImageExportFormat, bool, CancellationToken, Task> Handler { get; set; } =
            (_, _, _, _, _) => Task.CompletedTask;

        public RenderedImage? LastImage { get; private set; }

        public ImageExportFormat? LastFormat { get; private set; }

        public int Calls { get; private set; }

        public Task ExportAsync(
            RenderedImage image,
            string outputPath,
            ImageExportFormat format,
            bool overwrite,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastImage = image;
            LastFormat = format;
            return Handler(image, outputPath, format, overwrite, cancellationToken);
        }
    }

    private sealed class FakeImageSaveDialogService : IImageSaveDialogService
    {
        public int VideoCalls { get; private set; }
        public Action? OnVideoDialog { get; set; }
        public Queue<string?> ImageOutputPaths { get; } = new();

        public string? VideoOutputPath { get; set; }

        public string? SelectOutputFile(string sourcePath, ImageExportFormat format) =>
            ImageOutputPaths.Count > 0 ? ImageOutputPaths.Dequeue() : null;

        public string? SelectVideoOutputFile(string sourcePath)
        {
            VideoCalls++;
            OnVideoDialog?.Invoke();
            return VideoOutputPath;
        }
    }

    private sealed class FakeUserDialogService : IUserDialogService
    {
        public bool ConfirmResult { get; set; } = true;

        public bool Confirm(
            string title,
            string message,
            string confirmButtonText = "Confirm",
            string cancelButtonText = "Cancel") => ConfirmResult;
    }

    private sealed class FakeFolderLauncher : IFolderLauncher
    {
        public string? LastFolder { get; private set; }

        public Task OpenFolderAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            LastFolder = folderPath;
            return Task.CompletedTask;
        }
    }

    private sealed class TestMediaFiles : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "GlyphFrame.App.Tests",
            Guid.NewGuid().ToString("N"));

        public TestMediaFiles() => Directory.CreateDirectory(_root);

        public string Create(string fileName)
        {
            string path = Path.Combine(_root, fileName);
            File.WriteAllBytes(path, [1, 2, 3, 4]);
            return path;
        }

        public string PathFor(string fileName) => Path.Combine(_root, fileName);

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
