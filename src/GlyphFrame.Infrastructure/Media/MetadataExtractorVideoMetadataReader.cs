using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Media;
using GlyphFrame.Core.Models;
using MetadataDirectory = MetadataExtractor.Directory;

namespace GlyphFrame.Infrastructure.Media;

public sealed class MetadataExtractorVideoMetadataReader : IVideoMetadataReader
{
    public async Task<VideoMetadata> ReadAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        if (Path.GetExtension(inputPath).Equals(".mkv", StringComparison.OrdinalIgnoreCase))
        {
            return await MatroskaMetadataReader.ReadAsync(inputPath, cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => ReadManagedContainer(inputPath, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
    }

    private static VideoMetadata ReadManagedContainer(string inputPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<MetadataDirectory> directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(inputPath);
        cancellationToken.ThrowIfCancellationRequested();

        string extension = Path.GetExtension(inputPath).ToLowerInvariant();
        bool isAvi = extension == ".avi";
        bool hasExpectedContainer = directories.Any(directory =>
            isAvi
                ? directory.GetType().Namespace?.Contains(".Avi", StringComparison.Ordinal) == true
                : directory.GetType().Namespace?.Contains(".QuickTime", StringComparison.Ordinal) == true);
        if (!hasExpectedContainer)
        {
            throw new InvalidDataException("The file content does not match its video extension.");
        }

        static bool IsVideoDirectory(MetadataDirectory directory) =>
            directory.Name.Contains("Video", StringComparison.OrdinalIgnoreCase)
            || directory.Name.Contains("AVI", StringComparison.OrdinalIgnoreCase)
            || directory.Name.Contains("Track Header", StringComparison.OrdinalIgnoreCase)
            || directory.GetType().Name.Contains("Video", StringComparison.OrdinalIgnoreCase)
            || directory.GetType().Name.Contains("Avi", StringComparison.OrdinalIgnoreCase);

        static bool IsAudioDirectory(MetadataDirectory directory) =>
            directory.Name.Contains("Audio", StringComparison.OrdinalIgnoreCase)
            || directory.Name.Contains("Sound", StringComparison.OrdinalIgnoreCase)
            || directory.GetType().Name.Contains("Audio", StringComparison.OrdinalIgnoreCase)
            || directory.GetType().Name.Contains("Sound", StringComparison.OrdinalIgnoreCase);

        int? width = MetadataValueReader.FindPositiveInt(directories, IsVideoDirectory, "Image Width", "Width", "Frame Width");
        int? height = MetadataValueReader.FindPositiveInt(directories, IsVideoDirectory, "Image Height", "Height", "Frame Height");
        double? fps = MetadataValueReader.FindDouble(directories, IsVideoDirectory, "Frame Rate", "Frames Per Second");
        long? frameCount = MetadataValueReader.FindPositiveLong(directories, IsVideoDirectory, "Total Frames", "Frame Count");
        TimeSpan? duration = MetadataValueReader.FindDuration(directories);
        string? videoCodec = MetadataValueReader.FindDescription(
            directories,
            IsVideoDirectory,
            "Compression Type",
            "Compressor Name",
            "Codec");
        bool hasVideoMetadata = width.HasValue || height.HasValue || duration.HasValue || fps.HasValue || frameCount.HasValue;
        if (!hasVideoMetadata)
        {
            throw new InvalidDataException("The video metadata is unavailable.");
        }

        bool hasAnyAudioDirectory = directories.Any(IsAudioDirectory);
        (bool HasVideo, bool HasAudio) handlers = isAvi
            ? default
            : InspectQuickTimeHandlers(inputPath);
        bool? hasAudio = hasAnyAudioDirectory || handlers.HasAudio
            ? true
            : handlers.HasVideo
                ? false
                : null;
        string? audioCodec = MetadataValueReader.FindDescription(
            directories,
            IsAudioDirectory,
            "Compression Type",
            "Format",
            "Codec");
        int? sampleRate = MetadataValueReader.FindPositiveInt(
            directories,
            IsAudioDirectory,
            "Sample Rate",
            "Audio Sample Rate");
        FileInfo file = new(inputPath);

        return new VideoMetadata(
            file.FullName,
            file.Name,
            width,
            height,
            duration,
            fps is > 0 ? fps : null,
            frameCount,
            SupportedMediaFormats.GetCanonicalFormat(inputPath),
            file.Length,
            videoCodec,
            hasAudio,
            audioCodec,
            sampleRate);
    }

    private static (bool HasVideo, bool HasAudio) InspectQuickTimeHandlers(string inputPath)
    {
        const int maximumHeaderBytes = 16 * 1024 * 1024;
        using FileStream stream = new(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        int length = checked((int)Math.Min(stream.Length, maximumHeaderBytes));
        byte[] buffer = new byte[length];
        stream.ReadExactly(buffer);
        bool hasVideo = false;
        bool hasAudio = false;

        for (int index = 4; index <= buffer.Length - 16; index++)
        {
            if (buffer[index] != (byte)'h'
                || buffer[index + 1] != (byte)'d'
                || buffer[index + 2] != (byte)'l'
                || buffer[index + 3] != (byte)'r')
            {
                continue;
            }

            ReadOnlySpan<byte> handler = buffer.AsSpan(index + 12, 4);
            hasVideo |= handler.SequenceEqual("vide"u8);
            hasAudio |= handler.SequenceEqual("soun"u8);
        }

        return (hasVideo, hasAudio);
    }
}
