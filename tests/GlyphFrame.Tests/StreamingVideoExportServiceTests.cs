using System.Runtime.CompilerServices;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Infrastructure.Media;
using GlyphFrame.Rendering.Grid;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class StreamingVideoExportServiceTests
{
    [Fact]
    public async Task ExportAsync_StreamsFramesInOrderCropsOddDimensionsAndFinalizesAtomically()
    {
        using TemporaryMediaDirectory media = new("відео export 日本語");
        string input = media.CopyFixture("video-preview.mp4", "джерело відео.mp4");
        string output = Path.Combine(media.RootPath, "символи 日本語.mp4");
        FakeFrameReader reader = new(3, 3, frameCount: 3);
        RecordingPipeline pipeline = new();
        RecordingEncoder encoder = new(writePartial: true);
        StreamingVideoExportService service = CreateService(reader, encoder, pipeline);

        VideoExportResult result = await service.ExportAsync(Job(input, output, overwrite: false, width: 3, height: 3));

        Assert.True(result.Success, result.TechnicalDetail ?? result.UserMessage);
        Assert.Equal(3, result.FramesWritten);
        Assert.Equal(new PixelDimensions(2, 2), result.OutputDimensions);
        Assert.Equal([0, 1, 2], encoder.FrameMarkers);
        Assert.All(encoder.FrameDimensions, dimensions => Assert.Equal(new PixelDimensions(2, 2), dimensions));
        Assert.Equal(1, pipeline.MaximumConcurrentRenders);
        Assert.True(File.Exists(output));
        Assert.Empty(Directory.GetFiles(media.RootPath, "*.partial"));
        Assert.Empty(Directory.GetFiles(media.RootPath, "*.raw"));
        Assert.Empty(Directory.GetFiles(media.RootPath, "*.png"));
        using FileStream inputExclusive = new(input, FileMode.Open, FileAccess.Read, FileShare.None);
        using FileStream outputExclusive = new(output, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.True(inputExclusive.CanRead);
        Assert.True(outputExclusive.CanRead);
    }

    [Fact]
    public async Task ExportAsync_EncoderFailurePreservesExistingDestinationAndDeletesPartial()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "existing.mp4");
        await File.WriteAllBytesAsync(output, [9, 8, 7]);
        RecordingEncoder encoder = new(writePartial: true)
        {
            Result = new VideoEncodingResult(false, 1, "friendly failure", "encoder detail")
        };
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 1),
            encoder,
            new RecordingPipeline());

        VideoExportResult result = await service.ExportAsync(Job(input, output, overwrite: true));

        Assert.False(result.Success);
        Assert.Equal(VideoExportErrorCategory.EncoderFailed, result.ErrorCategory);
        Assert.Equal([9, 8, 7], await File.ReadAllBytesAsync(output));
        Assert.Empty(Directory.GetFiles(media.RootPath, "*.partial"));
    }

    [Fact]
    public async Task ExportAsync_CancellationDeletesPartialOutput()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "cancelled.mp4");
        RecordingEncoder encoder = new(writePartial: true) { Cancel = true };
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 2),
            encoder,
            new RecordingPipeline());

        VideoExportResult result = await service.ExportAsync(Job(input, output, overwrite: false));

        Assert.Equal(VideoExportStatus.Cancelled, result.Status);
        Assert.False(File.Exists(output));
        Assert.Empty(Directory.GetFiles(media.RootPath, "*.partial"));
    }

    [Fact]
    public async Task ExportAsync_DeclinedOverwriteNeverStartsEncoder()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "existing.mp4");
        await File.WriteAllBytesAsync(output, [4, 5, 6]);
        RecordingEncoder encoder = new(writePartial: true);
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 1),
            encoder,
            new RecordingPipeline());

        VideoExportResult result = await service.ExportAsync(Job(input, output, overwrite: false));

        Assert.False(result.Success);
        Assert.Equal(0, encoder.Calls);
        Assert.Equal([4, 5, 6], await File.ReadAllBytesAsync(output));
    }

    [Theory]
    [InlineData("decoder")]
    [InlineData("renderer")]
    public async Task ExportAsync_DecodeOrRenderFailurePreservesDestinationAndRemovesAllArtifacts(string failure)
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "existing.mp4");
        await File.WriteAllBytesAsync(output, [7, 6, 5]);
        FakeFrameReader reader = new(64, 48, 2)
        {
            Failure = failure == "decoder"
                ? new VideoExportException(
                    VideoExportErrorCategory.DecodeFailed,
                    "friendly decode failure",
                    "decoder detail")
                : null
        };
        RecordingPipeline pipeline = new()
        {
            Failure = failure == "renderer"
                ? new ImageProcessingException(
                    ImageProcessingErrorCategory.RenderingFailed,
                    "friendly render failure",
                    "renderer detail")
                : null
        };
        StreamingVideoExportService service = CreateService(
            reader,
            new RecordingEncoder(writePartial: true),
            pipeline);

        VideoExportResult result = await service.ExportAsync(Job(input, output, overwrite: true));

        Assert.False(result.Success);
        Assert.Equal(
            failure == "decoder" ? VideoExportErrorCategory.DecodeFailed : VideoExportErrorCategory.RenderingFailed,
            result.ErrorCategory);
        Assert.Equal([7, 6, 5], await File.ReadAllBytesAsync(output));
        AssertNoExportArtifacts(media.RootPath);
    }

    [Fact]
    public async Task ExportAsync_CancellationImmediatelyBeforePublicationPreservesDestinationAndDeletesPartial()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "existing.mp4");
        await File.WriteAllBytesAsync(output, [3, 2, 1]);
        using CancellationTokenSource cancellation = new();
        RecordingEncoder encoder = new(writePartial: true)
        {
            BeforeReturn = cancellation.Cancel
        };
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 1),
            encoder,
            new RecordingPipeline());

        VideoExportResult result = await service.ExportAsync(
            Job(input, output, overwrite: true),
            cancellation.Token);

        Assert.Equal(VideoExportStatus.Cancelled, result.Status);
        Assert.Equal([3, 2, 1], await File.ReadAllBytesAsync(output));
        AssertNoExportArtifacts(media.RootPath);
    }

    [Fact]
    public async Task ExportAsync_FinalizationFailureReturnsSpecificCategoryAndDeletesPartial()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "occupied.mp4");
        Directory.CreateDirectory(output);
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 1),
            new RecordingEncoder(writePartial: true),
            new RecordingPipeline());

        VideoExportResult result = await service.ExportAsync(Job(input, output, overwrite: false));

        Assert.Equal(VideoExportErrorCategory.OutputFinalizationFailed, result.ErrorCategory);
        Assert.True(Directory.Exists(output));
        AssertNoExportArtifacts(media.RootPath);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-1)]
    public async Task ExportAsync_FrameSequenceGapOrReorderingIsRejected(long secondSequence)
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "sequence.mp4");
        FakeFrameReader reader = new(64, 48, 2) { SequenceNumbers = [0, secondSequence] };
        StreamingVideoExportService service = CreateService(
            reader,
            new RecordingEncoder(writePartial: true),
            new RecordingPipeline());

        VideoExportResult result = await service.ExportAsync(Job(input, output, overwrite: false));

        Assert.Equal(VideoExportErrorCategory.DecodeFailed, result.ErrorCategory);
        Assert.Contains("sequential order", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(output));
        AssertNoExportArtifacts(media.RootPath);
    }

    [Fact]
    public async Task ExportAsync_PreservesSpecificEncoderFailureCategory()
    {
        using TemporaryMediaDirectory media = new();
        string input = media.CopyFixture("video-preview.mp4");
        string output = Path.Combine(media.RootPath, "write-failure.mp4");
        RecordingEncoder encoder = new(writePartial: true)
        {
            Result = new VideoEncodingResult(
                false,
                1,
                "friendly write failure",
                "write detail",
                VideoExportErrorCategory.EncoderWriteFailed)
        };
        StreamingVideoExportService service = CreateService(
            new FakeFrameReader(64, 48, 1),
            encoder,
            new RecordingPipeline());

        VideoExportResult result = await service.ExportAsync(Job(input, output, overwrite: false));

        Assert.Equal(VideoExportErrorCategory.EncoderWriteFailed, result.ErrorCategory);
        AssertNoExportArtifacts(media.RootPath);
    }

    private static StreamingVideoExportService CreateService(
        ISequentialVideoFrameReader reader,
        IVideoEncoder encoder,
        IImagePipelineService pipeline) => new(
            reader,
            encoder,
            pipeline,
            new OutputDimensionResolver());

    private static VideoExportJob Job(
        string input,
        string output,
        bool overwrite,
        int width = 64,
        int height = 48) => new(
            input,
            output,
            new VideoMetadata(
                input,
                Path.GetFileName(input),
                width,
                height,
                TimeSpan.FromSeconds(1.2),
                10,
                12,
                "MP4",
                new FileInfo(input).Length,
                "h264",
                true,
                "aac",
                48_000),
            ImageRenderSettings.Default,
            CharacterPresetOption.Default,
            overwrite);

    private static void AssertNoExportArtifacts(string directory)
    {
        string[] forbiddenExtensions = [".partial", ".tmp", ".raw", ".png", ".jpg", ".jpeg"];
        Assert.DoesNotContain(
            Directory.EnumerateFiles(directory),
            path => forbiddenExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase));
    }

    private sealed class FakeFrameReader(int width, int height, int frameCount) : ISequentialVideoFrameReader
    {
        public Exception? Failure { get; init; }

        public IReadOnlyList<long>? SequenceNumbers { get; init; }

        public async IAsyncEnumerable<DecodedVideoFrame> ReadFramesAsync(
            SequentialVideoDecodeJob job,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            for (int frame = 0; frame < frameCount; frame++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] pixels = new byte[width * height * 4];
                for (int offset = 0; offset < pixels.Length; offset += 4)
                {
                    pixels[offset] = (byte)frame;
                    pixels[offset + 3] = 255;
                }

                long sequenceNumber = SequenceNumbers?[frame] ?? frame;
                yield return new DecodedVideoFrame(sequenceNumber, new DecodedImage(width, height, pixels));
                await Task.Yield();
            }
        }
    }

    private sealed class RecordingPipeline : IImagePipelineService
    {
        private int _activeRenders;

        public int MaximumConcurrentRenders { get; private set; }

        public Exception? Failure { get; init; }

        public Task<DecodedImage> DecodeAsync(
            string inputPath,
            ImageOrientation orientation,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public async Task<ImagePipelineResult> RenderAsync(
            DecodedImage source,
            ImageRenderSettings settings,
            CharacterPresetOption preset,
            CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            int active = Interlocked.Increment(ref _activeRenders);
            MaximumConcurrentRenders = Math.Max(MaximumConcurrentRenders, active);
            try
            {
                await Task.Yield();
                return new ImagePipelineResult(
                    new RenderedImage(source.Width, source.Height, source.RgbaPixels.ToArray()),
                    new SymbolGrid(1, 1, source.Dimensions),
                    new GlyphMetrics("Consolas", "Consolas", 10, 5, 10, 8, false));
            }
            finally
            {
                Interlocked.Decrement(ref _activeRenders);
            }
        }
    }

    private sealed class RecordingEncoder(bool writePartial) : IVideoEncoder
    {
        public VideoEncodingResult? Result { get; init; }

        public bool Cancel { get; init; }

        public Action? BeforeReturn { get; init; }

        public int Calls { get; private set; }

        public List<int> FrameMarkers { get; } = [];

        public List<PixelDimensions> FrameDimensions { get; } = [];

        public async Task<VideoEncodingResult> EncodeAsync(
            VideoEncodingJob job,
            IAsyncEnumerable<RenderedImage> frames,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            if (writePartial)
            {
                await File.WriteAllBytesAsync(job.OutputPath, [1, 2, 3], cancellationToken);
            }

            if (Cancel)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            long count = 0;
            await foreach (RenderedImage frame in frames.WithCancellation(cancellationToken))
            {
                FrameMarkers.Add(frame.RgbaPixels.Span[0]);
                FrameDimensions.Add(frame.Dimensions);
                count++;
            }

            BeforeReturn?.Invoke();
            return Result ?? new VideoEncodingResult(true, count);
        }
    }
}
