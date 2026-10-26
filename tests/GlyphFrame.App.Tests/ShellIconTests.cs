using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GlyphFrame.App.Tests;

[Collection("WPF layout")]
public sealed class ShellIconTests
{
    private static byte[] ReadIcon() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "GlyphFrame.ico"));

    [Fact]
    public void Directory_ContainsAllShellSizesWith32BitTransparency()
    {
        byte[] bytes = ReadIcon();
        int[] sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
        Assert.Equal(0, BitConverter.ToUInt16(bytes, 0));
        Assert.Equal(1, BitConverter.ToUInt16(bytes, 2));
        Assert.Equal(sizes.Length, BitConverter.ToUInt16(bytes, 4));
        long previousEnd = 6 + sizes.Length * 16;
        for (int index = 0; index < sizes.Length; index++)
        {
            int entry = 6 + index * 16;
            Assert.Equal(sizes[index] == 256 ? 0 : sizes[index], bytes[entry]);
            Assert.Equal(bytes[entry], bytes[entry + 1]);
            Assert.Equal(1, BitConverter.ToUInt16(bytes, entry + 4));
            Assert.Equal(32, BitConverter.ToUInt16(bytes, entry + 6));
            uint length = BitConverter.ToUInt32(bytes, entry + 8);
            uint offset = BitConverter.ToUInt32(bytes, entry + 12);
            Assert.Equal(previousEnd, offset);
            previousEnd = offset + length;
            Assert.InRange(previousEnd, 1, bytes.LongLength);
        }
        Assert.Equal(bytes.LongLength, previousEnd);
    }

    [Theory]
    [InlineData(0, 16)]
    [InlineData(1, 20)]
    [InlineData(2, 24)]
    [InlineData(3, 32)]
    [InlineData(4, 40)]
    [InlineData(5, 48)]
    [InlineData(6, 64)]
    [InlineData(7, 128)]
    [InlineData(8, 256)]
    public void EveryFrame_IsWhiteArtworkOnTransparentBackground(int index, int size) => InputSettingsPolishTests.Sta(() =>
    {
        byte[] bytes = ReadIcon();
        int entry = 6 + index * 16;
        using MemoryStream png = new(bytes, (int)BitConverter.ToUInt32(bytes, entry + 12), (int)BitConverter.ToUInt32(bytes, entry + 8));
        BitmapFrame frame = Assert.Single(new PngBitmapDecoder(png, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames);
        Assert.Equal(size, frame.PixelWidth);
        Assert.Equal(size, frame.PixelHeight);
        FormatConvertedBitmap bitmap = new(frame, PixelFormats.Bgra32, null, 0);
        byte[] pixels = new byte[size * size * 4];
        bitmap.CopyPixels(pixels, size * 4, 0);
        int visible = 0;
        for (int pixel = 0; pixel < pixels.Length; pixel += 4)
        {
            if (pixels[pixel + 3] == 0) { continue; }
            visible++;
            Assert.Equal(255, pixels[pixel]);
            Assert.Equal(255, pixels[pixel + 1]);
            Assert.Equal(255, pixels[pixel + 2]);
        }
        Assert.InRange(visible, size * size / 10, size * size * 3 / 4);
    });
}
