using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Media;
using GlyphFrame.Core.Models;
using GlyphFrame.Infrastructure.Media;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class MediaInputServiceTests
{
    [Fact]
    public async Task LoadAsync_ValidatesExpectedPathFailures()
    {
        using TemporaryMediaDirectory media = new();
        MediaInputService service = CreateService();
        string folder = media.RootPath;
        string unsupported = media.CopyFixture("broken.png", "notes.txt");

        Assert.Equal(MediaInputErrorCategory.EmptyPath, (await service.LoadAsync(" ")).ErrorCategory);
        Assert.Equal(MediaInputErrorCategory.FileNotFound, (await service.LoadAsync(Path.Combine(folder, "missing.png"))).ErrorCategory);
        Assert.Equal(MediaInputErrorCategory.DirectorySelected, (await service.LoadAsync(folder)).ErrorCategory);
        Assert.Equal(MediaInputErrorCategory.UnsupportedExtension, (await service.LoadAsync(unsupported)).ErrorCategory);
    }

    [Fact]
    public async Task LoadAsync_DispatchesToTheCorrectReader()
    {
        using TemporaryMediaDirectory media = new();
        StubImageReader imageReader = new();
        StubVideoReader videoReader = new();
        MediaInputService service = new(new MediaTypeDetector(), imageReader, videoReader);
        string imagePath = media.CopyFixture("landscape.png", "image.png");
        string videoPath = media.CopyFixture("sample.mkv", "video.mkv");

        MediaInputResult image = await service.LoadAsync(imagePath);
        MediaInputResult video = await service.LoadAsync(videoPath);

        Assert.True(image.Success);
        Assert.True(video.Success);
        Assert.Equal(1, imageReader.CallCount);
        Assert.Equal(1, videoReader.CallCount);
    }

    [Fact]
    public async Task LoadAsync_MapsExclusiveLockToFriendlyStructuredFailure()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("landscape.png", "locked.png");
        MediaInputService service = CreateService();
        using FileStream lockStream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        MediaInputResult result = await service.LoadAsync(path);

        Assert.False(result.Success);
        Assert.Equal(MediaInputErrorCategory.FileInUse, result.ErrorCategory);
        Assert.DoesNotContain("IOException", result.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_MapsCorruptContentWithoutLeakingReaderDetails()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("broken.png");
        MediaInputService service = new(
            new MediaTypeDetector(),
            new ThrowingImageReader(),
            new StubVideoReader());

        MediaInputResult result = await service.LoadAsync(path);

        Assert.Equal(MediaInputErrorCategory.InvalidImage, result.ErrorCategory);
        Assert.Equal("GlyphFrame could not read this image.", result.UserMessage);
        Assert.Contains("fixture failure", result.TechnicalDetail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_ReturnsCancelledResultWhenReaderIsCancelled()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("landscape.png", "cancel.png");
        MediaInputService service = new(
            new MediaTypeDetector(),
            new CancellableImageReader(),
            new StubVideoReader());
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        MediaInputResult result = await service.LoadAsync(path, cancellation.Token);

        Assert.Equal(MediaInputErrorCategory.Cancelled, result.ErrorCategory);
    }

    [Fact]
    public async Task LoadAsync_ProcessesUnicodePathThroughTheRealImagePipeline()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("landscape.png", "画像-фото.PNG");
        MediaInputService service = new(
            new MediaTypeDetector(),
            new MetadataExtractorImageMetadataReader(),
            new MetadataExtractorVideoMetadataReader());

        MediaInputResult result = await service.LoadAsync(path);

        Assert.True(result.Success);
        Assert.Equal("画像-фото.PNG", result.ImageMetadata?.FileName);
        Assert.Equal(3, result.ImageMetadata?.Width);
    }

    private static MediaInputService CreateService() => new(
        new MediaTypeDetector(),
        new StubImageReader(),
        new StubVideoReader());

    private sealed class StubImageReader : IImageMetadataReader
    {
        public int CallCount { get; private set; }

        public Task<Core.Models.ImageMetadata> ReadAsync(string inputPath, CancellationToken cancellationToken = default)
        {
            CallCount++;
            FileInfo file = new(inputPath);
            return Task.FromResult(new Core.Models.ImageMetadata(
                file.FullName, file.Name, 3, 2, 3, 2, "PNG", file.Length,
                ImageOrientation.Unspecified, null));
        }
    }

    private sealed class StubVideoReader : IVideoMetadataReader
    {
        public int CallCount { get; private set; }

        public Task<Core.Models.VideoMetadata> ReadAsync(string inputPath, CancellationToken cancellationToken = default)
        {
            CallCount++;
            FileInfo file = new(inputPath);
            return Task.FromResult(new Core.Models.VideoMetadata(
                file.FullName, file.Name, 320, 240, TimeSpan.FromSeconds(5), 30, null,
                "MKV", file.Length, "V_TEST", false, null, null));
        }
    }

    private sealed class ThrowingImageReader : IImageMetadataReader
    {
        public Task<Core.Models.ImageMetadata> ReadAsync(string inputPath, CancellationToken cancellationToken = default) =>
            throw new InvalidDataException("fixture failure");
    }

    private sealed class CancellableImageReader : IImageMetadataReader
    {
        public async Task<Core.Models.ImageMetadata> ReadAsync(string inputPath, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException();
        }
    }
}
