using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using SkiaSharp;

namespace GlyphFrame.Rendering.Skia;

public sealed class SkiaImageDecoder : IImageDecoder
{
    private const int MaximumDimension = 16_384;
    private const long MaximumPixels = 100_000_000;

    public Task<DecodedImage> DecodeAsync(
        string inputPath,
        ImageOrientation orientation,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Decode(inputPath, orientation, cancellationToken), cancellationToken);

    private static DecodedImage Decode(
        string inputPath,
        ImageOrientation orientation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(inputPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ImageProcessingException(
                ImageProcessingErrorCategory.InvalidPath,
                "The selected image path is not valid.",
                exception.Message,
                exception);
        }

        if (!File.Exists(fullPath))
        {
            throw new ImageProcessingException(
                ImageProcessingErrorCategory.FileNotFound,
                "The selected image no longer exists.");
        }

        try
        {
            using FileStream stream = new(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.SequentialScan);
            using SKCodec? codec = SKCodec.Create(stream, out SKCodecResult creationResult);
            if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0)
            {
                throw new ImageProcessingException(
                    ImageProcessingErrorCategory.InvalidImage,
                    "GlyphFrame could not decode this image.",
                    $"SkiaSharp could not create an image codec ({creationResult}).");
            }

            ValidateDimensions(codec.Info.Width, codec.Info.Height);
            cancellationToken.ThrowIfCancellationRequested();

            SKImageInfo decodeInfo = new(
                codec.Info.Width,
                codec.Info.Height,
                SKColorType.Rgba8888,
                SKAlphaType.Unpremul);
            byte[] sourcePixels = new byte[checked(codec.Info.Width * codec.Info.Height * 4)];
            SKCodecResult decodeResult = codec.GetPixels(decodeInfo, sourcePixels);
            if (decodeResult != SKCodecResult.Success)
            {
                throw new ImageProcessingException(
                    ImageProcessingErrorCategory.InvalidImage,
                    "GlyphFrame could not decode this image.",
                    $"The pixels could not be decoded as unpremultiplied RGBA8888 ({decodeResult}).");
            }

            byte[] normalizedPixels = NormalizeOrientation(
                sourcePixels,
                codec.Info.Width,
                codec.Info.Height,
                orientation,
                cancellationToken,
                out int normalizedWidth,
                out int normalizedHeight);
            return new DecodedImage(normalizedWidth, normalizedHeight, normalizedPixels);
        }
        catch (ImageProcessingException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new ImageProcessingException(
                ImageProcessingErrorCategory.AccessDenied,
                "GlyphFrame does not have permission to read this image.",
                exception.Message,
                exception);
        }
        catch (IOException exception)
        {
            throw new ImageProcessingException(
                ImageProcessingErrorCategory.InvalidImage,
                "GlyphFrame could not read this image.",
                exception.Message,
                exception);
        }
        catch (Exception exception)
        {
            throw new ImageProcessingException(
                ImageProcessingErrorCategory.InvalidImage,
                "GlyphFrame could not decode this image.",
                exception.Message,
                exception);
        }
    }

    private static void ValidateDimensions(int width, int height)
    {
        long pixels = (long)width * height;
        if (width > MaximumDimension || height > MaximumDimension || pixels > MaximumPixels)
        {
            throw new ImageProcessingException(
                ImageProcessingErrorCategory.UnsupportedDimensions,
                "This image is too large to process safely.",
                $"Decoded dimensions were {width} x {height} ({pixels:N0} pixels).");
        }
    }

    internal static byte[] NormalizeOrientation(
        byte[] source,
        int width,
        int height,
        ImageOrientation orientation,
        CancellationToken cancellationToken,
        out int normalizedWidth,
        out int normalizedHeight)
    {
        orientation = orientation == ImageOrientation.Unspecified ? ImageOrientation.Normal : orientation;
        bool swapsDimensions = orientation.SwapsDimensions();
        normalizedWidth = swapsDimensions ? height : width;
        normalizedHeight = swapsDimensions ? width : height;

        if (orientation == ImageOrientation.Normal)
        {
            return source;
        }

        byte[] destination = new byte[source.Length];
        for (int y = 0; y < height; y++)
        {
            if ((y & 31) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            for (int x = 0; x < width; x++)
            {
                (int destinationX, int destinationY) = orientation switch
                {
                    ImageOrientation.MirroredHorizontally => (width - 1 - x, y),
                    ImageOrientation.Rotated180 => (width - 1 - x, height - 1 - y),
                    ImageOrientation.MirroredVertically => (x, height - 1 - y),
                    ImageOrientation.MirroredHorizontallyThenRotated270Clockwise => (y, x),
                    ImageOrientation.Rotated90Clockwise => (height - 1 - y, x),
                    ImageOrientation.MirroredHorizontallyThenRotated90Clockwise => (height - 1 - y, width - 1 - x),
                    ImageOrientation.Rotated270Clockwise => (y, width - 1 - x),
                    _ => (x, y)
                };

                int sourceOffset = (y * width + x) * 4;
                int destinationOffset = (destinationY * normalizedWidth + destinationX) * 4;
                source.AsSpan(sourceOffset, 4).CopyTo(destination.AsSpan(destinationOffset, 4));
            }
        }

        return destination;
    }
}
