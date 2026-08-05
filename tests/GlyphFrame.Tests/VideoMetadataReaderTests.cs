using GlyphFrame.Infrastructure.Media;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class VideoMetadataReaderTests
{
    private readonly MetadataExtractorVideoMetadataReader _reader = new();

    [Theory]
    [InlineData("sample.mp4", "MP4")]
    [InlineData("sample.mov", "MOV")]
    [InlineData("sample.avi", "AVI")]
    [InlineData("sample.mkv", "MKV")]
    public async Task ReadAsync_RecognizesEveryApprovedVideoContainer(string fileName, string container)
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture(fileName);

        Core.Models.VideoMetadata metadata = await _reader.ReadAsync(path);

        Assert.Equal(container, metadata.Container);
        Assert.Equal(320, metadata.Width);
        Assert.Equal(240, metadata.Height);
        Assert.Equal(new FileInfo(path).Length, metadata.FileSizeBytes);
        Assert.True(metadata.Duration > TimeSpan.Zero);

        using FileStream exclusive = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.CanWrite);
    }

    [Fact]
    public async Task ReadAsync_MatroskaReportsTrackMetadataWithoutScanningFrames()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("sample.mkv", "metadata.mkv");

        Core.Models.VideoMetadata metadata = await _reader.ReadAsync(path);

        Assert.Equal(30d, metadata.Fps!.Value, precision: 3);
        Assert.Null(metadata.FrameCount);
        Assert.Equal("V_MPEG4/ISO/AVC", metadata.VideoCodec);
        Assert.True(metadata.HasAudio);
        Assert.Equal("A_AAC", metadata.AudioCodec);
        Assert.Equal(48_000, metadata.AudioSampleRate);
    }

    [Fact]
    public async Task ReadAsync_RepresentsVideoWithoutAudioTruthfully()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("silent.mkv");

        Core.Models.VideoMetadata metadata = await _reader.ReadAsync(path);

        Assert.False(metadata.HasAudio);
        Assert.Null(metadata.AudioCodec);
        Assert.Null(metadata.AudioSampleRate);
    }

    [Fact]
    public async Task ReadAsync_RejectsMalformedVideoAndReleasesTheFile()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("broken.mkv");

        await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadAsync(path));

        using FileStream exclusive = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.CanWrite);
    }
}
