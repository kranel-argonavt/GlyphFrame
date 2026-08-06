using System.Runtime.InteropServices;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using SkiaSharp;

namespace GlyphFrame.Rendering.Skia;

public sealed class SkiaImageExporter : IImageExporter
{
    public const int JpegQuality = 90;

    public Task ExportAsync(
        RenderedImage image,
        string outputPath,
        ImageExportFormat format,
        bool overwrite,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Export(image, outputPath, format, overwrite, cancellationToken), cancellationToken);

    private static void Export(
        RenderedImage image,
        string outputPath,
        ImageExportFormat format,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        cancellationToken.ThrowIfCancellationRequested();

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(outputPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ImageProcessingException(
                ImageProcessingErrorCategory.OutputUnavailable,
                "The selected output path is not valid.",
                exception.Message,
                exception);
        }

        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new ImageProcessingException(
                ImageProcessingErrorCategory.OutputUnavailable,
                "The selected output folder is not available.");
        }

        if (File.Exists(fullPath) && !overwrite)
        {
            throw new ImageProcessingException(
                ImageProcessingErrorCategory.OutputUnavailable,
                "The output file already exists and was not overwritten.");
        }

        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            SKImageInfo info = new(
                image.Dimensions.Width,
                image.Dimensions.Height,
                SKColorType.Rgba8888,
                SKAlphaType.Opaque);
            using SKBitmap bitmap = new(info);
            Marshal.Copy(image.RgbaPixels.ToArray(), 0, bitmap.GetPixels(), image.RgbaPixels.Length);
            using SKImage skImage = SKImage.FromBitmap(bitmap);
            SKEncodedImageFormat encodedFormat = format switch
            {
                ImageExportFormat.Png => SKEncodedImageFormat.Png,
                ImageExportFormat.Jpeg => SKEncodedImageFormat.Jpeg,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported image format.")
            };
            int quality = format == ImageExportFormat.Jpeg ? JpegQuality : 100;
            using SKData? encoded = skImage.Encode(encodedFormat, quality);
            if (encoded is null)
            {
                throw new ImageProcessingException(
                    ImageProcessingErrorCategory.EncodingFailed,
                    "GlyphFrame could not encode the output image.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.WriteThrough))
            {
                encoded.SaveTo(stream);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, fullPath, overwrite);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ImageProcessingException)
        {
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new ImageProcessingException(
                ImageProcessingErrorCategory.AccessDenied,
                "GlyphFrame does not have permission to write this file.",
                exception.Message,
                exception);
        }
        catch (IOException exception)
        {
            throw new ImageProcessingException(
                ImageProcessingErrorCategory.OutputUnavailable,
                "GlyphFrame could not write the output image.",
                exception.Message,
                exception);
        }
        catch (Exception exception)
        {
            throw new ImageProcessingException(
                ImageProcessingErrorCategory.EncodingFailed,
                "GlyphFrame could not export the image.",
                exception.Message,
                exception);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (IOException)
            {
                // The final exception remains the actionable result; cleanup is best effort.
            }
            catch (UnauthorizedAccessException)
            {
                // The final exception remains the actionable result; cleanup is best effort.
            }
        }
    }
}
