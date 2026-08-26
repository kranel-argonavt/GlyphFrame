using System.Runtime.CompilerServices;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Infrastructure.Media;
using GlyphFrame.Rendering.Grid;
using GlyphFrame.Core.Policies;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class StreamingVideoExportServiceTests
{
    [Fact]
    public async Task ExportAsync_StreamsFramesInOrderCropsOddDimensionsAndFinalizesAtomically()
    {
        using TemporaryMediaDirectory media = new("відео export 日本語");
        string input = media.CopyFixture("video-preview.mp4", "джерело відео.mp4");
        string output = Path.Combine(media.RootPath, "символи 日本語.mp4");
        FakeFrameReader reader = new(3, 3, frameCount: 3);
        RecordingPipeline pipeline = new();
        RecordingEncoder encoder = new(writePartial: true);
        StreamingVideoExportService service = CreateService(reader, encoder, pipeline);

        VideoExportResult result = await service.ExportAsync(Job(input, output, overwrite: false, width: 3, height: 3));

        Assert.True(result.Success, result.TechnicalDetail ?? result.UserMessage);
        Assert.Equal(3, result.FramesWritten);
        Assert.Equal(new PixelDimensions(2, 2), result.OutputDimensions);
        Assert.Equal([0, 1, 2], encoder.FrameMarkers);
        Assert.All(encoder.FrameDimensions, dimensions => Assert.Equal(new PixelDimensions(2, 2), dimensions));
        Assert.Equal(1, pipeline.MaximumConcurrentRenders);
        Assert.True(File.Exists(output));
        Assert.Empty(Directory.GetFiles(media.RootPath, "*.partial"));
        Assert.Empty(Directory.GetFiles(media.RootPath, "*.raw"));
        Assert.Empty(Directory.GetFiles(media.RootPath, "*.png"));
        using FileStream inputExclusive = new(input, FileMode.Open, FileAccess.Read, FileShare.None);
        using FileStream outputExclusive = new(output, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.True(inputExclusive.CanRead);
        Assert.True(outputExclusive.CanRead);
    }

    [Fact]
    public async Task ExportAsync_RemoveAudioReportsAcceptedFramesThenFinalizesAndCompletesAfterPublication()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "progress.mp4");
        RecordingProgress<VideoExportProgress> progress = new();
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 3),
            new RecordingEncoder(writePartial: true),
            new RecordingPipeline());

        VideoExportResult result = await service.ExportAsync(
            Job(input, output, overwrite: false),
            CancellationToken.None,
            progress);

        Assert.True(result.Success);
        Assert.Equal(
            [
                VideoExportStage.Preparing,
                VideoExportStage.ProcessingFrames,
                VideoExportStage.FinishingEncoding,
                VideoExportStage.Finalizing,
                VideoExportStage.Completed
            ],
            ConsecutiveStages(progress.Values));
        VideoExportProgress[] frameUpdates = progress.Values
            .Where(update => update.Stage == VideoExportStage.ProcessingFrames && update.ProcessedFrames > 0)
            .ToArray();
        Assert.Equal([1L, 2L, 3L], frameUpdates.Select(update => update.ProcessedFrames));
        Assert.All(frameUpdates, update => Assert.Equal(12, update.TotalFrames));
        Assert.Equal(100d, progress.Values[^1].Percentage);
        Assert.True(File.Exists(output));
        Assert.DoesNotContain(progress.Values.Take(progress.Values.Count - 1), update => update.Percentage == 100d);
    }

    [Fact]
    public async Task ExportAsync_KeepAudioReportsProbeAndMuxStagesWithoutFrameEtaLeakage()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-audio-aac.mp4");
        string output = Path.Combine(media.RootPath, "progress-audio.mp4");
        RecordingProgress<VideoExportProgress> progress = new();
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 2),
            new RecordingEncoder(writePartial: true),
            new RecordingPipeline(),
            new FakeAudioProbe(AudioProbeResult.Present(new AudioStreamInfo("aac", 48_000, 1, null, null))),
            new FakeAudioMuxer());

        VideoExportResult result = await service.ExportAsync(
            Job(input, output, overwrite: false, audioMode: AudioExportMode.KeepAudio),
            CancellationToken.None,
            progress);

        Assert.True(result.Success);
        Assert.Equal(
            [
                VideoExportStage.Preparing,
                VideoExportStage.ProcessingFrames,
                VideoExportStage.FinishingEncoding,
                VideoExportStage.InspectingAudio,
                VideoExportStage.MergingAudio,
                VideoExportStage.Finalizing,
                VideoExportStage.Completed
            ],
            ConsecutiveStages(progress.Values));
        Assert.All(
            progress.Values.Where(update => update.Stage is VideoExportStage.InspectingAudio
                or VideoExportStage.MergingAudio
                or VideoExportStage.Finalizing),
            update =>
            {
                Assert.True(update.IsIndeterminate);
                Assert.Null(update.EstimatedRemaining);
            });
    }

    [Fact]
    public async Task ExportAsync_SilentKeepAudioReportsProbeButNeverFakeMuxProgress()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "progress-silent.mp4");
        RecordingProgress<VideoExportProgress> progress = new();
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 2),
            new RecordingEncoder(writePartial: true),
            new RecordingPipeline(),
            new FakeAudioProbe(AudioProbeResult.Absent()),
            new FakeAudioMuxer());

        VideoExportResult result = await service.ExportAsync(
            Job(input, output, overwrite: false, audioMode: AudioExportMode.KeepAudio),
            CancellationToken.None,
            progress);

        Assert.True(result.Success);
        Assert.Contains(progress.Values, update => update.Stage == VideoExportStage.InspectingAudio);
        Assert.DoesNotContain(progress.Values, update => update.Stage == VideoExportStage.MergingAudio);
    }

    [Fact]
    public async Task ExportAsync_CancellationReportsTerminalCancellingAndRejectsLateUpdates()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "progress-cancel.mp4");
        RecordingProgress<VideoExportProgress> progress = new();
        RecordingEncoder encoder = new(writePartial: true) { Cancel = true };
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 2),
            encoder,
            new RecordingPipeline());

        VideoExportResult result = await service.ExportAsync(
            Job(input, output, overwrite: false),
            CancellationToken.None,
            progress);

        Assert.Equal(VideoExportStatus.Cancelled, result.Status);
        Assert.Equal(VideoExportStage.Cancelling, progress.Values[^1].Stage);
        Assert.DoesNotContain(progress.Values, update => update.Stage == VideoExportStage.Completed);
        Assert.False(File.Exists(output));
        AssertNoExportArtifacts(media.RootPath);
    }

    [Fact]
    public async Task ExportAsync_EncoderFailurePreservesExistingDestinationAndDeletesPartial()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "existing.mp4");
        await File.WriteAllBytesAsync(output, [9, 8, 7]);
        RecordingEncoder encoder = new(writePartial: true)
        {
            Result = new VideoEncodingResult(false, 1, "friendly failure", "encoder detail")
        };
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 1),
            encoder,
            new RecordingPipeline());

        VideoExportResult result = await service.ExportAsync(Job(input, output, overwrite: true));

        Assert.False(result.Success);
        Assert.Equal(VideoExportErrorCategory.EncoderFailed, result.ErrorCategory);
        Assert.Equal([9, 8, 7], await File.ReadAllBytesAsync(output));
        Assert.Empty(Directory.GetFiles(media.RootPath, "*.partial"));
    }

    [Fact]
    public async Task ExportAsync_CancellationDeletesPartialOutput()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "cancelled.mp4");
        RecordingEncoder encoder = new(writePartial: true) { Cancel = true };
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 2),
            encoder,
            new RecordingPipeline());

        VideoExportResult result = await service.ExportAsync(Job(input, output, overwrite: false));

        Assert.Equal(VideoExportStatus.Cancelled, result.Status);
        Assert.False(File.Exists(output));
        Assert.Empty(Directory.GetFiles(media.RootPath, "*.partial"));
    }

    [Fact]
    public async Task ExportAsync_CancellationDuringRenderingCleansPartialAndPreservesDestination()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "existing-render-cancel.mp4");
        await File.WriteAllBytesAsync(output, [9, 4, 2]);
        TaskCompletionSource renderStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource renderRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingPipeline pipeline = new()
        {
            RenderStarted = renderStarted,
            RenderRelease = renderRelease
        };
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 2),
            new RecordingEncoder(writePartial: true),
            pipeline);
        using CancellationTokenSource cancellation = new();

        Task<VideoExportResult> export = service.ExportAsync(
            Job(input, output, overwrite: true),
            cancellation.Token);
        await renderStarted.Task;
        cancellation.Cancel();
        VideoExportResult result = await export;

        Assert.Equal(VideoExportStatus.Cancelled, result.Status);
        Assert.Equal([9, 4, 2], await File.ReadAllBytesAsync(output));
        AssertNoExportArtifacts(media.RootPath);
    }

    [Fact]
    public async Task ExportAsync_DeclinedOverwriteNeverStartsEncoder()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "existing.mp4");
        await File.WriteAllBytesAsync(output, [4, 5, 6]);
        RecordingEncoder encoder = new(writePartial: true);
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 1),
            encoder,
            new RecordingPipeline());

        VideoExportResult result = await service.ExportAsync(Job(input, output, overwrite: false));

        Assert.False(result.Success);
        Assert.Equal(0, encoder.Calls);
        Assert.Equal([4, 5, 6], await File.ReadAllBytesAsync(output));
    }

    [Theory]
    [InlineData("decoder")]
    [InlineData("renderer")]
    public async Task ExportAsync_DecodeOrRenderFailurePreservesDestinationAndRemovesAllArtifacts(string failure)
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "existing.mp4");
        await File.WriteAllBytesAsync(output, [7, 6, 5]);
        FakeFrameReader reader = new(64, 48, 2)
        {
            Failure = failure == "decoder"
                ? new VideoExportException(
                    VideoExportErrorCategory.DecodeFailed,
                    "friendly decode failure",
                    "decoder detail")
                : null
        };
        RecordingPipeline pipeline = new()
        {
            Failure = failure == "renderer"
                ? new ImageProcessingException(
                    ImageProcessingErrorCategory.RenderingFailed,
                    "friendly render failure",
                    "renderer detail")
                : null
        };
        StreamingVideoExportService service = CreateService(
            reader,
            new RecordingEncoder(writePartial: true),
            pipeline);

        VideoExportResult result = await service.ExportAsync(Job(input, output, overwrite: true));

        Assert.False(result.Success);
        Assert.Equal(
            failure == "decoder" ? VideoExportErrorCategory.DecodeFailed : VideoExportErrorCategory.RenderingFailed,
            result.ErrorCategory);
        Assert.Equal([7, 6, 5], await File.ReadAllBytesAsync(output));
        AssertNoExportArtifacts(media.RootPath);
    }

    [Fact]
    public async Task ExportAsync_CancellationImmediatelyBeforePublicationPreservesDestinationAndDeletesPartial()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "existing.mp4");
        await File.WriteAllBytesAsync(output, [3, 2, 1]);
        using CancellationTokenSource cancellation = new();
        RecordingEncoder encoder = new(writePartial: true)
        {
            BeforeReturn = cancellation.Cancel
        };
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 1),
            encoder,
            new RecordingPipeline());

        VideoExportResult result = await service.ExportAsync(
            Job(input, output, overwrite: true),
            cancellation.Token);

        Assert.Equal(VideoExportStatus.Cancelled, result.Status);
        Assert.Equal([3, 2, 1], await File.ReadAllBytesAsync(output));
        AssertNoExportArtifacts(media.RootPath);
    }

    [Fact]
    public async Task ExportAsync_CancellationAfterAtomicPublicationDoesNotRewriteSuccessAsCancelled()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "published.mp4");
        using CancellationTokenSource cancellation = new();
        CallbackProgress<VideoExportProgress> progress = new(update =>
        {
            if (update.Stage == VideoExportStage.Completed)
            {
                cancellation.Cancel();
            }
        });
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 1),
            new RecordingEncoder(writePartial: true),
            new RecordingPipeline());

        VideoExportResult result = await service.ExportAsync(
            Job(input, output, overwrite: false),
            cancellation.Token,
            progress);

        Assert.True(result.Success);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.True(File.Exists(output));
        AssertNoExportArtifacts(media.RootPath);
    }

    [Fact]
    public async Task ExportAsync_FinalizationFailureReturnsSpecificCategoryAndDeletesPartial()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "occupied.mp4");
        Directory.CreateDirectory(output);
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 1),
            new RecordingEncoder(writePartial: true),
            new RecordingPipeline());

        VideoExportResult result = await service.ExportAsync(Job(input, output, overwrite: false));

        Assert.Equal(VideoExportErrorCategory.OutputFinalizationFailed, result.ErrorCategory);
        Assert.True(Directory.Exists(output));
        AssertNoExportArtifacts(media.RootPath);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-1)]
    public async Task ExportAsync_FrameSequenceGapOrReorderingIsRejected(long secondSequence)
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "sequence.mp4");
        FakeFrameReader reader = new(64, 48, 2) { SequenceNumbers = [0, secondSequence] };
        StreamingVideoExportService service = CreateService(
            reader,
            new RecordingEncoder(writePartial: true),
            new RecordingPipeline());

        VideoExportResult result = await service.ExportAsync(Job(input, output, overwrite: false));

        Assert.Equal(VideoExportErrorCategory.DecodeFailed, result.ErrorCategory);
        Assert.Contains("sequential order", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(output));
        AssertNoExportArtifacts(media.RootPath);
    }

    [Fact]
    public async Task ExportAsync_PreservesSpecificEncoderFailureCategory()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "write-failure.mp4");
        RecordingEncoder encoder = new(writePartial: true)
        {
            Result = new VideoEncodingResult(
                false,
                1,
                "friendly write failure",
                "write detail",
                VideoExportErrorCategory.EncoderWriteFailed)
        };
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 1),
            encoder,
            new RecordingPipeline());

        VideoExportResult result = await service.ExportAsync(Job(input, output, overwrite: false));

        Assert.Equal(VideoExportErrorCategory.EncoderWriteFailed, result.ErrorCategory);
        AssertNoExportArtifacts(media.RootPath);
    }

    [Fact]
    public async Task ExportAsync_RemoveAudioSkipsProbeAndMuxAndReportsRemoved()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-audio-aac.mp4");
        string output = Path.Combine(media.RootPath, "removed.mp4");
        FakeAudioProbe probe = new(AudioProbeResult.Present(new AudioStreamInfo("aac", 48_000, 1, null, null)));
        FakeAudioMuxer muxer = new();
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 2),
            new RecordingEncoder(writePartial: true),
            new RecordingPipeline(),
            probe,
            muxer);

        VideoExportResult result = await service.ExportAsync(Job(
            input,
            output,
            overwrite: false,
            audioMode: AudioExportMode.RemoveAudio));

        Assert.True(result.Success);
        Assert.Equal(AudioMuxOutcome.Removed, result.AudioOutcome);
        Assert.Equal(0, probe.Calls);
        Assert.Equal(0, muxer.Calls);
        AssertNoExportArtifacts(media.RootPath);
    }

    [Fact]
    public async Task ExportAsync_KeepAudioWithoutSourceAudioSkipsMuxAndReportsNoSourceAudio()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "silent.mp4");
        FakeAudioProbe probe = new(AudioProbeResult.Absent());
        FakeAudioMuxer muxer = new();
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 2),
            new RecordingEncoder(writePartial: true),
            new RecordingPipeline(),
            probe,
            muxer);

        VideoExportResult result = await service.ExportAsync(Job(
            input,
            output,
            overwrite: false,
            audioMode: AudioExportMode.KeepAudio));

        Assert.True(result.Success);
        Assert.Equal(AudioMuxOutcome.NoSourceAudio, result.AudioOutcome);
        Assert.Equal(1, probe.Calls);
        Assert.Equal(0, muxer.Calls);
        AssertNoExportArtifacts(media.RootPath);
    }

    [Theory]
    [InlineData("aac", AudioMuxStrategy.StreamCopy, AudioMuxOutcome.StreamCopied)]
    [InlineData("pcm_s16le", AudioMuxStrategy.TranscodeToAac, AudioMuxOutcome.TranscodedToAac)]
    [InlineData(null, AudioMuxStrategy.TranscodeToAac, AudioMuxOutcome.TranscodedToAac)]
    public async Task ExportAsync_KeepAudioUsesCentralPolicyMuxesOnceAndEncodesVideoOnce(
        string? codec,
        AudioMuxStrategy expectedStrategy,
        AudioMuxOutcome expectedOutcome)
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-audio-aac.mp4");
        string output = Path.Combine(media.RootPath, "kept.mp4");
        RecordingEncoder encoder = new(writePartial: true);
        FakeAudioProbe probe = new(AudioProbeResult.Present(new AudioStreamInfo(codec, 48_000, 1, null, null)));
        FakeAudioMuxer muxer = new();
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 12),
            encoder,
            new RecordingPipeline(),
            probe,
            muxer);

        VideoExportResult result = await service.ExportAsync(Job(
            input,
            output,
            overwrite: false,
            audioMode: AudioExportMode.KeepAudio));

        Assert.True(result.Success, result.TechnicalDetail ?? result.UserMessage);
        Assert.Equal(expectedOutcome, result.AudioOutcome);
        Assert.Equal(1, encoder.Calls);
        Assert.Equal(1, muxer.Calls);
        Assert.Equal(expectedStrategy, muxer.LastJob!.Strategy);
        Assert.Equal(TimeSpan.FromSeconds(1.2), muxer.LastJob.VideoDuration);
        Assert.Equal(input, muxer.LastJob.SourceMediaPath);
        AssertNoExportArtifacts(media.RootPath);
    }

    [Fact]
    public async Task ExportAsync_AudioProbeFailureAfterEncodingDoesNotPublishVideoOnlyIntermediate()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-audio-aac.mp4");
        string output = Path.Combine(media.RootPath, "existing.mp4");
        await File.WriteAllBytesAsync(output, [8, 8, 8]);
        FakeAudioProbe probe = new(AudioProbeResult.Failure("friendly probe failure", "probe detail"));
        FakeAudioMuxer muxer = new();
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 2),
            new RecordingEncoder(writePartial: true),
            new RecordingPipeline(),
            probe,
            muxer);

        VideoExportResult result = await service.ExportAsync(Job(
            input,
            output,
            overwrite: true,
            audioMode: AudioExportMode.KeepAudio));

        Assert.Equal(VideoExportErrorCategory.AudioProbeFailed, result.ErrorCategory);
        Assert.Equal([8, 8, 8], await File.ReadAllBytesAsync(output));
        Assert.Equal(0, muxer.Calls);
        AssertNoExportArtifacts(media.RootPath);
    }

    [Fact]
    public async Task ExportAsync_AudioMuxFailurePreservesDestinationAndCleansBothPartials()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-audio-aac.mp4");
        string output = Path.Combine(media.RootPath, "existing.mp4");
        await File.WriteAllBytesAsync(output, [5, 5, 5]);
        FakeAudioMuxer muxer = new()
        {
            Result = AudioMuxResult.Failure(
                VideoExportErrorCategory.AudioStreamCopyFailed,
                "friendly copy failure",
                "copy detail")
        };
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 2),
            new RecordingEncoder(writePartial: true),
            new RecordingPipeline(),
            new FakeAudioProbe(AudioProbeResult.Present(new AudioStreamInfo("aac", 48_000, 1, null, null))),
            muxer);

        VideoExportResult result = await service.ExportAsync(Job(
            input,
            output,
            overwrite: true,
            audioMode: AudioExportMode.KeepAudio));

        Assert.Equal(VideoExportErrorCategory.AudioStreamCopyFailed, result.ErrorCategory);
        Assert.Equal([5, 5, 5], await File.ReadAllBytesAsync(output));
        AssertNoExportArtifacts(media.RootPath);
    }

    [Fact]
    public async Task ExportAsync_CancellationBeforeMuxDoesNotPublishVideoIntermediate()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-audio-aac.mp4");
        string output = Path.Combine(media.RootPath, "cancel-before-mux.mp4");
        FakeAudioProbe probe = new(AudioProbeResult.Present(new AudioStreamInfo("aac", 48_000, 1, null, null)))
        {
            Cancel = true
        };
        FakeAudioMuxer muxer = new();
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 2),
            new RecordingEncoder(writePartial: true),
            new RecordingPipeline(),
            probe,
            muxer);

        VideoExportResult result = await service.ExportAsync(Job(
            input,
            output,
            overwrite: false,
            audioMode: AudioExportMode.KeepAudio));

        Assert.Equal(VideoExportStatus.Cancelled, result.Status);
        Assert.Equal(0, muxer.Calls);
        Assert.False(File.Exists(output));
        AssertNoExportArtifacts(media.RootPath);
    }

    [Fact]
    public async Task ExportAsync_CancellationDuringMuxPreservesDestinationAndCleansBothPartials()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-audio-aac.mp4");
        string output = Path.Combine(media.RootPath, "existing.mp4");
        await File.WriteAllBytesAsync(output, [6, 6, 6]);
        FakeAudioMuxer muxer = new() { Cancel = true };
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 2),
            new RecordingEncoder(writePartial: true),
            new RecordingPipeline(),
            new FakeAudioProbe(AudioProbeResult.Present(new AudioStreamInfo("aac", 48_000, 1, null, null))),
            muxer);

        VideoExportResult result = await service.ExportAsync(Job(
            input,
            output,
            overwrite: true,
            audioMode: AudioExportMode.KeepAudio));

        Assert.Equal(VideoExportStatus.Cancelled, result.Status);
        Assert.Equal([6, 6, 6], await File.ReadAllBytesAsync(output));
        AssertNoExportArtifacts(media.RootPath);
    }

    [Fact]
    public async Task ExportAsync_CancellationAfterSuccessfulMuxDoesNotPublishOutput()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-audio-aac.mp4");
        string output = Path.Combine(media.RootPath, "cancelled.mp4");
        using CancellationTokenSource cancellation = new();
        FakeAudioMuxer muxer = new() { BeforeReturn = cancellation.Cancel };
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 2),
            new RecordingEncoder(writePartial: true),
            new RecordingPipeline(),
            new FakeAudioProbe(AudioProbeResult.Present(new AudioStreamInfo("aac", 48_000, 1, null, null))),
            muxer);

        VideoExportResult result = await service.ExportAsync(
            Job(input, output, overwrite: false, audioMode: AudioExportMode.KeepAudio),
            cancellation.Token);

        Assert.Equal(VideoExportStatus.Cancelled, result.Status);
        Assert.False(File.Exists(output));
        AssertNoExportArtifacts(media.RootPath);
    }

    private static StreamingVideoExportService CreateService(
        ISequentialVideoFrameReader reader,
        IVideoEncoder encoder,
        IImagePipelineService pipeline,
        IAudioStreamProbe? audioProbe = null,
        IAudioMuxer? audioMuxer = null) => new(
            reader,
            encoder,
            pipeline,
            new OutputDimensionResolver(),
            audioProbe ?? new FakeAudioProbe(AudioProbeResult.Absent()),
            new Mp4AudioCompatibilityPolicy(),
            audioMuxer ?? new FakeAudioMuxer());

    private static VideoExportJob Job(
        string input,
        string output,
        bool overwrite,
        int width = 64,
        int height = 48,
        AudioExportMode audioMode = AudioExportMode.RemoveAudio) => new(
            input,
            output,
            new VideoMetadata(
                input,
                Path.GetFileName(input),
                width,
                height,
                TimeSpan.FromSeconds(1.2),
                10,
                12,
                "MP4",
                new FileInfo(input).Length,
                "h264",
                true,
                "aac",
                48_000),
            ImageRenderSettings.Default,
            CharacterPresetOption.Default,
            overwrite,
            audioMode);

    private static void AssertNoExportArtifacts(string directory)
    {
        string[] forbiddenExtensions = [".partial", ".tmp", ".raw", ".png", ".jpg", ".jpeg"];
        Assert.DoesNotContain(
            Directory.EnumerateFiles(directory),
            path => forbiddenExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase));
    }

    private static VideoExportStage[] ConsecutiveStages(IReadOnlyList<VideoExportProgress> updates)
    {
        List<VideoExportStage> stages = [];
        foreach (VideoExportProgress update in updates)
        {
            if (stages.Count == 0 || stages[^1] != update.Stage)
            {
                stages.Add(update.Stage);
            }
        }

        return stages.ToArray();
    }

    private sealed class FakeFrameReader(int width, int height, int frameCount) : ISequentialVideoFrameReader
    {
        public Exception? Failure { get; init; }

        public IReadOnlyList<long>? SequenceNumbers { get; init; }

        public async IAsyncEnumerable<DecodedVideoFrame> ReadFramesAsync(
            SequentialVideoDecodeJob job,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            for (int frame = 0; frame < frameCount; frame++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] pixels = new byte[width * height * 4];
                for (int offset = 0; offset < pixels.Length; offset += 4)
                {
                    pixels[offset] = (byte)frame;
                    pixels[offset + 3] = 255;
                }

                long sequenceNumber = SequenceNumbers?[frame] ?? frame;
                yield return new DecodedVideoFrame(sequenceNumber, new DecodedImage(width, height, pixels));
                await Task.Yield();
            }
        }
    }

    private sealed class RecordingProgress<T> : IProgress<T>
    {
        public List<T> Values { get; } = [];

        public void Report(T value) => Values.Add(value);
    }

    private sealed class CallbackProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }

    private sealed class RecordingPipeline : IImagePipelineService
    {
        private int _activeRenders;

        public int MaximumConcurrentRenders { get; private set; }

        public Exception? Failure { get; init; }

        public TaskCompletionSource? RenderStarted { get; init; }

        public TaskCompletionSource? RenderRelease { get; init; }

        public Task<DecodedImage> DecodeAsync(
            string inputPath,
            ImageOrientation orientation,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public async Task<ImagePipelineResult> RenderAsync(
            DecodedImage source,
            ImageRenderSettings settings,
            CharacterPresetOption preset,
            CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            RenderStarted?.TrySetResult();
            if (RenderRelease is not null)
            {
                await RenderRelease.Task.WaitAsync(cancellationToken);
            }

            int active = Interlocked.Increment(ref _activeRenders);
            MaximumConcurrentRenders = Math.Max(MaximumConcurrentRenders, active);
            try
            {
                await Task.Yield();
                return new ImagePipelineResult(
                    new RenderedImage(source.Width, source.Height, source.RgbaPixels.ToArray()),
                    new SymbolGrid(1, 1, source.Dimensions),
                    new GlyphMetrics("Consolas", "Consolas", 10, 5, 10, 8, false));
            }
            finally
            {
                Interlocked.Decrement(ref _activeRenders);
            }
        }
    }

    private sealed class RecordingEncoder(bool writePartial) : IVideoEncoder
    {
        public VideoEncodingResult? Result { get; init; }

        public bool Cancel { get; init; }

        public Action? BeforeReturn { get; init; }

        public int Calls { get; private set; }

        public List<int> FrameMarkers { get; } = [];

        public List<PixelDimensions> FrameDimensions { get; } = [];

        public async Task<VideoEncodingResult> EncodeAsync(
            VideoEncodingJob job,
            IAsyncEnumerable<RenderedImage> frames,
            CancellationToken cancellationToken = default,
            IProgress<VideoEncodingProgress>? progress = null)
        {
            Calls++;
            if (writePartial)
            {
                await File.WriteAllBytesAsync(job.OutputPath, [1, 2, 3], cancellationToken);
            }

            if (Cancel)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            long count = 0;
            await foreach (RenderedImage frame in frames.WithCancellation(cancellationToken))
            {
                FrameMarkers.Add(frame.RgbaPixels.Span[0]);
                FrameDimensions.Add(frame.Dimensions);
                count++;
                progress?.Report(new VideoEncodingProgress(
                    VideoEncodingProgressStage.FrameAccepted,
                    count));
            }

            progress?.Report(new VideoEncodingProgress(
                VideoEncodingProgressStage.Finalizing,
                count));
            BeforeReturn?.Invoke();
            return Result ?? new VideoEncodingResult(true, count);
        }
    }

    private sealed class FakeAudioProbe(AudioProbeResult result) : IAudioStreamProbe
    {
        public bool Cancel { get; init; }

        public int Calls { get; private set; }

        public Task<AudioProbeResult> ProbeAsync(
            string inputPath,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            if (Cancel)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return Task.FromResult(result);
        }
    }

    private sealed class FakeAudioMuxer : IAudioMuxer
    {
        public AudioMuxResult? Result { get; init; }

        public bool Cancel { get; init; }

        public Action? BeforeReturn { get; init; }

        public int Calls { get; private set; }

        public AudioMuxJob? LastJob { get; private set; }

        public Task<AudioMuxResult> MuxAsync(
            AudioMuxJob job,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastJob = job;
            File.WriteAllBytes(job.OutputPath, [4, 5, 6]);
            if (Cancel)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            BeforeReturn?.Invoke();
            AudioMuxResult resolved = Result ?? AudioMuxResult.Completed(job.Strategy);
            return Task.FromResult(resolved);
        }
    }
}
