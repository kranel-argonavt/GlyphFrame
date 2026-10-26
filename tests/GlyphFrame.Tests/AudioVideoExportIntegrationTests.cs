using System.Globalization;
using System.Text.Json;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Core.Policies;
using GlyphFrame.Core.Presets;
using GlyphFrame.Infrastructure.Ffmpeg;
using GlyphFrame.Infrastructure.Media;
using GlyphFrame.Rendering.Conversion;
using GlyphFrame.Rendering.Grid;
using GlyphFrame.Rendering.Mapping;
using GlyphFrame.Rendering.Skia;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class AudioVideoExportIntegrationTests
{
    [Fact]
    public async Task FfmpegAudioMuxer_StreamCopyPreservesVideoAndAudioPacketPayloads()
    {
        using TemporaryMediaDirectory media = new("packet copy 日本語");
        string videoOnly = media.CopyFixture("video-preview.mp4", "symbolized video.partial");
        string source = media.CopyFixture("video-audio-aac.mp4", "source audio 日本語.mp4");
        string output = Path.Combine(media.RootPath, "mux packet result 测试.partial");
        BundledFfmpegLocator locator = new(Path.Combine(RepositoryRoot, "artifacts", "local-runtime"));
        ProcessRunner runner = new();
        FfmpegAudioMuxer muxer = new(locator);

        AudioMuxResult result = await muxer.MuxAsync(new AudioMuxJob(
            videoOnly,
            source,
            output,
            AudioMuxStrategy.StreamCopy,
            TimeSpan.FromSeconds(1.2)));

        Assert.True(result.Success, result.TechnicalDetail ?? result.UserMessage);
        Assert.Equal(
            await PacketHashesAsync(videoOnly, "v:0", locator, runner),
            await PacketHashesAsync(output, "v:0", locator, runner));
        Assert.Equal(
            await PacketHashesAsync(source, "a:0", locator, runner),
            await PacketHashesAsync(output, "a:0", locator, runner));
        using FileStream exclusive = new(output, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.True(exclusive.CanRead);
    }

    [Fact]
    public async Task KeepAudio_CompatibleAacIsStreamCopiedIntoSymbolizedMp4()
    {
        using TemporaryMediaDirectory media = new("аудіо copy 日本語");
        string input = media.CopyFixture("video-audio-aac.mp4", "джерело AAC 日本語.mp4");
        ExportContext context = await ExportAsync(media, input, AudioExportMode.KeepAudio, "результат copy 测试.mp4");
        using (context)
        {
            Assert.Equal(AudioMuxOutcome.StreamCopied, context.Result.AudioOutcome);
            JsonElement video = FindStream(context.Probe.RootElement, "video");
            JsonElement audio = FindStream(context.Probe.RootElement, "audio");
            AssertVideoStream(video);
            Assert.Equal("aac", audio.GetProperty("codec_name").GetString());
            Assert.Equal(
                await PacketHashesAsync(input, "a:0", context.Locator, context.Runner),
                await PacketHashesAsync(context.OutputPath, "a:0", context.Locator, context.Runner));
            Assert.Contains(context.Progress, update => update.Stage == VideoExportStage.InspectingAudio);
            Assert.Contains(context.Progress, update => update.Stage == VideoExportStage.MergingAudio);
            AssertDurationAligned(video, audio);
            AssertExportSafety(media.RootPath, input, context.OutputPath);
        }
    }

    [Fact]
    public async Task KeepAudio_IncompatiblePcmIsTranscodedToDeterministicAac()
    {
        using TemporaryMediaDirectory media = new("аудіо fallback 日本語");
        string input = media.CopyFixture("video-audio-pcm.mov", "джерело PCM 日本語.mov");
        ExportContext context = await ExportAsync(media, input, AudioExportMode.KeepAudio, "результат AAC 测试.mp4");
        using (context)
        {
            Assert.Equal(AudioMuxOutcome.TranscodedToAac, context.Result.AudioOutcome);
            JsonElement video = FindStream(context.Probe.RootElement, "video");
            JsonElement audio = FindStream(context.Probe.RootElement, "audio");
            AssertVideoStream(video);
            Assert.Equal("aac", audio.GetProperty("codec_name").GetString());
            Assert.Equal("48000", audio.GetProperty("sample_rate").GetString());
            Assert.Equal(2, audio.GetProperty("channels").GetInt32());
            AssertDurationAligned(video, audio);
            AssertExportSafety(media.RootPath, input, context.OutputPath);
        }
    }

    [Fact]
    public async Task RemoveAudio_PublishesVideoOnlyWithoutInvokingAudioRequirements()
    {
        using TemporaryMediaDirectory media = new("remove audio 日本語");
        string input = media.CopyFixture("video-audio-aac.mp4", "джерело with audio.mp4");
        ExportContext context = await ExportAsync(media, input, AudioExportMode.RemoveAudio, "без аудіо 测试.mp4");
        using (context)
        {
            Assert.Equal(AudioMuxOutcome.Removed, context.Result.AudioOutcome);
            JsonElement[] streams = context.Probe.RootElement.GetProperty("streams").EnumerateArray().ToArray();
            Assert.Single(streams);
            AssertVideoStream(streams[0]);
            Assert.DoesNotContain(context.Progress, update => update.Stage is VideoExportStage.InspectingAudio
                or VideoExportStage.MergingAudio);
            AssertExportSafety(media.RootPath, input, context.OutputPath);
        }
    }

    [Fact]
    public async Task KeepAudio_SourceWithoutAudioSucceedsAsVideoOnly()
    {
        using TemporaryMediaDirectory media = new("silent source 日本語");
        string input = media.CopyFixture("video-preview.mp4", "тихе джерело 日本語.mp4");
        ExportContext context = await ExportAsync(media, input, AudioExportMode.KeepAudio, "тихий результат 测试.mp4");
        using (context)
        {
            Assert.Equal(AudioMuxOutcome.NoSourceAudio, context.Result.AudioOutcome);
            JsonElement[] streams = context.Probe.RootElement.GetProperty("streams").EnumerateArray().ToArray();
            Assert.Single(streams);
            AssertVideoStream(streams[0]);
            Assert.Contains(context.Progress, update => update.Stage == VideoExportStage.InspectingAudio);
            Assert.DoesNotContain(context.Progress, update => update.Stage == VideoExportStage.MergingAudio);
            AssertExportSafety(media.RootPath, input, context.OutputPath);
        }
    }

    [Fact]
    public async Task KeepAudio_PreservesNormalSourceAudioStartOffset()
    {
        using TemporaryMediaDirectory media = new("audio offset 日本語");
        string input = media.CopyFixture("video-audio-offset.mp4", "offset source 日本語.mp4");
        ExportContext context = await ExportAsync(media, input, AudioExportMode.KeepAudio, "offset output 测试.mp4");
        using (context)
        using (JsonDocument sourceProbe = await ProbeAsync(input, context.Locator, context.Runner))
        {
            JsonElement sourceAudio = FindStream(sourceProbe.RootElement, "audio");
            JsonElement outputAudio = FindStream(context.Probe.RootElement, "audio");
            double sourceStart = ReadDecimalProperty(sourceAudio, "start_time");
            double outputStart = ReadDecimalProperty(outputAudio, "start_time");
            Assert.InRange(Math.Abs(sourceStart - outputStart), 0d, 0.002d);
            AssertExportSafety(media.RootPath, input, context.OutputPath);
        }
    }

    [Theory]
    [InlineData("video-audio-short.mp4", 0.6)]
    [InlineData("video-audio-long.mp4", 1.2)]
    public async Task KeepAudio_ShorterAudioDoesNotCutVideoAndLongerAudioIsTrimmed(
        string fixture,
        double expectedAudioDuration)
    {
        using TemporaryMediaDirectory media = new("audio duration 日本語");
        string input = media.CopyFixture(fixture, $"source {fixture}");
        ExportContext context = await ExportAsync(media, input, AudioExportMode.KeepAudio, $"output {fixture}");
        using (context)
        {
            JsonElement video = FindStream(context.Probe.RootElement, "video");
            JsonElement audio = FindStream(context.Probe.RootElement, "audio");
            double videoDuration = ReadDuration(video);
            double audioDuration = ReadDuration(audio);
            Assert.InRange(videoDuration, 1.19, 1.21);
            Assert.InRange(audioDuration, expectedAudioDuration - 0.03, expectedAudioDuration + 0.04);
            Assert.True(videoDuration >= audioDuration || Math.Abs(videoDuration - audioDuration) <= 0.125);
            AssertExportSafety(media.RootPath, input, context.OutputPath);
        }
    }

    private static async Task<ExportContext> ExportAsync(
        TemporaryMediaDirectory media,
        string input,
        AudioExportMode audioMode,
        string outputFileName)
    {
        string output = Path.Combine(media.RootPath, outputFileName);
        BundledFfmpegLocator locator = new(Path.Combine(RepositoryRoot, "artifacts", "local-runtime"));
        ProcessRunner runner = new();
        FfmpegValidationService validation = new(locator, runner);
        FfprobeVideoMetadataReader metadataReader = new(
            new MetadataExtractorVideoMetadataReader(),
            validation,
            runner);
        VideoMetadata metadata = await metadataReader.ReadAsync(input);
        StreamingVideoExportService service = new(
            new FfmpegSequentialVideoFrameReader(locator),
            new FfmpegVideoEncoder(locator),
            CreatePipeline(),
            new OutputDimensionResolver(),
            new FfprobeAudioStreamProbe(validation, runner),
            new Mp4AudioCompatibilityPolicy(),
            new FfmpegAudioMuxer(locator));
        VideoExportJob job = new(
            input,
            output,
            metadata,
            new ImageRenderSettings("Consolas", 9, OutputResolution.SameAsSource, SymbolPolarity.LightOnDark),
            new CharacterPresetProvider().Get(CharacterPresetKind.SimpleAscii),
            Overwrite: false,
            AudioMode: audioMode);

        RecordingProgress progress = new();
        VideoExportResult result = await service.ExportAsync(job, CancellationToken.None, progress);
        Assert.True(result.Success, result.TechnicalDetail ?? result.UserMessage);
        Assert.Equal(12, result.FramesWritten);
        Assert.Equal(new PixelDimensions(64, 48), result.OutputDimensions);
        Assert.Equal(10, result.FramesPerSecond);
        Assert.True(new FileInfo(output).Length > 1_000);
        JsonDocument probe = await ProbeAsync(output, locator, runner);
        return new ExportContext(output, result, probe, locator, runner, validation, progress.Values);
    }

    private static async Task<JsonDocument> ProbeAsync(
        string path,
        BundledFfmpegLocator locator,
        ProcessRunner runner)
    {
        ProcessExecutionResult result = await runner.RunAsync(new ProcessExecutionRequest(
            locator.Locate().Ffprobe.ResolvedPath,
            [
                "-v", "error",
                "-show_streams",
                "-show_format",
                "-of", "json",
                path
            ],
            TimeSpan.FromSeconds(20)));
        Assert.True(result.Succeeded, result.StandardError);
        return JsonDocument.Parse(result.StandardOutput);
    }

    private static async Task<string[]> PacketHashesAsync(
        string path,
        string stream,
        BundledFfmpegLocator locator,
        ProcessRunner runner)
    {
        ProcessExecutionResult result = await runner.RunAsync(new ProcessExecutionRequest(
            locator.Locate().Ffprobe.ResolvedPath,
            [
                "-v", "error",
                "-select_streams", stream,
                "-show_packets",
                "-show_entries", "packet=data_hash",
                "-show_data_hash", "sha256",
                "-of", "json",
                path
            ],
            TimeSpan.FromSeconds(20)));
        Assert.True(result.Succeeded, result.StandardError);
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        return document.RootElement.GetProperty("packets")
            .EnumerateArray()
            .Select(packet => packet.GetProperty("data_hash").GetString()!)
            .ToArray();
    }

    private static JsonElement FindStream(JsonElement root, string codecType) => root
        .GetProperty("streams")
        .EnumerateArray()
        .Single(stream => stream.GetProperty("codec_type").GetString() == codecType);

    private static void AssertVideoStream(JsonElement video)
    {
        Assert.Equal("video", video.GetProperty("codec_type").GetString());
        Assert.Equal("h264", video.GetProperty("codec_name").GetString());
        Assert.Equal("yuv420p", video.GetProperty("pix_fmt").GetString());
        Assert.Equal(64, video.GetProperty("width").GetInt32());
        Assert.Equal(48, video.GetProperty("height").GetInt32());
        Assert.Equal("10/1", video.GetProperty("avg_frame_rate").GetString());
        Assert.Equal("12", video.GetProperty("nb_frames").GetString());
    }

    private static void AssertDurationAligned(JsonElement video, JsonElement audio)
    {
        double difference = Math.Abs(ReadDuration(video) - ReadDuration(audio));
        Assert.True(
            difference <= AudioMuxPolicy.DurationTolerance.TotalSeconds,
            $"Audio/video duration difference {difference:F6}s exceeded {AudioMuxPolicy.DurationTolerance.TotalSeconds:F3}s.");
    }

    private static double ReadDuration(JsonElement stream) => ReadDecimalProperty(stream, "duration");

    private static double ReadDecimalProperty(JsonElement element, string propertyName) => double.Parse(
        element.GetProperty(propertyName).GetString()!,
        CultureInfo.InvariantCulture);

    private static void AssertExportSafety(string directory, string input, string output)
    {
        string[] forbidden = [".partial", ".tmp", ".raw", ".png", ".jpg", ".jpeg"];
        Assert.DoesNotContain(
            Directory.EnumerateFiles(directory),
            path => forbidden.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase));
        using FileStream inputExclusive = new(input, FileMode.Open, FileAccess.Read, FileShare.None);
        using FileStream outputExclusive = new(output, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.True(inputExclusive.CanRead);
        Assert.True(outputExclusive.CanRead);
    }

    private static ImagePipelineService CreatePipeline() => new(
        new SkiaImageDecoder(),
        new OutputDimensionResolver(),
        new SkiaGlyphMeasurer(),
        new SymbolGridCalculator(),
        new FrameToSymbolConverter(new BrightnessToSymbolMapper()),
        new SkiaSymbolFrameRenderer());

    private static string RepositoryRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private sealed record ExportContext(
        string OutputPath,
        VideoExportResult Result,
        JsonDocument Probe,
        BundledFfmpegLocator Locator,
        ProcessRunner Runner,
        FfmpegValidationService Validation,
        IReadOnlyList<VideoExportProgress> Progress) : IDisposable
    {
        public void Dispose()
        {
            Probe.Dispose();
            Validation.Dispose();
        }
    }

    private sealed class RecordingProgress : IProgress<VideoExportProgress>
    {
        public List<VideoExportProgress> Values { get; } = [];

        public void Report(VideoExportProgress value) => Values.Add(value);
    }
}
