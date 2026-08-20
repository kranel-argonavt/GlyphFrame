using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Infrastructure.Ffmpeg;
using GlyphFrame.Infrastructure.Media;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class FfmpegSequentialVideoFrameReaderTests
{
    private static readonly PixelDimensions Dimensions = new(2, 1);

    [Fact]
    public async Task ReadFramesAsync_MissingBundledFfmpegReturnsDecoderUnavailable()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        FakeStreamingProcessFactory factory = new();
        FfmpegSequentialVideoFrameReader reader = CreateReader(factory, ffmpegExists: false);

        VideoExportException exception = await Assert.ThrowsAsync<VideoExportException>(() =>
            CollectAsync(reader.ReadFramesAsync(new SequentialVideoDecodeJob(input, Dimensions))));

        Assert.Equal(VideoExportErrorCategory.DecoderUnavailable, exception.Category);
        Assert.Contains("unavailable", exception.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, factory.StartCalls);
    }

    [Fact]
    public async Task ReadFramesAsync_ProcessStartupFailureReturnsFriendlyDecoderUnavailable()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        FakeStreamingProcessFactory factory = new()
        {
            StartException = new InvalidOperationException("controlled decoder start failure")
        };
        FfmpegSequentialVideoFrameReader reader = CreateReader(factory);

        VideoExportException exception = await Assert.ThrowsAsync<VideoExportException>(() =>
            CollectAsync(reader.ReadFramesAsync(new SequentialVideoDecodeJob(input, Dimensions))));

        Assert.Equal(VideoExportErrorCategory.DecoderUnavailable, exception.Category);
        Assert.DoesNotContain("controlled", exception.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("controlled decoder start failure", exception.TechnicalDetail);
    }

    [Fact]
    public async Task ReadFramesAsync_ChunkedStdoutYieldsOwnedFramesInSequence()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        byte[] first = FrameBytes(10);
        byte[] second = FrameBytes(20);
        ChunkedReadStream output = new(first.Concat(second).ToArray(), maximumChunkSize: 3);
        FakeStreamingProcessSession session = new() { StandardOutput = output };
        FfmpegSequentialVideoFrameReader reader = CreateReader(
            new FakeStreamingProcessFactory { Session = session });

        List<DecodedVideoFrame> frames = await CollectAsync(
            reader.ReadFramesAsync(new SequentialVideoDecodeJob(input, Dimensions)));

        Assert.Equal([0L, 1L], frames.Select(frame => frame.SequenceNumber));
        Assert.Equal(first, frames[0].Frame.RgbaPixels.ToArray());
        Assert.Equal(second, frames[1].Frame.RgbaPixels.ToArray());
        Assert.All(frames, frame => Assert.Equal(Dimensions, frame.Frame.Dimensions));
        Assert.Equal(1, output.DisposeCalls);
        Assert.Equal(1, session.DisposeCalls);
        Assert.Equal(first, frames[0].Frame.RgbaPixels.ToArray());
    }

    [Fact]
    public async Task ReadFramesAsync_TruncatedFinalFrameThrowsSpecificFailureWithoutPublishingIt()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        byte[] bytes = FrameBytes(1).Concat(new byte[] { 9, 8, 7 }).ToArray();
        FakeStreamingProcessSession session = new()
        {
            StandardOutput = new ChunkedReadStream(bytes, maximumChunkSize: 2)
        };
        FfmpegSequentialVideoFrameReader reader = CreateReader(
            new FakeStreamingProcessFactory { Session = session });
        List<DecodedVideoFrame> published = [];

        VideoExportException exception = await Assert.ThrowsAsync<VideoExportException>(async () =>
        {
            await foreach (DecodedVideoFrame frame in reader.ReadFramesAsync(
                new SequentialVideoDecodeJob(input, Dimensions)))
            {
                published.Add(frame);
            }
        });

        Assert.Equal(VideoExportErrorCategory.TruncatedFrame, exception.Category);
        Assert.Single(published);
        Assert.Equal(1, session.KillCalls);
        Assert.Equal(1, session.DisposeCalls);
    }

    [Fact]
    public async Task ReadFramesAsync_NonZeroExitReturnsDecodeFailureWithBoundedStderr()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string diagnostics = new('d', StreamingProcessSupport.MaximumErrorCharacters + 200);
        FakeStreamingProcessSession session = new()
        {
            StandardOutput = new ChunkedReadStream(FrameBytes(1)),
            StandardError = new TrackingStringReader(diagnostics),
            ExitCode = 23
        };
        FfmpegSequentialVideoFrameReader reader = CreateReader(
            new FakeStreamingProcessFactory { Session = session });

        VideoExportException exception = await Assert.ThrowsAsync<VideoExportException>(() =>
            CollectAsync(reader.ReadFramesAsync(new SequentialVideoDecodeJob(input, Dimensions))));

        Assert.Equal(VideoExportErrorCategory.DecodeFailed, exception.Category);
        Assert.Contains("exit code: 23", exception.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
        Assert.True(exception.TechnicalDetail!.Length < diagnostics.Length + 100);
        Assert.Equal(1, session.DisposeCalls);
    }

    [Fact]
    public async Task ReadFramesAsync_ZeroFramesReturnsFriendlyDecodeFailure()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        FakeStreamingProcessSession session = new();
        FfmpegSequentialVideoFrameReader reader = CreateReader(
            new FakeStreamingProcessFactory { Session = session });

        VideoExportException exception = await Assert.ThrowsAsync<VideoExportException>(() =>
            CollectAsync(reader.ReadFramesAsync(new SequentialVideoDecodeJob(input, Dimensions))));

        Assert.Equal(VideoExportErrorCategory.DecodeFailed, exception.Category);
        Assert.Contains("decodable video stream", exception.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, session.DisposeCalls);
    }

    [Fact]
    public async Task ReadFramesAsync_CancellationDuringStdoutReadTerminatesTreeAndPublishesNoFrame()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        BlockingReadStream output = new();
        FakeStreamingProcessSession session = new() { StandardOutput = output };
        FfmpegSequentialVideoFrameReader reader = CreateReader(
            new FakeStreamingProcessFactory { Session = session });
        using CancellationTokenSource cancellation = new();
        IAsyncEnumerator<DecodedVideoFrame> enumerator = reader
            .ReadFramesAsync(new SequentialVideoDecodeJob(input, Dimensions), cancellation.Token)
            .GetAsyncEnumerator(cancellation.Token);

        Task<bool> moveNext = enumerator.MoveNextAsync().AsTask();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => moveNext);
        await enumerator.DisposeAsync();
        Assert.Equal(1, session.KillCalls);
        Assert.Equal(1, output.DisposeCalls);
        Assert.Equal(1, session.DisposeCalls);
    }

    [Fact]
    public async Task ReadFramesAsync_EarlyEnumeratorDisposalTerminatesDecoderAndRetainsFrameOwnership()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        byte[] first = FrameBytes(42);
        FakeStreamingProcessSession session = new()
        {
            StandardOutput = new ChunkedReadStream(first.Concat(FrameBytes(84)).ToArray())
        };
        FfmpegSequentialVideoFrameReader reader = CreateReader(
            new FakeStreamingProcessFactory { Session = session });
        IAsyncEnumerator<DecodedVideoFrame> enumerator = reader
            .ReadFramesAsync(new SequentialVideoDecodeJob(input, Dimensions))
            .GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        DecodedVideoFrame retained = enumerator.Current;
        await enumerator.DisposeAsync();

        Assert.Equal(first, retained.Frame.RgbaPixels.ToArray());
        Assert.Equal(1, session.KillCalls);
        Assert.Equal(1, session.DisposeCalls);
        using FileStream exclusive = new(input, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.True(exclusive.CanRead);
    }

    [Fact]
    public async Task ReadFramesAsync_UnicodePathRemainsOneArgumentListEntry()
    {
        using TemporaryMediaDirectory media = new("відео 日本語 测试");
        string input = media.CopyFixture("video-preview.mp4", "тестове відео-日本語-测试.mp4");
        FakeStreamingProcessSession session = new()
        {
            StandardOutput = new ChunkedReadStream(FrameBytes(1))
        };
        FakeStreamingProcessFactory factory = new() { Session = session };
        FfmpegSequentialVideoFrameReader reader = CreateReader(factory);

        await CollectAsync(reader.ReadFramesAsync(new SequentialVideoDecodeJob(input, Dimensions)));

        string resolvedInput = Path.GetFullPath(input);
        Assert.Contains(resolvedInput, factory.Arguments!);
        Assert.Equal(1, factory.Arguments!.Count(argument => argument == resolvedInput));
        Assert.True(factory.RedirectStandardOutput);
        Assert.False(factory.RedirectStandardInput);
    }

    private static FfmpegSequentialVideoFrameReader CreateReader(
        FakeStreamingProcessFactory factory,
        bool ffmpegExists = true) => new(
            new StaticLocator(CreateLocation(ffmpegExists)),
            factory);

    private static byte[] FrameBytes(byte marker) =>
        [marker, 1, 2, 255, marker, 3, 4, 255];

    private static async Task<List<DecodedVideoFrame>> CollectAsync(
        IAsyncEnumerable<DecodedVideoFrame> frames)
    {
        List<DecodedVideoFrame> result = [];
        await foreach (DecodedVideoFrame frame in frames)
        {
            result.Add(frame);
        }

        return result;
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
