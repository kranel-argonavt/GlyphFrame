using GlyphFrame.App.Services;

namespace GlyphFrame.App.Tests;

public sealed class VideoPlaybackCoordinatorTests
{
    [Theory]
    [InlineData(4d, 10d, 30d, 4d)]
    [InlineData(8d, 12d, 24d, 8d)]
    [InlineData(8d, 5d, 10d, 4.9d)]
    [InlineData(5d, 5d, 25d, 4.96d)]
    [InlineData(-2d, 5d, 25d, 0d)]
    public void TimestampTransfer_ClampsUsingTargetDurationAndFrameInterval(
        double requestedSeconds,
        double targetSeconds,
        double framesPerSecond,
        double expectedSeconds)
    {
        PlaybackTimestampTransfer transfer = PlaybackTimestampTransfer.Create(
            TimeSpan.FromSeconds(requestedSeconds),
            TimeSpan.FromSeconds(targetSeconds),
            framesPerSecond);

        Assert.Equal(expectedSeconds, transfer.TargetTimestamp.TotalSeconds, 3);
        Assert.True(transfer.TargetTimestamp >= TimeSpan.Zero);
        Assert.Equal(TimeSpan.FromSeconds(targetSeconds), transfer.TargetDuration);
        Assert.NotNull(transfer.TargetFrameInterval);
        Assert.NotNull(transfer.ValidationTolerance);
        Assert.True(transfer.ValidationTolerance >= TimeSpan.FromMilliseconds(250));
        Assert.True(transfer.ValidationTolerance >= transfer.TargetFrameInterval + transfer.TargetFrameInterval);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0d)]
    [InlineData(-1d)]
    public void TimestampTransfer_UnavailableDurationUsesZero(double? targetSeconds)
    {
        TimeSpan? duration = targetSeconds is double seconds ? TimeSpan.FromSeconds(seconds) : null;

        PlaybackTimestampTransfer transfer = PlaybackTimestampTransfer.Create(
            TimeSpan.FromSeconds(4),
            duration,
            targetFramesPerSecond: null);

        Assert.Equal(TimeSpan.Zero, transfer.TargetTimestamp);
        Assert.Null(transfer.TargetDuration);
        Assert.Null(transfer.TargetFrameInterval);
        Assert.Null(transfer.ValidationTolerance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0d)]
    [InlineData(-30d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void TimestampTransfer_UntrustworthyFpsDoesNotClaimTolerance(double? framesPerSecond)
    {
        PlaybackTimestampTransfer transfer = PlaybackTimestampTransfer.Create(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5),
            framesPerSecond);

        Assert.Equal(TimeSpan.FromSeconds(5), transfer.TargetTimestamp);
        Assert.Null(transfer.TargetFrameInterval);
        Assert.Null(transfer.ValidationTolerance);
    }
}
