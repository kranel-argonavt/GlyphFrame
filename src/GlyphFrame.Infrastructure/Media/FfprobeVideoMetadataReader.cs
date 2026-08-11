using System.Globalization;
using System.Text.Json;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Infrastructure.Media;

public sealed class FfprobeVideoMetadataReader : IVideoMetadataReader
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(20);
    private readonly MetadataExtractorVideoMetadataReader _fallbackReader;
    private readonly IFfmpegValidationService _validationService;
    private readonly IProcessRunner _processRunner;

    public FfprobeVideoMetadataReader(
        MetadataExtractorVideoMetadataReader fallbackReader,
        IFfmpegValidationService validationService,
        IProcessRunner processRunner)
    {
        _fallbackReader = fallbackReader ?? throw new ArgumentNullException(nameof(fallbackReader));
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task<VideoMetadata> ReadAsync(string inputPath, CancellationToken cancellationToken = default)
    {
        VideoMetadata fallback = await _fallbackReader.ReadAsync(inputPath, cancellationToken).ConfigureAwait(false);
        FfmpegValidationResult validation = await _validationService.ValidateAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!validation.Ffprobe.IsAvailable)
        {
            return fallback;
        }

        ProcessExecutionResult probe = await _processRunner.RunAsync(
            new ProcessExecutionRequest(
                validation.Ffprobe.ResolvedPath,
                [
                    "-v", "error",
                    "-print_format", "json",
                    "-show_streams",
                    "-show_format",
                    inputPath
                ],
                ProbeTimeout),
            cancellationToken).ConfigureAwait(false);

        if (!probe.Succeeded || string.IsNullOrWhiteSpace(probe.StandardOutput))
        {
            return fallback;
        }

        try
        {
            return Enrich(fallback, probe.StandardOutput);
        }
        catch (JsonException)
        {
            return fallback;
        }
        catch (FormatException)
        {
            return fallback;
        }
        catch (OverflowException)
        {
            return fallback;
        }
    }

    internal static VideoMetadata Enrich(VideoMetadata fallback, string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement? videoStream = FindStream(root, "video");
        JsonElement? audioStream = FindStream(root, "audio");
        JsonElement? format = root.TryGetProperty("format", out JsonElement formatElement) ? formatElement : null;

        int? width = GetPositiveInt(videoStream, "width") ?? fallback.Width;
        int? height = GetPositiveInt(videoStream, "height") ?? fallback.Height;
        TimeSpan? duration = GetDuration(videoStream) ?? GetDuration(format) ?? fallback.Duration;
        double? fps = GetFrameRate(videoStream) ?? fallback.Fps;
        long? frameCount = GetPositiveLong(videoStream, "nb_frames") ?? fallback.FrameCount;
        string? videoCodec = GetNonEmptyString(videoStream, "codec_name") ?? fallback.VideoCodec;
        string? audioCodec = GetNonEmptyString(audioStream, "codec_name") ?? fallback.AudioCodec;
        int? audioSampleRate = GetPositiveInt(audioStream, "sample_rate") ?? fallback.AudioSampleRate;
        bool? hasAudio = audioStream.HasValue
            ? true
            : HasStreamCatalog(root)
                ? false
                : fallback.HasAudio;
        return fallback with
        {
            Width = width,
            Height = height,
            Duration = duration,
            Fps = fps,
            FrameCount = frameCount,
            VideoCodec = videoCodec,
            HasAudio = hasAudio,
            AudioCodec = audioCodec,
            AudioSampleRate = audioSampleRate
        };
    }

    private static JsonElement? FindStream(JsonElement root, string codecType)
    {
        if (!root.TryGetProperty("streams", out JsonElement streams) || streams.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement stream in streams.EnumerateArray())
        {
            if (GetNonEmptyString(stream, "codec_type")?.Equals(codecType, StringComparison.OrdinalIgnoreCase) == true)
            {
                return stream;
            }
        }

        return null;
    }

    private static bool HasStreamCatalog(JsonElement root) =>
        root.TryGetProperty("streams", out JsonElement streams) && streams.ValueKind == JsonValueKind.Array;

    private static string? GetNonEmptyString(JsonElement? element, string propertyName)
    {
        if (!element.HasValue
            || !element.Value.TryGetProperty(propertyName, out JsonElement property)
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? value = property.GetString();
        return string.IsNullOrWhiteSpace(value) || value.Equals("N/A", StringComparison.OrdinalIgnoreCase) ? null : value;
    }

    private static int? GetPositiveInt(JsonElement? element, string propertyName)
    {
        string? text = GetInvariantText(element, propertyName);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0
            ? value
            : null;
    }

    private static long? GetPositiveLong(JsonElement? element, string propertyName)
    {
        string? text = GetInvariantText(element, propertyName);
        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) && value > 0
            ? value
            : null;
    }

    private static TimeSpan? GetDuration(JsonElement? element)
    {
        string? text = GetInvariantText(element, "duration");
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : null;
    }

    private static double? GetFrameRate(JsonElement? stream)
    {
        string? rational = GetNonEmptyString(stream, "avg_frame_rate")
            ?? GetNonEmptyString(stream, "r_frame_rate");
        if (rational is null)
        {
            return null;
        }

        string[] parts = rational.Split('/', 2);
        if (parts.Length != 2
            || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double numerator)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double denominator)
            || numerator <= 0
            || denominator <= 0)
        {
            return null;
        }

        double fps = numerator / denominator;
        return double.IsFinite(fps) && fps > 0 ? fps : null;
    }

    private static string? GetInvariantText(JsonElement? element, string propertyName)
    {
        if (!element.HasValue || !element.Value.TryGetProperty(propertyName, out JsonElement property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null
        };
    }
}
