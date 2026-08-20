using System.Globalization;
using GlyphFrame.Core.Models;
using GlyphFrame.Infrastructure.Ffmpeg;
using GlyphFrame.Infrastructure.Media;

namespace GlyphFrame.Tests;

public sealed class VideoExportPolicyTests
{
    [Theory]
    [InlineData(23.976, 23.976)]
    [InlineData(59.94, 59.94)]
    [InlineData(null, 30)]
    [InlineData(0d, 30)]
    [InlineData(-1d, 30)]
    [InlineData(double.NaN, 30)]
    [InlineData(double.PositiveInfinity, 30)]
    [InlineData(241d, 30)]
    public void NormalizeFramesPerSecond_UsesTrustedValueOrDeterministicFallback(
        double? source,
        double expected)
    {
        Assert.Equal(expected, VideoExportPolicy.NormalizeFramesPerSecond(source));
    }

    [Fact]
    public void FormatFramesPerSecond_IsInvariantAcrossCurrentCultures()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            Assert.Equal("59.94006", VideoExportPolicy.FormatFramesPerSecond(59.94006));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void NormalizeForYuv420_CropsOnlyTrailingOddRowAndColumn()
    {
        Assert.Equal(new PixelDimensions(320, 240), VideoExportPolicy.NormalizeForYuv420(new(321, 241)));
        Assert.Equal(new PixelDimensions(320, 240), VideoExportPolicy.NormalizeForYuv420(new(320, 240)));
    }

    [Fact]
    public void SequentialDecoderArguments_UseOneUnicodePathAndRawRgbaStdout()
    {
        const string path = "C:\\відео 日本語\\тестове відео.MOV";

        IReadOnlyList<string> arguments = FfmpegSequentialVideoFrameReader.CreateArguments(path);

        Assert.Equal(1, arguments.Count(argument => argument == path));
        Assert.Contains("0:v:0", arguments);
        Assert.Contains("rgba", arguments);
        Assert.Contains("rawvideo", arguments);
        Assert.Contains("pipe:1", arguments);
        Assert.Contains("-an", arguments);
    }

    [Fact]
    public void EncoderArguments_AreVideoOnlyH264Crf20MediumYuv420AndInvariant()
    {
        VideoEncodingJob job = new(
            "C:\\output 日本語\\video.partial",
            new PixelDimensions(320, 240),
            59.94);

        IReadOnlyList<string> arguments = FfmpegVideoEncoder.CreateArguments(job);

        AssertArgumentValue(arguments, "-f", "rawvideo");
        AssertArgumentValue(arguments, "-pix_fmt", "rgba", occurrence: 0);
        AssertArgumentValue(arguments, "-video_size", "320x240");
        AssertArgumentValue(arguments, "-framerate", "59.94");
        AssertArgumentValue(arguments, "-c:v", "libx264");
        AssertArgumentValue(arguments, "-crf", "20");
        AssertArgumentValue(arguments, "-preset", "medium");
        AssertArgumentValue(arguments, "-pix_fmt", "yuv420p", occurrence: 1);
        Assert.Contains("-an", arguments);
        Assert.Equal(job.OutputPath, arguments[^1]);
    }

    [Fact]
    public async Task SequentialFrameRead_ReportsShortFrameByteCount()
    {
        using MemoryStream truncated = new([1, 2, 3]);
        byte[] expectedFrame = new byte[8];

        int received = await FfmpegSequentialVideoFrameReader.ReadFrameAsync(
            truncated,
            expectedFrame,
            CancellationToken.None);

        Assert.Equal(3, received);
    }

    private static void AssertArgumentValue(
        IReadOnlyList<string> arguments,
        string option,
        string expected,
        int occurrence = 0)
    {
        int optionIndex = arguments
            .Select((value, index) => (value, index))
            .Where(item => item.value == option)
            .ElementAt(occurrence)
            .index;
        Assert.Equal(expected, arguments[optionIndex + 1]);
    }
}
