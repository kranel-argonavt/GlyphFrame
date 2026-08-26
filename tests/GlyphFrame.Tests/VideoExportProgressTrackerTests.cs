using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Tests;

public sealed class VideoExportProgressTrackerTests
{
    [Fact]
    public void DeterminateFrameProgress_IsFiniteClampedMonotonicAndNeverPrematurelyComplete()
    {
        TimeSpan elapsed = TimeSpan.FromSeconds(2);
        RecordingProgress progress = new();
        VideoExportProgressTracker tracker = new(progress, 10, () => elapsed);

        tracker.ReportStage(VideoExportStage.Preparing);
        tracker.ReportStage(VideoExportStage.ProcessingFrames);
        tracker.ReportFramesAccepted(2);
        tracker.ReportFramesAccepted(1);
        tracker.ReportFramesAccepted(long.MaxValue);

        Assert.All(progress.Values, value =>
        {
            Assert.True(value.Percentage is null || double.IsFinite(value.Percentage.Value));
            Assert.True(value.Percentage is null or >= 0 and < 100);
        });
        double[] percentages = progress.Values
            .Where(value => value.Percentage.HasValue)
            .Select(value => value.Percentage!.Value)
            .ToArray();
        Assert.Equal(percentages.OrderBy(value => value), percentages);
        Assert.Equal(VideoExportProgressTracker.MaximumIncompletePercentage, percentages[^1]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void InvalidOrUnavailableTotal_UsesHonestIndeterminateFrameProgress(long? totalFrames)
    {
        RecordingProgress progress = new();
        VideoExportProgressTracker tracker = new(progress, totalFrames, () => TimeSpan.FromSeconds(3));

        tracker.ReportStage(VideoExportStage.ProcessingFrames);
        tracker.ReportFramesAccepted(4);

        VideoExportProgress update = progress.Values[^1];
        Assert.Equal(4, update.ProcessedFrames);
        Assert.Null(update.TotalFrames);
        Assert.Null(update.Percentage);
        Assert.True(update.IsIndeterminate);
        Assert.Null(update.EstimatedRemaining);
    }

    [Fact]
    public void Eta_UsesCompletedFrameThroughputAndIsStable()
    {
        TimeSpan elapsed = TimeSpan.FromSeconds(4);
        RecordingProgress progress = new();
        VideoExportProgressTracker tracker = new(progress, 10, () => elapsed);

        tracker.ReportStage(VideoExportStage.ProcessingFrames);
        tracker.ReportFramesAccepted(4);

        VideoExportProgress update = progress.Values[^1];
        Assert.Equal(TimeSpan.FromSeconds(6), update.EstimatedRemaining);
        Assert.Equal(TimeSpan.FromSeconds(4), update.Elapsed);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 5)]
    [InlineData(5, 5)]
    [InlineData(6, 5)]
    public void Eta_IsUnavailableWithoutMeaningfulRemainingFrameProgress(long processed, long total)
    {
        RecordingProgress progress = new();
        VideoExportProgressTracker tracker = new(progress, total, () => TimeSpan.FromSeconds(5));

        tracker.ReportStage(VideoExportStage.ProcessingFrames);
        tracker.ReportFramesAccepted(processed);

        Assert.Null(progress.Values[^1].EstimatedRemaining);
    }

    [Fact]
    public void Eta_IsUnavailableBeforeMinimumElapsedTimeOrDuringIndeterminateStages()
    {
        TimeSpan elapsed = TimeSpan.FromMilliseconds(500);
        RecordingProgress progress = new();
        VideoExportProgressTracker tracker = new(progress, 10, () => elapsed);

        tracker.ReportStage(VideoExportStage.ProcessingFrames);
        tracker.ReportFramesAccepted(2);
        Assert.Null(progress.Values[^1].EstimatedRemaining);

        elapsed = TimeSpan.FromSeconds(2);
        tracker.ReportFramesAccepted(3);
        Assert.NotNull(progress.Values[^1].EstimatedRemaining);

        tracker.ReportStage(VideoExportStage.MergingAudio);
        Assert.Null(progress.Values[^1].EstimatedRemaining);
        Assert.True(progress.Values[^1].IsIndeterminate);
    }

    [Fact]
    public void NegativeElapsedTime_IsClampedAndCannotProduceNegativeRemainingTime()
    {
        RecordingProgress progress = new();
        VideoExportProgressTracker tracker = new(progress, 10, () => TimeSpan.FromSeconds(-2));

        tracker.ReportStage(VideoExportStage.ProcessingFrames);
        tracker.ReportFramesAccepted(3);

        Assert.Equal(TimeSpan.Zero, progress.Values[^1].Elapsed);
        Assert.Null(progress.Values[^1].EstimatedRemaining);
    }

    [Fact]
    public void EtaOverflow_IsSafelyClampedToFiniteNonNegativeTimeSpan()
    {
        RecordingProgress progress = new();
        VideoExportProgressTracker tracker = new(
            progress,
            long.MaxValue,
            () => TimeSpan.MaxValue);

        tracker.ReportStage(VideoExportStage.ProcessingFrames);
        tracker.ReportFramesAccepted(2);

        Assert.Equal(TimeSpan.MaxValue, progress.Values[^1].EstimatedRemaining);
        Assert.True(progress.Values[^1].EstimatedRemaining >= TimeSpan.Zero);
    }

    [Fact]
    public void Completed_IsTheOnlySnapshotThatReportsOneHundredPercent()
    {
        RecordingProgress progress = new();
        VideoExportProgressTracker tracker = new(progress, 2, () => TimeSpan.FromSeconds(2));

        tracker.ReportStage(VideoExportStage.ProcessingFrames);
        tracker.ReportFramesAccepted(2);
        tracker.ReportStage(VideoExportStage.Finalizing);
        tracker.Complete();

        Assert.Equal(100d, progress.Values[^1].Percentage);
        Assert.Equal(VideoExportStage.Completed, progress.Values[^1].Stage);
        Assert.False(progress.Values[^1].IsIndeterminate);
        Assert.DoesNotContain(progress.Values.Take(progress.Values.Count - 1), value => value.Percentage == 100d);
    }

    [Fact]
    public void CancellationIsTerminalAndRejectsLateProgress()
    {
        RecordingProgress progress = new();
        VideoExportProgressTracker tracker = new(progress, 10, () => TimeSpan.FromSeconds(1));
        tracker.ReportStage(VideoExportStage.ProcessingFrames);
        tracker.ReportFramesAccepted(1);

        tracker.ReportStage(VideoExportStage.Cancelling);
        int countAfterCancellation = progress.Values.Count;
        tracker.ReportFramesAccepted(2);
        tracker.ReportStage(VideoExportStage.Finalizing);
        tracker.Complete();

        Assert.Equal(countAfterCancellation, progress.Values.Count);
        Assert.Equal(VideoExportStage.Cancelling, progress.Values[^1].Stage);
        Assert.Null(progress.Values[^1].Percentage);
    }

    private sealed class RecordingProgress : IProgress<VideoExportProgress>
    {
        public List<VideoExportProgress> Values { get; } = [];

        public void Report(VideoExportProgress value) => Values.Add(value);
    }
}
