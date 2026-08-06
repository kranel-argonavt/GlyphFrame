using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using SkiaSharp;

namespace GlyphFrame.Tests.TestInfrastructure;

internal static class Phase6TestImageFactory
{
    public static string Create(
        TemporaryMediaDirectory directory,
        string fileName,
        int width,
        int height,
        Func<int, int, SKColor>? pixelFactory = null)
    {
        string path = Path.Combine(directory.RootPath, fileName);
        pixelFactory ??= (x, y) => new SKColor(
            (byte)(32 + x * 40),
            (byte)(48 + y * 50),
            (byte)(64 + (x + y) * 20),
            255);

        if (Path.GetExtension(fileName).Equals(".bmp", StringComparison.OrdinalIgnoreCase))
        {
            WriteBmp(path, width, height, pixelFactory);
            return path;
        }

        SKEncodedImageFormat format = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => SKEncodedImageFormat.Png,
            ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
            ".webp" => SKEncodedImageFormat.Webp,
            _ => throw new ArgumentOutOfRangeException(nameof(fileName), fileName, "Unsupported test image extension.")
        };

        using SKBitmap bitmap = new(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, pixelFactory(x, y));
            }
        }

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(format, format == SKEncodedImageFormat.Jpeg ? 95 : 100)
            ?? throw new InvalidOperationException($"SkiaSharp could not encode the {format} test image.");
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
        return path;
    }

    public static DecodedImagePattern CreateDecoded(
        int width,
        int height,
        Func<int, int, SKColor> pixelFactory)
    {
        byte[] pixels = new byte[checked(width * height * 4)];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                SKColor color = pixelFactory(x, y);
                int offset = (y * width + x) * 4;
                pixels[offset] = color.Red;
                pixels[offset + 1] = color.Green;
                pixels[offset + 2] = color.Blue;
                pixels[offset + 3] = color.Alpha;
            }
        }

        return new DecodedImagePattern(width, height, pixels);
    }

    public static string CreateStraightAlphaPng(
        TemporaryMediaDirectory directory,
        string fileName,
        int width,
        int height,
        Func<int, int, SKColor> pixelFactory)
    {
        string path = Path.Combine(directory.RootPath, fileName);
        byte[] scanlines = new byte[checked(height * (1 + width * 4))];
        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * (1 + width * 4);
            scanlines[rowOffset] = 0;
            for (int x = 0; x < width; x++)
            {
                SKColor color = pixelFactory(x, y);
                int offset = rowOffset + 1 + x * 4;
                scanlines[offset] = color.Red;
                scanlines[offset + 1] = color.Green;
                scanlines[offset + 2] = color.Blue;
                scanlines[offset + 3] = color.Alpha;
            }
        }

        using MemoryStream compressed = new();
        using (ZLibStream zlib = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(scanlines);
        }

        using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        byte[] header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8;
        header[9] = 6;
        WritePngChunk(output, "IHDR", header);
        WritePngChunk(output, "IDAT", compressed.ToArray());
        WritePngChunk(output, "IEND", []);
        return path;
    }

    public static void AddExifOrientation(string jpegPath, ushort orientation)
    {
        if (orientation is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(orientation));
        }

        byte[] jpeg = File.ReadAllBytes(jpegPath);
        if (jpeg.Length < 2 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
        {
            throw new InvalidDataException("The test image is not a JPEG file.");
        }

        byte[] exif =
        [
            (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0,
            (byte)'I', (byte)'I', 42, 0, 8, 0, 0, 0,
            1, 0,
            0x12, 0x01, 3, 0, 1, 0, 0, 0,
            (byte)orientation, 0, 0, 0,
            0, 0, 0, 0
        ];
        byte[] result = new byte[checked(jpeg.Length + exif.Length + 4)];
        result[0] = 0xFF;
        result[1] = 0xD8;
        result[2] = 0xFF;
        result[3] = 0xE1;
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(4, 2), checked((ushort)(exif.Length + 2)));
        exif.CopyTo(result, 6);
        jpeg.AsSpan(2).CopyTo(result.AsSpan(6 + exif.Length));
        File.WriteAllBytes(jpegPath, result);
    }

    private static void WritePngChunk(Stream output, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);

        uint crc = 0xFFFFFFFF;
        foreach (byte value in typeBytes)
        {
            crc = UpdateCrc(crc, value);
        }

        foreach (byte value in data)
        {
            crc = UpdateCrc(crc, value);
        }

        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, ~crc);
        output.Write(crcBytes);
    }

    private static uint UpdateCrc(uint crc, byte value)
    {
        crc ^= value;
        for (int bit = 0; bit < 8; bit++)
        {
            crc = (crc & 1) != 0 ? 0xEDB88320U ^ (crc >> 1) : crc >> 1;
        }

        return crc;
    }

    private static void WriteBmp(
        string path,
        int width,
        int height,
        Func<int, int, SKColor> pixelFactory)
    {
        int rowStride = ((width * 3 + 3) / 4) * 4;
        int pixelBytes = checked(rowStride * height);
        byte[] bytes = new byte[checked(54 + pixelBytes)];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(2, 4), bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(10, 4), 54);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14, 4), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18, 4), width);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22, 4), height);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26, 2), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28, 2), 24);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(34, 4), pixelBytes);

        for (int y = 0; y < height; y++)
        {
            int destinationRow = 54 + (height - 1 - y) * rowStride;
            for (int x = 0; x < width; x++)
            {
                SKColor color = pixelFactory(x, y);
                int offset = destinationRow + x * 3;
                bytes[offset] = color.Blue;
                bytes[offset + 1] = color.Green;
                bytes[offset + 2] = color.Red;
            }
        }

        File.WriteAllBytes(path, bytes);
    }

    internal sealed record DecodedImagePattern(int Width, int Height, byte[] Pixels);
}
