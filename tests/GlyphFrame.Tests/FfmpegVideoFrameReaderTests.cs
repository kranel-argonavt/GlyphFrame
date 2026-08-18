using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Infrastructure.Ffmpeg;
using GlyphFrame.Infrastructure.Media;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class FfmpegVideoFrameReaderTests
{
    [Theory]
    [InlineData("video-preview.mp4")]
    [InlineData("video-preview.mov")]
    [InlineData("video-preview.mkv")]
    [InlineData("video-preview.avi")]
    public async Task ReadAsync_DecodesEverySupportedContainerThroughBundledFfmpeg(string fixtureName)
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture(fixtureName);
        FfmpegVideoFrameReader reader = CreateRealReader();

        VideoFrameReadResult result = await reader.ReadAsync(path, 0.5d, FixtureMetadata(path));

        Assert.True(result.Success, result.TechnicalDetail ?? result.UserMessage);
        Assert.Equal(64, result.Frame!.Width);
        Assert.Equal(48, result.Frame.Height);
        Assert.Equal(64 * 48 * 4, result.Frame.RgbaPixels.Length);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(0.5d)]
    [InlineData(1d)]
    public async Task ReadAsync_DecodesStartMiddleAndFinalAvailableFrame(double position)
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("video-preview.mp4");

        VideoFrameReadResult result = await CreateRealReader().ReadAsync(path, position, FixtureMetadata(path));

        Assert.True(result.Success, result.TechnicalDetail ?? result.UserMessage);
        Assert.Equal(position, result.RequestedNormalizedPosition);
        Assert.Null(result.ActualNormalizedPosition);
        Assert.Equal(64 * 48 * 4, result.Frame!.RgbaPixels.Length);
    }

    [Theory]
    [InlineData(-2d, 0d)]
    [InlineData(2d, 1d)]
    [InlineData(double.NaN, 0d)]
    [InlineData(double.PositiveInfinity, 0d)]
    public async Task ReadAsync_ClampsInvalidPositions(double requested, double expected)
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("video-preview.mp4");

        VideoFrameReadResult result = await CreateRealReader().ReadAsync(path, requested, FixtureMetadata(path));

        Assert.True(result.Success, result.TechnicalDetail ?? result.UserMessage);
        Assert.Equal(expected, result.RequestedNormalizedPosition);
    }

    [Fact]
    public async Task ReadAsync_ReturnsOwnedStraightRgbaWithCorrectChannelsAndOpaqueAlpha()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("video-preview.mp4");

        VideoFrameReadResult result = await CreateRealReader().ReadAsync(path, 0d, FixtureMetadata(path));

        Assert.True(result.Success, result.TechnicalDetail ?? result.UserMessage);
        byte[] owned = result.Frame!.RgbaPixels.ToArray();
        int center = ((24 * result.Frame.Width) + 32) * 4;
        Assert.InRange(owned[center], 245, 255);
        Assert.InRange(owned[center + 1], 0, 8);
        Assert.InRange(owned[center + 2], 0, 8);
        Assert.Equal(255, owned[center + 3]);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        Assert.InRange(owned[center], 245, 255);
    }

    [Fact]
    public async Task ReadAsync_DistinctPositionsReturnDistinctFrames()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("video-preview.mov");
        FfmpegVideoFrameReader reader = CreateRealReader();

        VideoFrameReadResult start = await reader.ReadAsync(path, 0d, FixtureMetadata(path));
        VideoFrameReadResult late = await reader.ReadAsync(path, 0.8d, FixtureMetadata(path));

        Assert.True(start.Success, start.TechnicalDetail ?? start.UserMessage);
        Assert.True(late.Success, late.TechnicalDetail ?? late.UserMessage);
        Assert.False(start.Frame!.RgbaPixels.Span.SequenceEqual(late.Frame!.RgbaPixels.Span));
        int center = ((24 * late.Frame.Width) + 32) * 4;
        Assert.InRange(late.Frame.RgbaPixels.Span[center], 0, 8);
        Assert.InRange(late.Frame.RgbaPixels.Span[center + 2], 245, 255);
    }

    [Theory]
    [InlineData("video-preview.mp4", "тестове відео.MP4")]
    [InlineData("video-preview.mov", "відео-日本語-测试.MOV")]
    [InlineData("video-preview.mkv", "змішане відео 日本語.MKV")]
    [InlineData("video-preview.avi", "відео з пробілами.AVI")]
    public async Task ReadAsync_DecodesFromActualUnicodeDirectoryAndFilename(
        string fixtureName,
        string unicodeFileName)
    {
        using TemporaryMediaDirectory media = new("Кирилиця та 日本語");
        string path = media.CopyFixture(fixtureName, unicodeFileName);
        Assert.Contains(path, character => character > 127);
        Assert.Contains(' ', path);

        VideoFrameReadResult result = await CreateRealReader().ReadAsync(path, 0.5d, FixtureMetadata(path));

        Assert.True(result.Success, result.TechnicalDetail ?? result.UserMessage);
        Assert.Equal(64, result.Frame!.Width);
        using FileStream exclusive = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.CanWrite);
    }

    [Fact]
    public async Task ReadAsync_ReleasesFileAfterRepeatedReads()
    {
        using TemporaryMediaDirectory media = new("повторні читання");
        string path = media.CopyFixture("video-preview.mp4", "повтор.mp4");
        FfmpegVideoFrameReader reader = CreateRealReader();

        for (int index = 0; index < 4; index++)
        {
            VideoFrameReadResult result = await reader.ReadAsync(path, index / 3d, FixtureMetadata(path));
            Assert.True(result.Success, result.TechnicalDetail ?? result.UserMessage);
            using FileStream exclusive = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.True(exclusive.CanWrite);
        }
    }

    [Fact]
    public async Task ReadAsync_UsesArgumentListAndPreservesUnicodePathAsOneArgument()
    {
        using TemporaryMediaDirectory media = new("Unicode каталог");
        string path = media.CopyFixture("video-preview.mov", "тестове відео.MOV");
        RecordingBinaryRunner runner = new(SuccessfulFrame(64, 48));
        FfmpegVideoFrameReader reader = CreateFakeReader(runner);

        VideoFrameReadResult result = await reader.ReadAsync(path, 0.5d, FixtureMetadata(path));

        Assert.True(result.Success);
        Assert.Contains(path, runner.LastRequest!.Arguments);
        Assert.Equal(1, runner.LastRequest.Arguments.Count(argument => argument == path));
        Assert.Contains("pipe:1", runner.LastRequest.Arguments);
        Assert.Contains("rgba", runner.LastRequest.Arguments);
    }

    [Fact]
    public async Task ReadAsync_UsesMetadataReaderWhenContextIsAbsent()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("video-preview.mp4");
        RecordingMetadataReader metadata = new(FixtureMetadata(path));
        RecordingBinaryRunner runner = new(SuccessfulFrame(64, 48));
        FfmpegVideoFrameReader reader = CreateFakeReader(runner, metadata);

        VideoFrameReadResult result = await reader.ReadAsync(path, 0.5d);

        Assert.True(result.Success);
        Assert.Equal(path, metadata.LastPath);
    }

    [Fact]
    public async Task ReadAsync_UsesPostRotationDisplayDimensions()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("video-preview.mov");
        VideoMetadata rotated = FixtureMetadata(path) with { Width = 2, Height = 1, RotationDegrees = 90 };
        RecordingBinaryRunner runner = new(SuccessfulFrame(1, 2));

        VideoFrameReadResult result = await CreateFakeReader(runner).ReadAsync(path, 0.5d, rotated);

        Assert.True(result.Success);
        Assert.Equal(1, result.Frame!.Width);
        Assert.Equal(2, result.Frame.Height);
        Assert.Equal(8, runner.LastRequest!.MaximumStandardOutputBytes);
    }

    [Fact]
    public async Task ReadAsync_RealRotatedMovUsesFfprobeMetadataAndFfmpegAutorotation()
    {
        string path = FixturePath("video-preview-rotated.mov");
        string fixtureDirectory = Path.GetDirectoryName(path)!;
        string[] filesBefore = Directory.GetFiles(fixtureDirectory)
            .Select(file => Path.GetFileName(file)!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        BundledFfmpegLocator locator = new(RepositoryRoot);
        ProcessRunner processRunner = new();
        using FfmpegValidationService validation = new(locator, processRunner);
        FfprobeVideoMetadataReader metadataReader = new(
            new MetadataExtractorVideoMetadataReader(),
            validation,
            processRunner);

        VideoMetadata metadata = await metadataReader.ReadAsync(path);
        FfmpegVideoFrameReader frameReader = new(locator, new BinaryProcessRunner(), metadataReader);
        VideoFrameReadResult result = await frameReader.ReadAsync(path, 0.5d, metadata);

        Assert.Equal(320, metadata.Width);
        Assert.Equal(240, metadata.Height);
        Assert.Equal(90, metadata.RotationDegrees);
        Assert.Equal(90, metadata.NormalizedRotationDegrees);
        Assert.Equal(240, metadata.DisplayWidth);
        Assert.Equal(320, metadata.DisplayHeight);
        Assert.Equal(10d, metadata.Fps);
        Assert.Equal(12, metadata.FrameCount);
        Assert.Equal("h264", metadata.VideoCodec);
        Assert.True(result.Success, result.TechnicalDetail ?? result.UserMessage);
        Assert.Equal(0.5d, result.RequestedNormalizedPosition);
        Assert.Equal(240, result.Frame!.Width);
        Assert.Equal(320, result.Frame.Height);
        Assert.Equal(240 * 320 * 4, result.Frame.RgbaPixels.Length);

        AssertPixel(result.Frame, 60, 80, red: 0, green: 255, blue: 0);
        AssertPixel(result.Frame, 180, 80, red: 255, green: 255, blue: 0);
        AssertPixel(result.Frame, 60, 240, red: 255, green: 0, blue: 0);
        AssertPixel(result.Frame, 180, 240, red: 0, green: 0, blue: 255);

        byte[] ownedFrame = result.Frame.RgbaPixels.ToArray();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Assert.Equal(255, ownedFrame[((80 * 240) + 60) * 4 + 3]);

        using (FileStream exclusive = new(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.True(exclusive.CanRead);
        }

        string[] filesAfter = Directory.GetFiles(fixtureDirectory)
            .Select(file => Path.GetFileName(file)!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(filesBefore, filesAfter);
        Assert.DoesNotContain(filesAfter, name =>
            name!.EndsWith(".rgba", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".raw", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAsync_RealRotatedMovAlsoDecodesFromUnicodePath()
    {
        using TemporaryMediaDirectory media = new("Поворот 日本語 测试");
        string path = media.CopyFixture("video-preview-rotated.mov", "обернене відео 日本語.MOV");
        Assert.Contains(path, character => character > 127);
        BundledFfmpegLocator locator = new(RepositoryRoot);
        ProcessRunner processRunner = new();
        using FfmpegValidationService validation = new(locator, processRunner);
        FfprobeVideoMetadataReader metadataReader = new(
            new MetadataExtractorVideoMetadataReader(),
            validation,
            processRunner);

        VideoMetadata metadata = await metadataReader.ReadAsync(path);
        VideoFrameReadResult result = await new FfmpegVideoFrameReader(
            locator,
            new BinaryProcessRunner(),
            metadataReader).ReadAsync(path, 0.5d, metadata);

        Assert.Equal(90, metadata.NormalizedRotationDegrees);
        Assert.True(result.Success, result.TechnicalDetail ?? result.UserMessage);
        Assert.Equal(240, result.Frame!.Width);
        Assert.Equal(320, result.Frame.Height);
        AssertPixel(result.Frame, 60, 80, red: 0, green: 255, blue: 0);
        AssertPixel(result.Frame, 180, 240, red: 0, green: 0, blue: 255);
        using FileStream exclusive = new(path, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.True(exclusive.CanRead);
    }

    [Fact]
    public async Task ReadAsync_MissingFfmpegReturnsFriendlyUnavailableFailure()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("video-preview.mp4");
        FfmpegVideoFrameReader reader = new(
            new StaticLocator(CreateLocation(Path.Combine(media.RootPath, "missing.exe"), exists: false)),
            new RecordingBinaryRunner(SuccessfulFrame(64, 48)),
            new RecordingMetadataReader(FixtureMetadata(path)));

        VideoFrameReadResult result = await reader.ReadAsync(path, 0.5d, FixtureMetadata(path));

        Assert.Equal(VideoFrameReadErrorCategory.DecoderUnavailable, result.ErrorCategory);
        Assert.DoesNotContain(path, result.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ProcessExecutionStatus.StartFailed, VideoFrameReadErrorCategory.ProcessStartFailed)]
    [InlineData(ProcessExecutionStatus.AccessDenied, VideoFrameReadErrorCategory.AccessDenied)]
    [InlineData(ProcessExecutionStatus.TimedOut, VideoFrameReadErrorCategory.TimedOut)]
    public async Task ReadAsync_MapsProcessFailures(
        ProcessExecutionStatus status,
        VideoFrameReadErrorCategory expectedCategory)
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("video-preview.mp4");
        BinaryProcessExecutionResult execution = new(
            status, null, [], "bounded diagnostic", TimeSpan.FromMilliseconds(5), TechnicalDetail: "start detail");

        VideoFrameReadResult result = await CreateFakeReader(new RecordingBinaryRunner(execution))
            .ReadAsync(path, 0.5d, FixtureMetadata(path));

        Assert.Equal(expectedCategory, result.ErrorCategory);
        Assert.DoesNotContain("start detail", result.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_NonZeroExitAndNoVideoStreamAreStructuredFailures()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("video-preview.mp4");
        BinaryProcessExecutionResult execution = new(
            ProcessExecutionStatus.Completed, 1, [], "Stream map '0:v:0' matches no streams.", TimeSpan.FromMilliseconds(5));

        VideoFrameReadResult result = await CreateFakeReader(new RecordingBinaryRunner(execution))
            .ReadAsync(path, 0.5d, FixtureMetadata(path));

        Assert.Equal(VideoFrameReadErrorCategory.NoVideoStream, result.ErrorCategory);
        Assert.Contains("does not contain", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("matches no streams", result.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadAsync_RejectsTruncatedBinaryOutput()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("video-preview.mp4");
        BinaryProcessExecutionResult execution = new(
            ProcessExecutionStatus.Completed, 0, new byte[17], string.Empty, TimeSpan.FromMilliseconds(5));

        VideoFrameReadResult result = await CreateFakeReader(new RecordingBinaryRunner(execution))
            .ReadAsync(path, 0.5d, FixtureMetadata(path));

        Assert.Equal(VideoFrameReadErrorCategory.IncompleteOutput, result.ErrorCategory);
        Assert.Contains("received: 17", result.TechnicalDetail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 48)]
    [InlineData(64, 0)]
    [InlineData(20000, 48)]
    public async Task ReadAsync_RejectsInvalidOrUnreasonableDimensions(int width, int height)
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("video-preview.mp4");
        VideoMetadata metadata = FixtureMetadata(path) with { Width = width, Height = height };

        VideoFrameReadResult result = await CreateFakeReader(new RecordingBinaryRunner(SuccessfulFrame(64, 48)))
            .ReadAsync(path, 0.5d, metadata);

        Assert.Equal(VideoFrameReadErrorCategory.InvalidDimensions, result.ErrorCategory);
    }

    [Fact]
    public async Task ReadAsync_CorruptMediaReturnsFriendlyFailureAndReleasesFile()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("broken.mkv");

        VideoFrameReadResult result = await CreateRealReader().ReadAsync(path, 0d, FixtureMetadata(path));

        Assert.False(result.Success);
        Assert.Equal(VideoFrameReadErrorCategory.DecodeFailed, result.ErrorCategory);
        Assert.DoesNotContain("ffmpeg", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        using FileStream exclusive = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.CanWrite);
    }

    [Fact]
    public async Task ReadAsync_PreCancelledAndActiveCancellationReturnCancelledResult()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("video-preview.mp4");
        using CancellationTokenSource preCancelled = new();
        preCancelled.Cancel();
        VideoFrameReadResult first = await CreateRealReader().ReadAsync(
            path, 0.5d, FixtureMetadata(path), preCancelled.Token);

        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingBinaryRunner runner = new(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return SuccessfulFrame(64, 48);
        });
        using CancellationTokenSource active = new();
        Task<VideoFrameReadResult> pending = CreateFakeReader(runner)
            .ReadAsync(path, 0.5d, FixtureMetadata(path), active.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        active.Cancel();
        VideoFrameReadResult second = await pending;

        Assert.Equal(VideoFrameReadStatus.Cancelled, first.Status);
        Assert.Equal(VideoFrameReadStatus.Cancelled, second.Status);
    }

    [Fact]
    public async Task FrameResultWithoutTimingContext_DoesNotFabricateResolvedTiming()
    {
        DecodedImage frame = new(1, 1, [10, 20, 30, 255]);

        VideoFrameReadResult result = VideoFrameReadResult.ForFrame(
            frame, 0.625d, null, null, null, null, null, null);

        Assert.True(result.Success);
        Assert.Equal(0.625d, result.RequestedNormalizedPosition);
        Assert.Null(result.ActualNormalizedPosition);
        Assert.Null(result.ActualTimestamp);
    }

    private static FfmpegVideoFrameReader CreateRealReader() => new(
        new BundledFfmpegLocator(RepositoryRoot),
        new BinaryProcessRunner(),
        new RecordingMetadataReader(FixtureMetadata("unused.mp4")));

    private static string FixturePath(string fixtureName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);
        Assert.True(File.Exists(path), $"The committed media fixture '{fixtureName}' is missing.");
        return path;
    }

    private static void AssertPixel(
        DecodedImage frame,
        int x,
        int y,
        byte red,
        byte green,
        byte blue)
    {
        int offset = checked(((y * frame.Width) + x) * 4);
        ReadOnlySpan<byte> pixels = frame.RgbaPixels.Span;
        Assert.InRange((int)pixels[offset], Math.Max(0, red - 8), Math.Min(255, red + 8));
        Assert.InRange((int)pixels[offset + 1], Math.Max(0, green - 8), Math.Min(255, green + 8));
        Assert.InRange((int)pixels[offset + 2], Math.Max(0, blue - 8), Math.Min(255, blue + 8));
        Assert.Equal(255, pixels[offset + 3]);
    }

    private static FfmpegVideoFrameReader CreateFakeReader(
        RecordingBinaryRunner runner,
        IVideoMetadataReader? metadata = null) => new(
        new StaticLocator(CreateLocation(Path.Combine(RepositoryRoot, "Tools", "ffmpeg", "ffmpeg.exe"), exists: true)),
        runner,
        metadata ?? new RecordingMetadataReader(FixtureMetadata("unused.mp4")));

    private static VideoMetadata FixtureMetadata(string path) => new(
        path,
        Path.GetFileName(path),
        64,
        48,
        TimeSpan.FromSeconds(1.2),
        10,
        12,
        Path.GetExtension(path).TrimStart('.').ToUpperInvariant(),
        File.Exists(path) ? new FileInfo(path).Length : 0,
        "h264",
        false,
        null,
        null);

    private static BinaryProcessExecutionResult SuccessfulFrame(int width, int height) => new(
        ProcessExecutionStatus.Completed,
        0,
        Enumerable.Repeat((byte)255, checked(width * height * 4)).ToArray(),
        string.Empty,
        TimeSpan.FromMilliseconds(5));

    private static BundledFfmpegLocation CreateLocation(string ffmpegPath, bool exists)
    {
        string directory = Path.GetDirectoryName(ffmpegPath)!;
        return new BundledFfmpegLocation(
            Path.GetDirectoryName(directory)!,
            directory,
            new BundledToolPath(FfmpegToolKind.Ffmpeg, ffmpegPath, exists, false),
            new BundledToolPath(FfmpegToolKind.Ffprobe, Path.Combine(directory, "ffprobe.exe"), exists, false));
    }

    private static string RepositoryRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private sealed class StaticLocator(BundledFfmpegLocation location) : IBundledFfmpegLocator
    {
        public BundledFfmpegLocation Locate() => location;
    }

    private sealed class RecordingMetadataReader(VideoMetadata result) : IVideoMetadataReader
    {
        public string? LastPath { get; private set; }

        public Task<VideoMetadata> ReadAsync(string inputPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastPath = inputPath;
            return Task.FromResult(result with { FilePath = inputPath, FileName = Path.GetFileName(inputPath) });
        }
    }

    private sealed class RecordingBinaryRunner : IBinaryProcessRunner
    {
        private readonly Func<BinaryProcessExecutionRequest, CancellationToken, Task<BinaryProcessExecutionResult>> _handler;

        public RecordingBinaryRunner(BinaryProcessExecutionResult result)
            : this((_, _) => Task.FromResult(result))
        {
        }

        public RecordingBinaryRunner(
            Func<BinaryProcessExecutionRequest, CancellationToken, Task<BinaryProcessExecutionResult>> handler)
        {
            _handler = handler;
        }

        public BinaryProcessExecutionRequest? LastRequest { get; private set; }

        public Task<BinaryProcessExecutionResult> RunAsync(
            BinaryProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return _handler(request, cancellationToken);
        }
    }
}
