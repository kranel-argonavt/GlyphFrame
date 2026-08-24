using System.Globalization;
using System.Text.Json;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Infrastructure.Media;

public sealed class FfprobeAudioStreamProbe(
    IFfmpegValidationService validationService,
    IProcessRunner processRunner) : IAudioStreamProbe
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(20);
    private readonly IProcessRunner _processRunner = processRunner
        ?? throw new ArgumentNullException(nameof(processRunner));
    private readonly IFfmpegValidationService _validationService = validationService
        ?? throw new ArgumentNullException(nameof(validationService));

    public async Task<AudioProbeResult> ProbeAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
        {
            return AudioProbeResult.Failure(
                "GlyphFrame could not inspect source audio because the input file is unavailable.");
        }

        FfmpegValidationResult validation;
        try
        {
            validation = await _validationService.ValidateAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception
            or InvalidOperationException)
        {
            return AudioProbeResult.Failure(
                "GlyphFrame could not validate bundled ffprobe for audio inspection.",
                exception.Message,
                VideoExportErrorCategory.AudioMetadataUnavailable);
        }

        if (!validation.Ffprobe.IsAvailable)
        {
            return AudioProbeResult.Failure(
                "Bundled ffprobe is unavailable, so GlyphFrame cannot safely determine whether audio should be kept.",
                validation.Ffprobe.TechnicalDetails,
                VideoExportErrorCategory.AudioMetadataUnavailable);
        }

        ProcessExecutionResult result = await _processRunner.RunAsync(
            new ProcessExecutionRequest(
                validation.Ffprobe.ResolvedPath,
                [
                    "-v", "error",
                    "-select_streams", "a:0",
                    "-show_entries", "stream=codec_name,sample_rate,channels,start_time,duration",
                    "-show_entries", "format=duration",
                    "-of", "json",
                    inputPath
                ],
                ProbeTimeout),
            cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            string technical = $"ffprobe status: {result.Status}; exit code: {result.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "unavailable"}. {result.StandardError} {result.TechnicalDetail}".Trim();
            return AudioProbeResult.Failure(
                "GlyphFrame could not inspect the source audio stream.",
                technical);
        }

        if (string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return AudioProbeResult.Failure(
                "GlyphFrame could not inspect the source audio stream.",
                "ffprobe returned no JSON output.");
        }

        try
        {
            return Parse(result.StandardOutput);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or OverflowException)
        {
            return AudioProbeResult.Failure(
                "GlyphFrame could not understand the source audio metadata.",
                exception.Message);
        }
    }

    internal static AudioProbeResult Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("streams", out JsonElement streams)
            || streams.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("ffprobe output did not contain a streams array.");
        }

        JsonElement? stream = streams.EnumerateArray().Cast<JsonElement?>().FirstOrDefault();
        if (stream is null)
        {
            return AudioProbeResult.Absent();
        }

        return AudioProbeResult.Present(new AudioStreamInfo(
            GetString(stream.Value, "codec_name"),
            GetPositiveInt(stream.Value, "sample_rate"),
            GetPositiveInt(stream.Value, "channels"),
            GetTimeSpan(stream.Value, "duration"),
            GetTimeSpan(stream.Value, "start_time", allowNegative: true)));
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    private static int? GetPositiveInt(JsonElement element, string propertyName)
    {
        string? value = GetString(element, propertyName);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            && parsed > 0
                ? parsed
                : null;
    }

    private static TimeSpan? GetTimeSpan(
        JsonElement element,
        string propertyName,
        bool allowNegative = false)
    {
        string? value = GetString(element, propertyName);
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)
            || !double.IsFinite(seconds)
            || (!allowNegative && seconds < 0d))
        {
            return null;
        }

        return TimeSpan.FromSeconds(seconds);
    }
}
