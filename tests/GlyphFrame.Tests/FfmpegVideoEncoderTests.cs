using System.Runtime.CompilerServices;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Infrastructure.Ffmpeg;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class FfmpegVideoEncoderTests
{
    private static readonly PixelDimensions Dimensions = new(2, 1);

    [Fact]
    public async Task EncodeAsync_MissingBundledFfmpegReturnsFriendlyUnavailableFailure()
    {
        FakeStreamingProcessFactory factory = new();
        FfmpegVideoEncoder encoder = CreateEncoder(factory, ffmpegExists: false);

        VideoEncodingResult result = await encoder.EncodeAsync(Job(), Frames(Frame(1)));

        Assert.False(result.Success);
        Assert.Equal(VideoExportErrorCategory.EncoderStartFailed, result.ErrorCategory);
        Assert.Contains("unavailable", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.TechnicalDetail);
        Assert.Equal(0, factory.StartCalls);
    }

    [Fact]
    public async Task EncodeAsync_ProcessStartupFailureReturnsFriendlyFailureAndTechnicalDetail()
    {
        FakeStreamingProcessFactory factory = new()
        {
            StartException = new InvalidOperationException("controlled start failure")
        };
        FfmpegVideoEncoder encoder = CreateEncoder(factory);

        VideoEncodingResult result = await encoder.EncodeAsync(Job(), Frames(Frame(1)));

        Assert.Equal(VideoExportErrorCategory.EncoderStartFailed, result.ErrorCategory);
        Assert.DoesNotContain("controlled", result.UserMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("controlled start failure", result.TechnicalDetail);
    }

    [Fact]
    public async Task EncodeAsync_WritesEveryFrameInExactSequenceAndAwaitsBackpressure()
    {
        TaskCompletionSource firstWriteEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirstWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondFrameRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int writes = 0;
        TrackingWriteStream input = new()
        {
            WriteHandler = async (_, cancellationToken) =>
            {
                if (Interlocked.Increment(ref writes) == 1)
                {
                    firstWriteEntered.SetResult();
                    await releaseFirstWrite.Task.WaitAsync(cancellationToken);
                }
            }
        };
        FakeStreamingProcessSession session = new() { StandardInput = input };
        FfmpegVideoEncoder encoder = CreateEncoder(new FakeStreamingProcessFactory { Session = session });

        Task<VideoEncodingResult> encoding = encoder.EncodeAsync(Job(), ControlledFrames());
        await firstWriteEntered.Task;
        Assert.False(secondFrameRequested.Task.IsCompleted);
        releaseFirstWrite.SetResult();
        VideoEncodingResult result = await encoding;

        Assert.True(result.Success);
        Assert.Equal(2, result.FramesWritten);
        Assert.Equal(FrameBytes(1).Concat(FrameBytes(2)), input.Bytes);
        Assert.Equal(2, input.WriteCalls);

        async IAsyncEnumerable<RenderedImage> ControlledFrames(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return Frame(1);
            cancellationToken.ThrowIfCancellationRequested();
            secondFrameRequested.SetResult();
            yield return Frame(2);
            await Task.Yield();
        }
    }

    [Fact]
    public async Task EncodeAsync_IncorrectDimensionsAreRejectedBeforeWriting()
    {
        TrackingWriteStream input = new();
        FakeStreamingProcessSession session = new() { StandardInput = input };
        FfmpegVideoEncoder encoder = CreateEncoder(new FakeStreamingProcessFactory { Session = session });

        VideoExportException exception = await Assert.ThrowsAsync<VideoExportException>(() =>
            encoder.EncodeAsync(Job(), Frames(new RenderedImage(4, 1, new byte[16]))));

        Assert.Equal(VideoExportErrorCategory.EncoderWriteFailed, exception.Category);
        Assert.Equal(0, input.WriteCalls);
        Assert.Equal(1, session.KillCalls);
    }

    [Fact]
    public void ValidateFramePayload_IncorrectRgbaByteLengthIsRejected()
    {
        VideoExportException exception = Assert.Throws<VideoExportException>(() =>
            FfmpegVideoEncoder.ValidateFramePayload(Dimensions, new byte[7], Dimensions, 4));

        Assert.Equal(VideoExportErrorCategory.EncoderWriteFailed, exception.Category);
        Assert.Contains("expected 8 bytes", exception.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EncodeAsync_ZeroFramesReturnsFailureAndCompletesInput()
    {
        FakeStreamingProcessSession session = new();
        FfmpegVideoEncoder encoder = CreateEncoder(new FakeStreamingProcessFactory { Session = session });

        VideoEncodingResult result = await encoder.EncodeAsync(Job(), Frames());

        Assert.Equal(VideoExportErrorCategory.EncoderFailed, result.ErrorCategory);
        Assert.Contains("did not produce", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, session.CompleteStandardInputCalls);
        Assert.Equal(1, session.DisposeCalls);
    }

    [Fact]
    public async Task EncodeAsync_BrokenStdinTerminatesTreeAndReturnsWriteFailure()
    {
        TrackingWriteStream input = new()
        {
            WriteHandler = (_, _) => throw new IOException("controlled broken pipe")
        };
        FakeStreamingProcessSession session = new() { StandardInput = input };
        FfmpegVideoEncoder encoder = CreateEncoder(new FakeStreamingProcessFactory { Session = session });

        VideoEncodingResult result = await encoder.EncodeAsync(Job(), Frames(Frame(1)));

        Assert.Equal(VideoExportErrorCategory.EncoderWriteFailed, result.ErrorCategory);
        Assert.Contains("stream", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("controlled broken pipe", result.TechnicalDetail);
        Assert.Equal(1, session.KillCalls);
        Assert.True(session.HasExited);
        Assert.Equal(1, session.DisposeCalls);
    }

    [Fact]
    public async Task EncodeAsync_NonZeroExitPreservesBoundedStderrAndDisposesResources()
    {
        string diagnostics = new('x', StreamingProcessSupport.MaximumErrorCharacters + 100);
        TrackingWriteStream input = new();
        TrackingStringReader error = new(diagnostics);
        FakeStreamingProcessSession session = new()
        {
            StandardInput = input,
            StandardError = error,
            ExitCode = 7
        };
        FfmpegVideoEncoder encoder = CreateEncoder(new FakeStreamingProcessFactory { Session = session });

        VideoEncodingResult result = await encoder.EncodeAsync(Job(), Frames(Frame(1)));

        Assert.Equal(VideoExportErrorCategory.EncoderFailed, result.ErrorCategory);
        Assert.Contains("exit code: 7", result.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.TechnicalDetail!.Length < diagnostics.Length + 100);
        Assert.Equal(1, input.DisposeCalls);
        Assert.Equal(1, error.DisposeCalls);
        Assert.Equal(1, session.DisposeCalls);
    }

    [Fact]
    public async Task EncodeAsync_FinalizationTimeoutTerminatesProcessTree()
    {
        FakeStreamingProcessSession session = new()
        {
            WaitForExitHandler = cancellationToken =>
                Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
        };
        FfmpegVideoEncoder encoder = CreateEncoder(
            new FakeStreamingProcessFactory { Session = session },
            finalizationTimeout: TimeSpan.FromMilliseconds(25));

        VideoEncodingResult result = await encoder.EncodeAsync(Job(), Frames(Frame(1)));

        Assert.Equal(VideoExportErrorCategory.EncoderFailed, result.ErrorCategory);
        Assert.Contains("did not finish in time", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, session.KillCalls);
        Assert.Equal(1, session.DisposeCalls);
    }

    [Fact]
    public async Task EncodeAsync_CancellationDuringWriteTerminatesTreeAndReleasesResources()
    {
        TaskCompletionSource writeEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TrackingWriteStream input = new()
        {
            WriteHandler = async (_, cancellationToken) =>
            {
                writeEntered.SetResult();
                TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                await completion.Task.WaitAsync(cancellationToken);
            }
        };
        FakeStreamingProcessSession session = new() { StandardInput = input };
        FfmpegVideoEncoder encoder = CreateEncoder(new FakeStreamingProcessFactory { Session = session });
        using CancellationTokenSource cancellation = new();

        Task<VideoEncodingResult> encoding = encoder.EncodeAsync(Job(), Frames(Frame(1)), cancellation.Token);
        await writeEntered.Task;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => encoding);
        Assert.Equal(1, session.KillCalls);
        Assert.Equal(1, input.DisposeCalls);
        Assert.Equal(1, session.DisposeCalls);
    }

    [Fact]
    public async Task EncodeAsync_SuccessCompletesInputOnceAwaitsExitAndDoesNotKillExitedProcess()
    {
        FakeStreamingProcessSession session = new();
        FfmpegVideoEncoder encoder = CreateEncoder(new FakeStreamingProcessFactory { Session = session });

        VideoEncodingResult result = await encoder.EncodeAsync(Job(), Frames(Frame(1), Frame(2)));

        Assert.True(result.Success);
        Assert.Equal(2, result.FramesWritten);
        Assert.Equal(1, session.CompleteStandardInputCalls);
        Assert.Equal(1, session.WaitForExitCalls);
        Assert.Equal(0, session.KillCalls);
        Assert.Equal(1, session.DisposeCalls);
    }

    private static FfmpegVideoEncoder CreateEncoder(
        FakeStreamingProcessFactory factory,
        bool ffmpegExists = true,
        TimeSpan? finalizationTimeout = null) => new(
            new StaticLocator(CreateLocation(ffmpegExists)),
            factory,
            finalizationTimeout ?? TimeSpan.FromSeconds(1));

    private static VideoEncodingJob Job() => new("output.mp4", Dimensions, 10);

    private static RenderedImage Frame(byte marker) => new(2, 1, FrameBytes(marker));

    private static byte[] FrameBytes(byte marker) =>
        [marker, 1, 2, 255, marker, 3, 4, 255];

    private static async IAsyncEnumerable<RenderedImage> Frames(params RenderedImage[] frames)
    {
        foreach (RenderedImage frame in frames)
        {
            yield return frame;
        }

        await Task.Yield();
    }

    private static BundledFfmpegLocation CreateLocation(bool exists)
    {
        string tools = Path.Combine("application", "Tools", "ffmpeg");
        return new BundledFfmpegLocation(
            "application",
            tools,
            new BundledToolPath(FfmpegToolKind.Ffmpeg, Path.Combine(tools, "ffmpeg.exe"), exists, false),
            new BundledToolPath(FfmpegToolKind.Ffprobe, Path.Combine(tools, "ffprobe.exe"), exists, false));
    }

    private sealed class StaticLocator(BundledFfmpegLocation location) : IBundledFfmpegLocator
    {
        public BundledFfmpegLocation Locate() => location;
    }
}
