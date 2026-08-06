using GlyphFrame.Core.Imaging;

namespace GlyphFrame.Tests;

public sealed class StraightAlphaPixelConverterTests
{
    [Fact]
    public void RgbaToBgra_PreservesStraightAlphaAndSwapsOnlyColorChannels()
    {
        byte[] rgba =
        [
            17, 34, 51, 255,
            255, 0, 0, 128,
            73, 149, 211, 0
        ];

        byte[] bgra = StraightAlphaPixelConverter.RgbaToBgra(rgba);

        Assert.Equal(
        [
            51, 34, 17, 255,
            0, 0, 255, 128,
            211, 149, 73, 0
        ],
        bgra);
        Assert.Equal(rgba, new byte[]
        {
            17, 34, 51, 255,
            255, 0, 0, 128,
            73, 149, 211, 0
        });
    }

    [Fact]
    public void RgbaToBgra_RejectsIncompletePixels()
    {
        Assert.Throws<ArgumentException>(() => StraightAlphaPixelConverter.RgbaToBgra([1, 2, 3]));
    }
}
