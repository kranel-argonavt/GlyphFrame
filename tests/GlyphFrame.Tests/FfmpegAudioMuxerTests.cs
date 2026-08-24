using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Infrastructure.Ffmpeg;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class FfmpegAudioMuxerTests
{
    [Theory]
    [InlineData(AudioMuxStrategy.StreamCopy, "copy")]
    [InlineData(AudioMuxStrategy.TranscodeToAac, "aac")]
    public void CreateArguments_MapsTwoInputsCopiesVideoAndSelectsAudioStrategy(
        AudioMuxStrategy strategy,
        string expectedAudioCodec)
    {
        AudioMuxJob job = new(
            "C:\\відео partial\\symbolized.partial",
            "C:\\джерело 日本語\\source.mov",
            "C:\\output 测试\\final.partial",
            strategy,
            TimeSpan.FromSeconds(1.2));

        IReadOnlyList<string> arguments = FfmpegAudioMuxer.CreateArguments(job);

        AssertArgumentPair(arguments, "-i", job.VideoOnlyInputPath, occurrence: 0);
        AssertArgumentPair(arguments, "-i", job.SourceMediaPath, occurrence: 1);
        AssertArgumentPair(arguments, "-map", "0:v:0", occurrence: 0);
        AssertArgumentPair(arguments, "-map", "1:a:0", occurrence: 1);
        AssertArgumentPair(arguments, "-c:v", "copy");
        AssertArgumentPair(arguments, "-c:a", expectedAudioCodec);
        AssertArgumentPair(arguments, "-t", "1.2");
        AssertArgumentPair(arguments, "-f", "mp4");
        Assert.Equal(job.OutputPath, arguments[^1]);
        Assert.DoesNotContain("libx264", arguments);
        Assert.DoesNotContain("-crf", arguments);
        Assert.DoesNotContain("-shortest", arguments);

        if (strategy == AudioMuxStrategy.TranscodeToAac)
        {
            AssertArgumentPair(arguments, "-b:a", "192k");
            AssertArgumentPair(arguments, "-ar", "48000");
            AssertArgumentPair(arguments, "-ac", "2");
        }
        else
        {
            Assert.DoesNotContain("-b:a", arguments);
            Assert.DoesNotContain("-ar", arguments);
            Assert.DoesNotContain("-ac", arguments);
        }
    }

    [Fact]
    public async Task MuxAsync_MissingBundledFfmpegReturnsFriendlyStartFailure()
    {
        using MuxFiles files = new();
        FakeStreamingProcessFactory factory = new();
        FfmpegAudioMuxer muxer = CreateMuxer(factory, ffmpegExists: false);

        AudioMuxResult result = await muxer.MuxAsync(files.Job(AudioMuxStrategy.StreamCopy));

        Assert.Equal(VideoExportErrorCategory.AudioMuxStartFailed, result.ErrorCategory);
        Assert.Contains("unavailable", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, factory.StartCalls);
    }

    [Fact]
    public async Task MuxAsync_MissingIntermediateIsRejectedBeforeProcessStartup()
    {
        using MuxFiles files = new();
        File.Delete(files.VideoPath);
        FakeStreamingProcessFactory factory = new();
        FfmpegAudioMuxer muxer = CreateMuxer(factory);

        AudioMuxResult result = await muxer.MuxAsync(files.Job(AudioMuxStrategy.StreamCopy));

        Assert.Equal(VideoExportErrorCategory.AudioMuxFailed, result.ErrorCategory);
        Assert.Contains("required", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, factory.StartCalls);
    }

    [Fact]
    public async Task MuxAsync_ProcessStartupFailureKeepsTechnicalDetailSeparate()
    {
        using MuxFiles files = new();
        FakeStreamingProcessFactory factory = new()
        {
            StartException = new InvalidOperationException("controlled mux start failure")
        };
        FfmpegAudioMuxer muxer = CreateMuxer(factory);

        AudioMuxResult result = await muxer.MuxAsync(files.Job(AudioMuxStrategy.StreamCopy));

        Assert.Equal(VideoExportErrorCategory.AudioMuxStartFailed, result.ErrorCategory);
        Assert.DoesNotContain("controlled", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("controlled mux start failure", result.TechnicalDetail);
    }

    [Theory]
    [InlineData(AudioMuxStrategy.StreamCopy, AudioMuxOutcome.StreamCopied)]
    [InlineData(AudioMuxStrategy.TranscodeToAac, AudioMuxOutcome.TranscodedToAac)]
    public async Task MuxAsync_SuccessWaitsForExitProducesOutcomeAndReleasesOutput(
        AudioMuxStrategy strategy,
        AudioMuxOutcome expectedOutcome)
    {
        using MuxFiles files = new();
        FakeStreamingProcessSession session = new()
        {
            WaitForExitHandler = _ =>
            {
                File.WriteAllBytes(files.OutputPath, [1, 2, 3]);
                return Task.CompletedTask;
            }
        };
        FakeStreamingProcessFactory factory = new() { Session = session };
        FfmpegAudioMuxer muxer = CreateMuxer(factory);

        AudioMuxResult result = await muxer.MuxAsync(files.Job(strategy));

        Assert.True(result.Success);
        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal(1, session.WaitForExitCalls);
        Assert.Equal(0, session.KillCalls);
        Assert.Equal(1, session.DisposeCalls);
        Assert.Contains(files.SourcePath, factory.Arguments!);
        using FileStream exclusive = new(files.OutputPath, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.True(exclusive.CanRead);
    }

    [Theory]
    [InlineData(AudioMuxStrategy.StreamCopy, VideoExportErrorCategory.AudioStreamCopyFailed)]
    [InlineData(AudioMuxStrategy.TranscodeToAac, VideoExportErrorCategory.AudioTranscodeFailed)]
    public async Task MuxAsync_NonZeroExitUsesSpecificCategoryAndBoundedStderr(
        AudioMuxStrategy strategy,
        VideoExportErrorCategory expectedCategory)
    {
        using MuxFiles files = new();
        string diagnostics = new('m', StreamingProcessSupport.MaximumErrorCharacters + 100);
        FakeStreamingProcessSession session = new()
        {
            ExitCode = 4,
            StandardError = new TrackingStringReader(diagnostics)
        };
        FfmpegAudioMuxer muxer = CreateMuxer(new FakeStreamingProcessFactory { Session = session });

        AudioMuxResult result = await muxer.MuxAsync(files.Job(strategy));

        Assert.Equal(expectedCategory, result.ErrorCategory);
        Assert.Contains("exit code: 4", result.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.TechnicalDetail!.Length < diagnostics.Length + 100);
        Assert.Equal(1, session.DisposeCalls);
    }

    [Fact]
    public async Task MuxAsync_TimeoutTerminatesEntireProcessTree()
    {
        using MuxFiles files = new();
        FakeStreamingProcessSession session = new()
        {
            WaitForExitHandler = cancellationToken => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
        };
        FfmpegAudioMuxer muxer = CreateMuxer(
            new FakeStreamingProcessFactory { Session = session },
            timeout: TimeSpan.FromMilliseconds(25));

        AudioMuxResult result = await muxer.MuxAsync(files.Job(AudioMuxStrategy.StreamCopy));

        Assert.Equal(VideoExportErrorCategory.AudioMuxFailed, result.ErrorCategory);
        Assert.Contains("did not finish", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, session.KillCalls);
        Assert.Equal(1, session.DisposeCalls);
    }

    [Fact]
    public async Task MuxAsync_ZeroExitWithoutOutputIsNotReportedAsSuccess()
    {
        using MuxFiles files = new();
        FakeStreamingProcessSession session = new();
        FfmpegAudioMuxer muxer = CreateMuxer(new FakeStreamingProcessFactory { Session = session });

        AudioMuxResult result = await muxer.MuxAsync(files.Job(AudioMuxStrategy.StreamCopy));

        Assert.False(result.Success);
        Assert.Equal(VideoExportErrorCategory.AudioMuxFailed, result.ErrorCategory);
        Assert.Contains("did not create", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, session.DisposeCalls);
    }

    [Fact]
    public async Task MuxAsync_CancellationTerminatesEntireProcessTreeAndDisposesProcess()
    {
        using MuxFiles files = new();
        TaskCompletionSource waitStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeStreamingProcessSession session = new()
        {
            WaitForExitHandler = async cancellationToken =>
            {
                waitStarted.SetResult();
                TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                await completion.Task.WaitAsync(cancellationToken);
            }
        };
        FfmpegAudioMuxer muxer = CreateMuxer(new FakeStreamingProcessFactory { Session = session });
        using CancellationTokenSource cancellation = new();

        Task<AudioMuxResult> muxing = muxer.MuxAsync(
            files.Job(AudioMuxStrategy.StreamCopy),
            cancellation.Token);
        await waitStarted.Task;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => muxing);
        Assert.Equal(1, session.KillCalls);
        Assert.Equal(1, session.DisposeCalls);
    }

    private static FfmpegAudioMuxer CreateMuxer(
        FakeStreamingProcessFactory factory,
        bool ffmpegExists = true,
        TimeSpan? timeout = null) => new(
            new StaticLocator(CreateLocation(ffmpegExists)),
            factory,
            timeout ?? TimeSpan.FromSeconds(1));

    private static BundledFfmpegLocation CreateLocation(bool exists)
    {
        string tools = Path.Combine("application", "Tools", "ffmpeg");
        return new BundledFfmpegLocation(
            "application",
            tools,
            new BundledToolPath(FfmpegToolKind.Ffmpeg, Path.Combine(tools, "ffmpeg.exe"), exists, false),
            new BundledToolPath(FfmpegToolKind.Ffprobe, Path.Combine(tools, "ffprobe.exe"), true, false));
    }

    private static void AssertArgumentPair(
        IReadOnlyList<string> arguments,
        string name,
        string value,
        int occurrence = 0)
    {
        int index = arguments
            .Select((argument, index) => (argument, index))
            .Where(item => item.argument == name)
            .ElementAt(occurrence)
            .index;
        Assert.Equal(value, arguments[index + 1]);
    }

    private sealed class StaticLocator(BundledFfmpegLocation location) : IBundledFfmpegLocator
    {
        public BundledFfmpegLocation Locate() => location;
    }

    private sealed class MuxFiles : IDisposable
    {
        private readonly TemporaryMediaDirectory _directory = new();

        public MuxFiles()
        {
            VideoPath = _directory.CopyFixture("video-preview.mp4", "symbolized.partial");
            SourcePath = _directory.CopyFixture("video-audio-aac.mp4", "source.mp4");
            OutputPath = Path.Combine(_directory.RootPath, "muxed.partial");
        }

        public string VideoPath { get; }

        public string SourcePath { get; }

        public string OutputPath { get; }

        public AudioMuxJob Job(AudioMuxStrategy strategy) => new(
            VideoPath,
            SourcePath,
            OutputPath,
            strategy,
            TimeSpan.FromSeconds(1.2));

        public void Dispose() => _directory.Dispose();
    }
}
