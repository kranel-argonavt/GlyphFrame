using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Infrastructure.Media;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class FfprobeVideoMetadataReaderTests
{
    [Fact]
    public async Task ReadAsync_EnrichesManagedMetadataWithReliableProbeFields()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("sample.mp4", "probe metadata Ω.mp4");
        RecordingProcessRunner runner = new(SuccessfulProbeResult());
        FfprobeVideoMetadataReader reader = new(
            new MetadataExtractorVideoMetadataReader(),
            new StaticValidationService(CreateValidation(available: true)),
            runner);

        VideoMetadata metadata = await reader.ReadAsync(path);

        Assert.Equal(320, metadata.Width);
        Assert.Equal(240, metadata.Height);
        Assert.Equal(29.97, metadata.Fps!.Value, precision: 3);
        Assert.Equal(900, metadata.FrameCount);
        Assert.Equal("h264", metadata.VideoCodec);
        Assert.True(metadata.HasAudio);
        Assert.Equal("aac", metadata.AudioCodec);
        Assert.Equal(48_000, metadata.AudioSampleRate);
        Assert.Equal(path, runner.LastRequest!.Arguments[^1]);
        Assert.Contains("-show_streams", runner.LastRequest.Arguments);
    }

    [Fact]
    public async Task ReadAsync_UsesManagedFallbackWhenFfprobeIsUnavailable()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("sample.mkv");
        RecordingProcessRunner runner = new(SuccessfulProbeResult());
        FfprobeVideoMetadataReader reader = new(
            new MetadataExtractorVideoMetadataReader(),
            new StaticValidationService(CreateValidation(available: false)),
            runner);

        VideoMetadata metadata = await reader.ReadAsync(path);

        Assert.Equal("V_MPEG4/ISO/AVC", metadata.VideoCodec);
        Assert.Equal(30d, metadata.Fps!.Value, precision: 3);
        Assert.Null(runner.LastRequest);
    }

    [Fact]
    public async Task ReadAsync_PreservesFallbackWhenProbeFails()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("sample.avi");
        RecordingProcessRunner runner = new(new ProcessExecutionResult(
            ProcessExecutionStatus.Completed,
            3,
            string.Empty,
            "probe error",
            TimeSpan.FromMilliseconds(5)));
        FfprobeVideoMetadataReader reader = new(
            new MetadataExtractorVideoMetadataReader(),
            new StaticValidationService(CreateValidation(available: true)),
            runner);

        VideoMetadata metadata = await reader.ReadAsync(path);

        Assert.Equal("AVI", metadata.Container);
        Assert.Equal(320, metadata.Width);
        Assert.Equal(240, metadata.Height);
    }

    [Fact]
    public async Task ReadAsync_MalformedProbeJsonDoesNotBreakVideoImportOrLockFile()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("sample.mov");
        RecordingProcessRunner runner = new(new ProcessExecutionResult(
            ProcessExecutionStatus.Completed,
            0,
            "{not-json",
            string.Empty,
            TimeSpan.FromMilliseconds(5)));
        FfprobeVideoMetadataReader reader = new(
            new MetadataExtractorVideoMetadataReader(),
            new StaticValidationService(CreateValidation(available: true)),
            runner);

        VideoMetadata metadata = await reader.ReadAsync(path);

        Assert.Equal("MOV", metadata.Container);
        using FileStream exclusive = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.CanWrite);
    }

    [Fact]
    public async Task ReadAsync_ReportsNoAudioOnlyWhenProbeReturnedAStreamCatalog()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("sample.mp4");
        const string noAudioJson = """
            {
              "streams": [
                { "codec_type": "video", "codec_name": "h264", "width": 320, "height": 240, "avg_frame_rate": "25/1" }
              ],
              "format": { "format_name": "mov,mp4", "duration": "1.0" }
            }
            """;
        RecordingProcessRunner runner = new(new ProcessExecutionResult(
            ProcessExecutionStatus.Completed, 0, noAudioJson, string.Empty, TimeSpan.FromMilliseconds(5)));
        FfprobeVideoMetadataReader reader = new(
            new MetadataExtractorVideoMetadataReader(),
            new StaticValidationService(CreateValidation(available: true)),
            runner);

        VideoMetadata metadata = await reader.ReadAsync(path);

        Assert.False(metadata.HasAudio);
        Assert.Null(metadata.AudioCodec);
        Assert.Null(metadata.AudioSampleRate);
    }

    [Theory]
    [InlineData(-90, 270, 240, 320)]
    [InlineData(90, 90, 240, 320)]
    [InlineData(180, 180, 320, 240)]
    public void Enrich_ParsesDisplayRotationAndDimensions(
        int reportedRotation,
        int normalizedRotation,
        int displayWidth,
        int displayHeight)
    {
        VideoMetadata fallback = new(
            "rotation.mov", "rotation.mov", 320, 240, TimeSpan.FromSeconds(1), 30, 30,
            "MOV", 100, "h264", false, null, null);
        string json = $$"""
            {
              "streams": [
                {
                  "codec_type": "video",
                  "width": 320,
                  "height": 240,
                  "side_data_list": [ { "rotation": {{reportedRotation}} } ]
                }
              ]
            }
            """;

        VideoMetadata result = FfprobeVideoMetadataReader.Enrich(fallback, json);

        Assert.Equal(normalizedRotation, result.NormalizedRotationDegrees);
        Assert.Equal(displayWidth, result.DisplayWidth);
        Assert.Equal(displayHeight, result.DisplayHeight);
    }

    private static ProcessExecutionResult SuccessfulProbeResult()
    {
        const string json = """
            {
              "streams": [
                {
                  "codec_type": "video",
                  "codec_name": "h264",
                  "width": 320,
                  "height": 240,
                  "duration": "30.030",
                  "avg_frame_rate": "30000/1001",
                  "nb_frames": "900"
                },
                {
                  "codec_type": "audio",
                  "codec_name": "aac",
                  "sample_rate": "48000"
                }
              ],
              "format": { "format_name": "mov,mp4,m4a,3gp,3g2,mj2", "duration": "30.030" }
            }
            """;
        return new ProcessExecutionResult(
            ProcessExecutionStatus.Completed, 0, json, string.Empty, TimeSpan.FromMilliseconds(5));
    }

    private static FfmpegValidationResult CreateValidation(bool available)
    {
        FfmpegToolValidationResult ffmpeg = new(
            FfmpegToolKind.Ffmpeg,
            FfmpegToolValidationStatus.Available,
            "C:\\app\\Tools\\ffmpeg\\ffmpeg.exe",
            "9.0-test",
            "available",
            "ok");
        FfmpegToolValidationResult ffprobe = available
            ? new(FfmpegToolKind.Ffprobe, FfmpegToolValidationStatus.Available, "C:\\app\\Tools\\ffmpeg\\ffprobe.exe", "9.0-test", "available", "ok")
            : new(FfmpegToolKind.Ffprobe, FfmpegToolValidationStatus.Missing, "C:\\app\\Tools\\ffmpeg\\ffprobe.exe", null, "missing", "missing");
        return new FfmpegValidationResult("C:\\app\\Tools\\ffmpeg", ffmpeg, ffprobe, DateTimeOffset.UtcNow);
    }

    private sealed class StaticValidationService(FfmpegValidationResult result) : IFfmpegValidationService
    {
        public Task<FfmpegValidationResult> ValidateAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingProcessRunner(ProcessExecutionResult result) : IProcessRunner
    {
        public ProcessExecutionRequest? LastRequest { get; private set; }

        public Task<ProcessExecutionResult> RunAsync(ProcessExecutionRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return Task.FromResult(result);
        }
    }
}
