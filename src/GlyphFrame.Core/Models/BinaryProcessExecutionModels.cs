using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

public sealed record BinaryProcessExecutionRequest
{
    public BinaryProcessExecutionRequest(
        string executablePath,
        IEnumerable<string>? arguments,
        TimeSpan timeout,
        int maximumStandardOutputBytes,
        int maximumStandardErrorCharacters = 32_768)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        if (maximumStandardOutputBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumStandardOutputBytes));
        }

        if (maximumStandardErrorCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumStandardErrorCharacters));
        }

        ExecutablePath = executablePath;
        Arguments = (arguments ?? []).ToArray();
        Timeout = timeout;
        MaximumStandardOutputBytes = maximumStandardOutputBytes;
        MaximumStandardErrorCharacters = maximumStandardErrorCharacters;
    }

    public string ExecutablePath { get; }

    public IReadOnlyList<string> Arguments { get; }

    public TimeSpan Timeout { get; }

    public int MaximumStandardOutputBytes { get; }

    public int MaximumStandardErrorCharacters { get; }
}

public sealed record BinaryProcessExecutionResult(
    ProcessExecutionStatus Status,
    int? ExitCode,
    byte[] StandardOutput,
    string StandardError,
    TimeSpan Duration,
    bool StandardOutputExceededLimit = false,
    bool StandardErrorWasTruncated = false,
    string? TechnicalDetail = null)
{
    public bool Succeeded => Status == ProcessExecutionStatus.Completed && ExitCode == 0;
}
