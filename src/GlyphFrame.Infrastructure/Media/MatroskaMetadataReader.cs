using System.Buffers.Binary;
using System.Text;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Infrastructure.Media;

internal static class MatroskaMetadataReader
{
    private const long MaxMetadataScanBytes = 16 * 1024 * 1024;
    private const ulong EbmlId = 0x1A45DFA3;
    private const ulong DocTypeId = 0x4282;
    private const ulong SegmentId = 0x18538067;
    private const ulong InfoId = 0x1549A966;
    private const ulong TracksId = 0x1654AE6B;
    private const ulong ClusterId = 0x1F43B675;
    private const ulong TimecodeScaleId = 0x2AD7B1;
    private const ulong DurationId = 0x4489;
    private const ulong TrackEntryId = 0xAE;
    private const ulong TrackTypeId = 0x83;
    private const ulong CodecId = 0x86;
    private const ulong DefaultDurationId = 0x23E383;
    private const ulong VideoId = 0xE0;
    private const ulong AudioId = 0xE1;
    private const ulong PixelWidthId = 0xB0;
    private const ulong PixelHeightId = 0xBA;
    private const ulong SamplingFrequencyId = 0xB5;

    public static async Task<VideoMetadata> ReadAsync(
        string inputPath,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            inputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        ElementHeader ebml = await ReadRequiredHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
        if (ebml.Id != EbmlId || ebml.IsUnknownSize)
        {
            throw new InvalidDataException("The Matroska EBML header is invalid.");
        }

        string? docType = await ReadDocTypeAsync(stream, ebml.EndOffset, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(docType, "matroska", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The file is not a Matroska container.");
        }

        stream.Position = ebml.EndOffset;
        ElementHeader segment = await FindSegmentAsync(stream, cancellationToken).ConfigureAwait(false);
        long segmentEnd = segment.IsUnknownSize ? stream.Length : segment.EndOffset;
        long scanLimit = Math.Min(segmentEnd, checked(segment.DataOffset + MaxMetadataScanBytes));
        MatroskaValues values = new();

        while (stream.Position < scanLimit)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ElementHeader? child = await ReadHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
            if (child is null)
            {
                break;
            }

            if (child.Value.Id == InfoId)
            {
                await ReadInfoAsync(stream, child.Value.EndOffset, values, cancellationToken).ConfigureAwait(false);
                values.InfoRead = true;
            }
            else if (child.Value.Id == TracksId)
            {
                await ReadTracksAsync(stream, child.Value.EndOffset, values, cancellationToken).ConfigureAwait(false);
                values.TracksRead = true;
            }
            else if (child.Value.Id == ClusterId)
            {
                break;
            }
            else
            {
                SeekPast(stream, child.Value, segmentEnd);
            }

            if (values.InfoRead && values.TracksRead)
            {
                break;
            }
        }

        if (!values.TracksRead || values.Width is null || values.Height is null)
        {
            throw new InvalidDataException("The Matroska video track metadata is unavailable.");
        }

        TimeSpan? duration = values.DurationUnits is > 0
            ? TimeSpan.FromTicks(Convert.ToInt64(values.DurationUnits.Value * values.TimecodeScaleNanoseconds / 100d))
            : null;
        double? fps = values.DefaultDurationNanoseconds is > 0
            ? 1_000_000_000d / values.DefaultDurationNanoseconds.Value
            : null;
        FileInfo file = new(inputPath);

        return new VideoMetadata(
            file.FullName,
            file.Name,
            values.Width,
            values.Height,
            duration,
            fps,
            null,
            "MKV",
            file.Length,
            values.VideoCodec,
            values.HasAudio,
            values.AudioCodec,
            values.AudioSampleRate);
    }

    private static async Task<string?> ReadDocTypeAsync(
        FileStream stream,
        long endOffset,
        CancellationToken cancellationToken)
    {
        while (stream.Position < endOffset)
        {
            ElementHeader child = await ReadRequiredHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
            if (child.Id == DocTypeId)
            {
                return await ReadStringAsync(stream, child.Size, cancellationToken).ConfigureAwait(false);
            }

            SeekPast(stream, child, endOffset);
        }

        return null;
    }

    private static async Task<ElementHeader> FindSegmentAsync(
        FileStream stream,
        CancellationToken cancellationToken)
    {
        while (stream.Position < stream.Length)
        {
            ElementHeader header = await ReadRequiredHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
            if (header.Id == SegmentId)
            {
                return header;
            }

            SeekPast(stream, header, stream.Length);
        }

        throw new InvalidDataException("The Matroska segment is missing.");
    }

