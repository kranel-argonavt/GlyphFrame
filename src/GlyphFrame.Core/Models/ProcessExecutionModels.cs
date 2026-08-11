using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

public sealed record ProcessExecutionRequest
{
    public ProcessExecutionRequest(
        string executablePath,
        IEnumerable<string>? arguments = null,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        TimeSpan resolvedTimeout = timeout ?? TimeSpan.FromSeconds(30);
        if (resolvedTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "The process timeout must be positive.");
        }

        ExecutablePath = executablePath;
        Arguments = (arguments ?? []).ToArray();
        Timeout = resolvedTimeout;
    }

    public string ExecutablePath { get; }

    public IReadOnlyList<string> Arguments { get; }

    public TimeSpan Timeout { get; }
}

public sealed record ProcessExecutionResult(
    ProcessExecutionStatus Status,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration,
    string? TechnicalDetail = null)
{
    public bool Started => Status != ProcessExecutionStatus.StartFailed;

    public bool Succeeded => Status == ProcessExecutionStatus.Completed && ExitCode == 0;
}
