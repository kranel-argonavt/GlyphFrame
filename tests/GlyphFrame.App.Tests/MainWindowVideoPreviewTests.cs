using System.ComponentModel;
using System.IO;
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
        Assert.True(viewModel.ExportVideoCommand.CanExecute(null));
        Assert.True(viewModel.ShowOutputCard);
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
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.DropMediaCommand.ExecuteAsync([invalidPath]);

        Assert.Equal(1, harness.ManagedArtifacts.ClearCalls);
        Assert.Equal(videoPath, viewModel.VideoMetadata?.FilePath);
    }

    [Fact]
    public async Task VideoExport_UsesCurrentSettingsAndRestoresUiAfterSuccess()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("export.mp4");
        string outputPath = files.PathFor("символи output.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = outputPath;
        harness.VideoExports.Handler = (job, _, _) => Task.FromResult(VideoExportResult.Completed(
            job.OutputPath,
            12,
            new PixelDimensions(64, 48),
            10));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        viewModel.SelectedAudioMode = AudioExportMode.RemoveAudio;

        await viewModel.ExportVideoCommand.ExecuteAsync(null);

        Assert.NotNull(harness.VideoExports.LastJob);
        Assert.Equal(videoPath, harness.VideoExports.LastJob!.InputPath);
        Assert.Equal(outputPath, harness.VideoExports.LastJob.OutputPath);
        Assert.Equal(viewModel.SelectedPreset, harness.VideoExports.LastJob.CharacterPreset);
        Assert.Equal(AudioExportMode.RemoveAudio, harness.VideoExports.LastJob.AudioMode);
        Assert.Equal(100, viewModel.ExportProgress);
        Assert.False(viewModel.IsExportActive);
        Assert.Contains("without audio", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(outputPath, viewModel.OutputPathText);
    }

    [Theory]
    [InlineData(AudioMuxOutcome.Removed, "without audio")]
    [InlineData(AudioMuxOutcome.NoSourceAudio, "source contained no audio")]
    [InlineData(AudioMuxOutcome.StreamCopied, "original audio")]
    [InlineData(AudioMuxOutcome.TranscodedToAac, "converted to AAC")]
    public async Task VideoExport_SuccessMessageDistinguishesAudioOutcome(
        AudioMuxOutcome outcome,
        string expectedMessage)
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("audio-outcome.mp4");
        string outputPath = files.PathFor("audio-outcome-output.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = outputPath;
        harness.VideoExports.Handler = (job, _, _) => Task.FromResult(VideoExportResult.Completed(
            job.OutputPath,
            12,
            new PixelDimensions(64, 48),
            10,
            outcome));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        await viewModel.ExportVideoCommand.ExecuteAsync(null);

        Assert.Contains(expectedMessage, viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
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
        harness.VideoExports.Handler = (_, _, _) => Task.FromResult(VideoExportResult.Failure(
            VideoExportErrorCategory.EncoderFailed,
            "GlyphFrame could not finish encoding the MP4 video.",
            "raw encoder detail"));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

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
        harness.VideoExports.Handler = (_, _, _) =>
        {
            started.TrySetResult(true);
            return completion.Task;
        };
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        Task export = viewModel.ExportVideoCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.True(viewModel.IsExportActive);
        Assert.False(viewModel.ExportVideoCommand.CanExecute(null));
        Assert.False(viewModel.BrowseMediaCommand.CanExecute(null));
        Assert.False(viewModel.BrowseOutputCommand.CanExecute(null));
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
        harness.VideoExports.Handler = (_, _, _) =>
        {
            started.TrySetResult(null);
            return completion.Task;
        };
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        viewModel.SelectedAudioMode = AudioExportMode.KeepAudio;

        Task export = viewModel.ExportVideoCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        viewModel.SelectedAudioMode = AudioExportMode.RemoveAudio;

        Assert.Equal(AudioExportMode.KeepAudio, harness.VideoExports.LastJob!.AudioMode);
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
    public async Task VideoExport_LiveDeterminateProgressUpdatesFramesElapsedAndRemainingTime()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("live-progress.mp4");
        string outputPath = files.PathFor("live-progress-output.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = outputPath;
        TaskCompletionSource progressReported = NewCompletion();
        TaskCompletionSource<VideoExportResult> completion = NewCompletion<VideoExportResult>();
        harness.VideoExports.Handler = (job, _, progress) =>
        {
            progress!.Report(ExportProgress(
                VideoExportStage.ProcessingFrames,
                5,
                10,
                49.5,
                TimeSpan.FromSeconds(12),
                TimeSpan.FromSeconds(12),
                "Decoding, rendering, and encoding frames..."));
            progressReported.TrySetResult();
            return completion.Task;
        };
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        Task export = viewModel.ExportVideoCommand.ExecuteAsync(null);
        await progressReported.Task;

        Assert.False(viewModel.IsExportProgressIndeterminate);
        Assert.True(viewModel.AreExportAnimationsEnabled);
        Assert.True(viewModel.IsExportActive);
        Assert.Equal(VideoExportStage.ProcessingFrames, viewModel.ExportStage);
        Assert.Equal(49.5, viewModel.ExportProgress);
        Assert.Equal("50%", viewModel.ExportPercentageText);
        Assert.Equal("Frame 5 / 10", viewModel.ExportFrameText);
        Assert.Equal("Elapsed 00:00:12", viewModel.ElapsedText);
        Assert.Equal("Remaining about 00:00:12", viewModel.RemainingText);
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
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

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
        harness.VideoExports.Handler = (_, _, _) => Task.FromResult(VideoExportResult.Failure(
            VideoExportErrorCategory.EncoderFailed,
            "GlyphFrame could not encode the video."));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        await viewModel.ExportVideoCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsExportCompletionVisible);
        Assert.False(viewModel.IsExportStatusVisible);
        Assert.Equal(0, harness.ExportStatusPresentation.Calls);
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
        harness.VideoExports.Handler = (job, _, _) => Interlocked.Increment(ref exportCall) == 1
            ? Task.FromResult(VideoExportResult.Completed(job.OutputPath, 3, new PixelDimensions(64, 48), 10))
            : WaitForSecondExportAsync();
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
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
        harness.VideoExports.Handler = (job, _, _) => Interlocked.Increment(ref exportCall) == 1
            ? Task.FromResult(VideoExportResult.Completed(job.OutputPath, 3, new PixelDimensions(64, 48), 10))
            : WaitForSecondExportAsync();
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
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
            presentationStarted.SetResult();
            await never.Task.WaitAsync(token);
        };
        MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
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
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        await viewModel.ExportVideoCommand.ExecuteAsync(null);

        Assert.False(viewModel.AreExportAnimationsEnabled);
        Assert.True(viewModel.IsExportCompletionVisible);
        Assert.Equal("Export completed.", viewModel.ExportProgressText);
        completedStateReleased.SetResult();
    }

    [Fact]
    public async Task VideoExport_IndeterminateMuxStageHidesFrameEtaAndShowsTruthfulStage()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("mux-progress.mp4");
        string outputPath = files.PathFor("mux-progress-output.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = outputPath;
        TaskCompletionSource stageReported = NewCompletion();
        TaskCompletionSource<VideoExportResult> completion = NewCompletion<VideoExportResult>();
        harness.VideoExports.Handler = (_, _, progress) =>
        {
            progress!.Report(ExportProgress(
                VideoExportStage.MergingAudio,
                10,
                10,
                null,
                TimeSpan.FromSeconds(20),
                null,
                "Merging audio..."));
            stageReported.TrySetResult();
            return completion.Task;
        };
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        Task export = viewModel.ExportVideoCommand.ExecuteAsync(null);
        await stageReported.Task;

        Assert.True(viewModel.IsExportProgressIndeterminate);
        Assert.Equal("...", viewModel.ExportPercentageText);
        Assert.Equal("Merging audio...", viewModel.ExportProgressText);
        Assert.Equal("Frame 10 / 10", viewModel.ExportFrameText);
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
        harness.VideoExports.Handler = async (_, token, _) =>
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
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

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
        Assert.False(viewModel.BrowseOutputCommand.CanExecute(null));
        Assert.False(viewModel.AreSettingsEnabled);

        cleanupFinished.SetResult();
        await export;

        Assert.False(viewModel.IsExportActive);
        Assert.False(viewModel.IsExportCancellationRequested);
        Assert.False(viewModel.IsExportCompletionVisible);
        Assert.True(viewModel.BrowseMediaCommand.CanExecute(null));
        Assert.True(viewModel.BrowseOutputCommand.CanExecute(null));
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
        harness.VideoExports.Handler = (job, _, progress) =>
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
                job.OutputPath,
                10,
                new PixelDimensions(64, 48),
                10));
        };
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
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
        harness.VideoExports.Handler = (job, _, progress) =>
        {
            if (Interlocked.Increment(ref call) == 1)
            {
                oldProgress = progress;
                return Task.FromResult(VideoExportResult.Completed(
                    job.OutputPath,
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
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
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
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        await viewModel.ExportVideoCommand.ExecuteAsync(null);

        Assert.Equal(0, harness.VideoExports.Calls);
        Assert.Equal(original, await File.ReadAllBytesAsync(outputPath));
        Assert.Contains("existing file was not changed", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
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
        Assert.False(viewModel.ShowOutputCard);
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
        Assert.False(viewModel.ShowOutputCard);
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

        public FakeVideoExportService VideoExports { get; } = new();

        public FakeManagedVideoArtifactSession ManagedArtifacts { get; } = new();

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
            ApplicationInfo.Phase11B2,
            new FakeSettingsManager(),
            new FakeSettingsDialogService(),
            new FakeMediaDialogService(),
            MediaInput,
            allowNull ? videoFrameReader! : videoFrameReader ?? VideoFrames,
            VideoExports,
            allowNullManagedVideoArtifactSession
                ? managedVideoArtifactSession!
                : managedVideoArtifactSession ?? ManagedArtifacts,
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
        public ManagedVideoArtifact? Current => null;

        public int PendingCleanupCount => 0;

        public IReadOnlyList<string> CleanupDiagnostics => [];

        public int ClearCalls { get; private set; }

        public Task AcceptAsync(ManagedVideoArtifact artifact, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RejectAsync(ManagedVideoArtifact artifact, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            ClearCalls++;
            return Task.CompletedTask;
        }

        public Task<ArtifactCleanupResult> RecoverOrphansAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ArtifactCleanupResult(0, []));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
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
        public Func<string, double, VideoMetadata?, CancellationToken, Task<VideoFrameReadResult>> Handler { get; set; } =
            (_, position, metadata, _) => Task.FromResult(FrameResult(SolidFrame(1, 2, 3), position, metadata));

        public Task<VideoFrameReadResult> ReadAsync(
            string inputPath,
            double normalizedPosition,
            VideoMetadata? metadataContext = null,
            CancellationToken cancellationToken = default) =>
            Handler(inputPath, normalizedPosition, metadataContext, cancellationToken);
    }

    private sealed class FakeMediaInputService : IMediaInputService
    {
        public Func<string?, CancellationToken, Task<MediaInputResult>> Handler { get; set; } =
            (path, _) => Task.FromResult(VideoResult(path!));

        public Task<MediaInputResult> LoadAsync(string? inputPath, CancellationToken cancellationToken = default) =>
            Handler(inputPath, cancellationToken);
    }

    private sealed class FakeVideoExportService : IVideoExportService
    {
        public Func<VideoExportJob, CancellationToken, IProgress<VideoExportProgress>?, Task<VideoExportResult>> Handler { get; set; } =
            (job, _, _) => Task.FromResult(VideoExportResult.Completed(
                job.OutputPath,
                1,
                new PixelDimensions(2, 2),
                30));

        public VideoExportJob? LastJob { get; private set; }

        public int Calls { get; private set; }

        public Task<VideoExportResult> ExportAsync(
            VideoExportJob job,
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
        public Queue<string?> ImageOutputPaths { get; } = new();

        public string? VideoOutputPath { get; set; }

        public string? SelectOutputFile(string sourcePath, ImageExportFormat format) =>
            ImageOutputPaths.Count > 0 ? ImageOutputPaths.Dequeue() : null;

        public string? SelectVideoOutputFile(string sourcePath) => VideoOutputPath;
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
