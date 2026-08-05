using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Media;

namespace GlyphFrame.Tests;

public sealed class MediaTypeDetectorTests
{
    private readonly MediaTypeDetector _detector = new();

    [Theory]
    [InlineData("photo.png")]
    [InlineData("photo.jpg")]
    [InlineData("photo.jpeg")]
    [InlineData("photo.webp")]
    [InlineData("photo.bmp")]
    [InlineData("PHOTO.PNG")]
    [InlineData("Photo.JpEg")]
    [InlineData("C:\\媒体\\фото.webp")]
    public void Detect_RecognizesEveryApprovedImageExtension(string path)
    {
        Assert.Equal(MediaSelectionKind.Image, _detector.Detect(path));
    }

    [Theory]
    [InlineData("video.mp4")]
    [InlineData("video.mov")]
    [InlineData("video.mkv")]
    [InlineData("video.avi")]
    [InlineData("VIDEO.MP4")]
    [InlineData("C:\\媒体\\фильм.MkV")]
    public void Detect_RecognizesEveryApprovedVideoExtension(string path)
    {
        Assert.Equal(MediaSelectionKind.Video, _detector.Detect(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-extension")]
    [InlineData("notes.txt")]
    [InlineData("archive.gif")]
    public void Detect_DoesNotClassifyUnknownFilesAsMedia(string path)
    {
        Assert.Equal(MediaSelectionKind.None, _detector.Detect(path));
    }
}
