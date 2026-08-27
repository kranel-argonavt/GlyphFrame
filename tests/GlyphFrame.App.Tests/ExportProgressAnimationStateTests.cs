using GlyphFrame.App.Presentation;
using GlyphFrame.Core.Enums;

namespace GlyphFrame.App.Tests;

public sealed class ExportProgressAnimationStateTests
{
    [Fact]
    public void AnimationTimings_RemainWithinApprovedVisualRanges()
    {
        Assert.InRange(ExportAnimationTimings.RingRotation.TotalSeconds, 2.8, 3.2);
        Assert.InRange((ExportAnimationTimings.RingPulseHalfCycle * 2).TotalSeconds, 1.3, 1.6);
        Assert.InRange(ExportAnimationTimings.ProgressTransition.TotalMilliseconds, 180, 250);
        Assert.InRange(ExportAnimationTimings.StageCrossfade.TotalMilliseconds, 120, 180);
        Assert.InRange(ExportAnimationTimings.CompletionTransition.TotalMilliseconds, 250, 350);
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(double.NegativeInfinity, 0)]
    [InlineData(double.PositiveInfinity, 0)]
    [InlineData(-1, 0)]
    [InlineData(42.5, 42.5)]
    [InlineData(200, 99.9)]
    public void Update_ClampsActiveProgressToTruthfulFiniteRange(double source, double expected)
    {
        ExportProgressAnimationState state = new();

        state.Update(1, source, false, VideoExportStage.ProcessingFrames);

        Assert.Equal(expected, state.TargetValue);
    }

    [Fact]
    public void Update_DoesNotMoveBackwardsWithinOneOperation()
    {
        ExportProgressAnimationState state = new();

        state.Update(1, 72, false, VideoExportStage.ProcessingFrames);
        state.Update(1, 35, false, VideoExportStage.ProcessingFrames);

        Assert.Equal(72, state.TargetValue);
    }

    [Fact]
    public void Update_AcceptsOneHundredOnlyForCompletedStage()
    {
        ExportProgressAnimationState state = new();

        state.Update(1, 100, false, VideoExportStage.Finalizing);
        Assert.Equal(99.9, state.TargetValue);

        state.Update(1, 100, false, VideoExportStage.Completed);
        Assert.Equal(100, state.TargetValue);
    }

    [Fact]
    public void Update_IndeterminateModeRetainsLatestTruthfulTarget()
    {
        ExportProgressAnimationState state = new();

        state.Update(1, 60, false, VideoExportStage.ProcessingFrames);
        state.Update(1, 60, true, VideoExportStage.MergingAudio);
        state.Update(1, 40, false, VideoExportStage.Finalizing);

        Assert.False(state.IsIndeterminate);
        Assert.Equal(60, state.TargetValue);
    }

    [Fact]
    public void Update_NewOperationGenerationResetsProgressToZero()
    {
        ExportProgressAnimationState state = new();
        state.Update(1, 100, false, VideoExportStage.Completed);

        state.Update(2, 0, true, VideoExportStage.Preparing);

        Assert.Equal(0, state.TargetValue);
        Assert.True(state.IsIndeterminate);
    }
}