    private static async Task ReadInfoAsync(
        FileStream stream,
        long endOffset,
        MatroskaValues values,
        CancellationToken cancellationToken)
    {
        while (stream.Position < endOffset)
        {
            ElementHeader child = await ReadRequiredHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
            switch (child.Id)
            {
                case TimecodeScaleId:
                    values.TimecodeScaleNanoseconds = checked((long)await ReadUnsignedAsync(stream, child.Size, cancellationToken).ConfigureAwait(false));
                    break;
                case DurationId:
                    values.DurationUnits = await ReadFloatAsync(stream, child.Size, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    SeekPast(stream, child, endOffset);
                    break;
            }
        }
    }

    private static async Task ReadTracksAsync(
        FileStream stream,
        long endOffset,
        MatroskaValues values,
        CancellationToken cancellationToken)
    {
        while (stream.Position < endOffset)
        {
            ElementHeader child = await ReadRequiredHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
            if (child.Id == TrackEntryId)
            {
                await ReadTrackEntryAsync(stream, child.EndOffset, values, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                SeekPast(stream, child, endOffset);
            }
        }

        values.HasAudio ??= false;
    }

    private static async Task ReadTrackEntryAsync(
        FileStream stream,
        long endOffset,
        MatroskaValues values,
        CancellationToken cancellationToken)
    {
        ulong? trackType = null;
        string? codec = null;
        long? defaultDuration = null;
        int? width = null;
        int? height = null;
        int? sampleRate = null;

        while (stream.Position < endOffset)
        {
            ElementHeader child = await ReadRequiredHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
            switch (child.Id)
            {
                case TrackTypeId:
                    trackType = await ReadUnsignedAsync(stream, child.Size, cancellationToken).ConfigureAwait(false);
                    break;
                case CodecId:
                    codec = await ReadStringAsync(stream, child.Size, cancellationToken).ConfigureAwait(false);
                    break;
                case DefaultDurationId:
                    defaultDuration = checked((long)await ReadUnsignedAsync(stream, child.Size, cancellationToken).ConfigureAwait(false));
                    break;
                case VideoId:
                    (width, height) = await ReadVideoAsync(stream, child.EndOffset, cancellationToken).ConfigureAwait(false);
                    break;
                case AudioId:
                    sampleRate = await ReadAudioAsync(stream, child.EndOffset, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    SeekPast(stream, child, endOffset);
                    break;
            }
        }

        if (trackType == 1)
        {
            values.Width ??= width;
            values.Height ??= height;
            values.VideoCodec ??= codec;
            values.DefaultDurationNanoseconds ??= defaultDuration;
        }
        else if (trackType == 2)
        {
            values.HasAudio = true;
            values.AudioCodec ??= codec;
            values.AudioSampleRate ??= sampleRate;
        }
    }

    private static async Task<(int? Width, int? Height)> ReadVideoAsync(
        FileStream stream,
        long endOffset,
        CancellationToken cancellationToken)
    {
        int? width = null;
        int? height = null;
        while (stream.Position < endOffset)
        {
            ElementHeader child = await ReadRequiredHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
            if (child.Id == PixelWidthId)
            {
                width = checked((int)await ReadUnsignedAsync(stream, child.Size, cancellationToken).ConfigureAwait(false));
            }
            else if (child.Id == PixelHeightId)
            {
                height = checked((int)await ReadUnsignedAsync(stream, child.Size, cancellationToken).ConfigureAwait(false));
            }
            else
            {
                SeekPast(stream, child, endOffset);
            }
        }

        return (width, height);
    }

    private static async Task<int?> ReadAudioAsync(
        FileStream stream,
        long endOffset,
        CancellationToken cancellationToken)
    {
        int? sampleRate = null;
        while (stream.Position < endOffset)
        {
            ElementHeader child = await ReadRequiredHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
            if (child.Id == SamplingFrequencyId)
            {
                sampleRate = Convert.ToInt32(Math.Round(await ReadFloatAsync(stream, child.Size, cancellationToken).ConfigureAwait(false)));
            }
            else
            {
                SeekPast(stream, child, endOffset);
            }
        }

        return sampleRate;
    }

    private static async Task<ElementHeader> ReadRequiredHeaderAsync(
        FileStream stream,
        CancellationToken cancellationToken) =>
        await ReadHeaderAsync(stream, cancellationToken).ConfigureAwait(false)
        ?? throw new EndOfStreamException("The EBML element header is truncated.");

    private static async Task<ElementHeader?> ReadHeaderAsync(
        FileStream stream,
        CancellationToken cancellationToken)
    {
        int firstId = await ReadByteAsync(stream, cancellationToken).ConfigureAwait(false);
        if (firstId < 0)
        {
            return null;
        }

        (ulong id, _) = await ReadVariableIntegerAsync(stream, (byte)firstId, preserveMarker: true, cancellationToken).ConfigureAwait(false);
        int firstSize = await ReadByteAsync(stream, cancellationToken).ConfigureAwait(false);
        if (firstSize < 0)
        {
            throw new EndOfStreamException("The EBML element size is missing.");
        }

        (ulong size, int sizeLength) = await ReadVariableIntegerAsync(
            stream,
            (byte)firstSize,
            preserveMarker: false,
            cancellationToken).ConfigureAwait(false);
        ulong unknownValue = (1UL << (sizeLength * 7)) - 1;
        bool isUnknown = size == unknownValue;
        long dataOffset = stream.Position;
        long longSize = isUnknown
            ? 0
            : size <= long.MaxValue
                ? (long)size
                : throw new InvalidDataException("The EBML element is too large.");
        if (!isUnknown && dataOffset + longSize > stream.Length)
        {
            throw new EndOfStreamException("The EBML element payload is truncated.");
        }

        return new ElementHeader(id, longSize, dataOffset, isUnknown);
    }

    private static async Task<(ulong Value, int Length)> ReadVariableIntegerAsync(
        FileStream stream,
        byte firstByte,
        bool preserveMarker,
        CancellationToken cancellationToken)
    {
        int length = 1;
        byte mask = 0x80;
        while (length <= 8 && (firstByte & mask) == 0)
        {
            length++;
            mask >>= 1;
        }

        if (length > 8)
        {
            throw new InvalidDataException("The EBML variable integer is invalid.");
        }

        ulong value = preserveMarker ? firstByte : (ulong)(firstByte & (mask - 1));
        for (int index = 1; index < length; index++)
        {
            int next = await ReadByteAsync(stream, cancellationToken).ConfigureAwait(false);
            if (next < 0)
            {
                throw new EndOfStreamException("The EBML variable integer is truncated.");
            }

            value = (value << 8) | (byte)next;
        }

        return (value, length);
    }

    private static async Task<ulong> ReadUnsignedAsync(
        FileStream stream,
        long size,
        CancellationToken cancellationToken)
    {
        if (size is < 1 or > 8)
        {
            throw new InvalidDataException("The EBML unsigned integer size is invalid.");
        }

        byte[] buffer = new byte[size];
        await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        ulong value = 0;
        foreach (byte current in buffer)
        {
            value = (value << 8) | current;
        }

        return value;
    }

    private static async Task<double> ReadFloatAsync(
        FileStream stream,
        long size,
        CancellationToken cancellationToken)
    {
        if (size == 4)
        {
            byte[] buffer = new byte[4];
            await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
            return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(buffer));
        }

        if (size == 8)
        {
            byte[] buffer = new byte[8];
            await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
            return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(buffer));
        }

        throw new InvalidDataException("The EBML floating-point size is invalid.");
    }

    private static async Task<string> ReadStringAsync(
        FileStream stream,
        long size,
        CancellationToken cancellationToken)
    {
        if (size is < 0 or > 1024)
        {
            throw new InvalidDataException("The EBML string is too large.");
        }

        byte[] buffer = new byte[size];
        await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(buffer).TrimEnd('\0');
    }

    private static async Task<int> ReadByteAsync(FileStream stream, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[1];
        int read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        return read == 0 ? -1 : buffer[0];
    }

    private static void SeekPast(FileStream stream, ElementHeader header, long parentEnd)
    {
        if (header.IsUnknownSize)
        {
            throw new InvalidDataException("An unexpected unknown-sized EBML element was found.");
        }

        if (header.EndOffset > parentEnd)
        {
            throw new InvalidDataException("An EBML child exceeds its parent element.");
        }

        stream.Position = header.EndOffset;
    }

    private readonly record struct ElementHeader(ulong Id, long Size, long DataOffset, bool IsUnknownSize)
    {
        public long EndOffset => checked(DataOffset + Size);
    }

    private sealed class MatroskaValues
    {
        public bool InfoRead { get; set; }
        public bool TracksRead { get; set; }
        public long TimecodeScaleNanoseconds { get; set; } = 1_000_000;
        public double? DurationUnits { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public long? DefaultDurationNanoseconds { get; set; }
        public string? VideoCodec { get; set; }
        public bool? HasAudio { get; set; }
        public string? AudioCodec { get; set; }
        public int? AudioSampleRate { get; set; }
    }
}
