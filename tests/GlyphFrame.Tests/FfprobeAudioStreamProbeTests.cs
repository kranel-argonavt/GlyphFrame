using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Infrastructure.Media;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class FfprobeAudioStreamProbeTests
{
    private const string AudioJson = """
        {
          "streams": [
            {
              "codec_name": "aac",
              "sample_rate": "48000",
              "channels": 2,
              "start_time": "0.125",
              "duration": "1.2"
            }
          ],
          "format": { "duration": "1.2" }
        }
        """;

    [Fact]
    public void Parse_AudioStreamPreservesCodecRateChannelsAndTiming()
    {
        AudioProbeResult result = FfprobeAudioStreamProbe.Parse(AudioJson);

        Assert.True(result.Success);
        Assert.True(result.HasAudio);
        Assert.Equal("aac", result.Stream!.CodecName);
        Assert.Equal(48_000, result.Stream.SampleRate);
        Assert.Equal(2, result.Stream.Channels);
        Assert.Equal(TimeSpan.FromSeconds(1.2), result.Stream.Duration);
        Assert.Equal(TimeSpan.FromSeconds(0.125), result.Stream.StartTime);
    }

    [Fact]
    public void Parse_EmptyStreamsConfirmsNoAudio()
    {
        AudioProbeResult result = FfprobeAudioStreamProbe.Parse("{\"streams\":[]}");

        Assert.True(result.Success);
        Assert.False(result.HasAudio);
        Assert.Null(result.Stream);
    }

    [Fact]
    public void Parse_MalformedCatalogIsRejected()
    {
        Assert.ThrowsAny<Exception>(() => FfprobeAudioStreamProbe.Parse("{\"format\":{}}"));
        Assert.ThrowsAny<Exception>(() => FfprobeAudioStreamProbe.Parse("not-json"));
    }

    [Fact]
    public async Task ProbeAsync_UsesUnicodePathAsOneArgumentAndReturnsAudio()
    {
        using TemporaryMediaDirectory media = new("аудіо 日本語 测试");
        string input = media.CopyFixture("video-audio-aac.mp4", "тестове аудіо-日本語.mp4");
        RecordingProcessRunner runner = new(Result(AudioJson));
        FfprobeAudioStreamProbe probe = new(
            new StaticValidationService(Validation(available: true)),
            runner);

        AudioProbeResult result = await probe.ProbeAsync(input);

        Assert.True(result.HasAudio);
        Assert.Equal(Path.GetFullPath(input), runner.LastRequest!.Arguments[^1]);
        Assert.Equal(1, runner.LastRequest.Arguments.Count(argument => argument == Path.GetFullPath(input)));
        Assert.Contains("a:0", runner.LastRequest.Arguments);
    }

    [Fact]
    public async Task ProbeAsync_MissingFfprobeFailsInsteadOfSilentlyDroppingAudio()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-audio-aac.mp4");
        RecordingProcessRunner runner = new(Result(AudioJson));
        FfprobeAudioStreamProbe probe = new(
            new StaticValidationService(Validation(available: false)),
            runner);

        AudioProbeResult result = await probe.ProbeAsync(input);

        Assert.False(result.Success);
        Assert.Equal(VideoExportErrorCategory.AudioMetadataUnavailable, result.ErrorCategory);
        Assert.Contains("unavailable", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Null(runner.LastRequest);
    }

    [Fact]
    public async Task ProbeAsync_NonZeroExitReturnsFriendlyFailureWithTechnicalDetail()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-audio-aac.mp4");
        RecordingProcessRunner runner = new(new ProcessExecutionResult(
            ProcessExecutionStatus.Completed,
            7,
            string.Empty,
            "controlled probe failure",
            TimeSpan.Zero));
        FfprobeAudioStreamProbe probe = new(
            new StaticValidationService(Validation(available: true)),
            runner);

        AudioProbeResult result = await probe.ProbeAsync(input);

        Assert.False(result.Success);
        Assert.DoesNotContain("controlled", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("controlled probe failure", result.TechnicalDetail);
    }

    [Fact]
    public async Task ProbeAsync_MalformedOutputReturnsStableFailure()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-audio-aac.mp4");
        FfprobeAudioStreamProbe probe = new(
            new StaticValidationService(Validation(available: true)),
            new RecordingProcessRunner(Result("{not-json")));

        AudioProbeResult result = await probe.ProbeAsync(input);

        Assert.False(result.Success);
        Assert.Contains("metadata", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.TechnicalDetail);
    }

    [Fact]
    public async Task ProbeAsync_PreCancelledRequestDoesNotStartProcess()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-audio-aac.mp4");
        RecordingProcessRunner runner = new(Result(AudioJson));
        FfprobeAudioStreamProbe probe = new(
            new StaticValidationService(Validation(available: true)),
            runner);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            probe.ProbeAsync(input, cancellation.Token));
        Assert.Null(runner.LastRequest);
    }

    private static ProcessExecutionResult Result(string output) => new(
        ProcessExecutionStatus.Completed,
        0,
        output,
        string.Empty,
        TimeSpan.Zero);

    private static FfmpegValidationResult Validation(bool available)
    {
        FfmpegToolValidationResult ffmpeg = new(
            FfmpegToolKind.Ffmpeg,
            FfmpegToolValidationStatus.Available,
            "C:\\app\\Tools\\ffmpeg\\ffmpeg.exe",
            "9.0-test",
            "available",
            "ok");
        FfmpegToolValidationResult ffprobe = new(
            FfmpegToolKind.Ffprobe,
            available ? FfmpegToolValidationStatus.Available : FfmpegToolValidationStatus.Missing,
            "C:\\app\\Tools\\ffmpeg\\ffprobe.exe",
            available ? "9.0-test" : null,
            available ? "available" : "missing",
            available ? "ok" : "missing diagnostic");
        return new FfmpegValidationResult("C:\\app\\Tools\\ffmpeg", ffmpeg, ffprobe, DateTimeOffset.UtcNow);
    }

    private sealed class StaticValidationService(FfmpegValidationResult result) : IFfmpegValidationService
    {
        public Task<FfmpegValidationResult> ValidateAsync(
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingProcessRunner(ProcessExecutionResult result) : IProcessRunner
    {
        public ProcessExecutionRequest? LastRequest { get; private set; }

        public Task<ProcessExecutionResult> RunAsync(
            ProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return Task.FromResult(result);
        }
    }
}
