using System.Text.RegularExpressions;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Infrastructure.Ffmpeg;

public sealed partial class FfmpegValidationService : IFfmpegValidationService, IDisposable
{
    private static readonly TimeSpan ValidationTimeout = TimeSpan.FromSeconds(10);
    private readonly IBundledFfmpegLocator _locator;
    private readonly IProcessRunner _processRunner;
    private readonly SemaphoreSlim _validationLock = new(1, 1);
    private FfmpegValidationResult? _cachedResult;
    private bool _disposed;

    public FfmpegValidationService(IBundledFfmpegLocator locator, IProcessRunner processRunner)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task<FfmpegValidationResult> ValidateAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!forceRefresh && _cachedResult is not null)
        {
            return _cachedResult;
        }

        await _validationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!forceRefresh && _cachedResult is not null)
            {
                return _cachedResult;
            }

            BundledFfmpegLocation location = _locator.Locate();
            Task<FfmpegToolValidationResult> ffmpegTask = ValidateToolAsync(location.Ffmpeg, cancellationToken);
            Task<FfmpegToolValidationResult> ffprobeTask = ValidateToolAsync(location.Ffprobe, cancellationToken);
            FfmpegToolValidationResult[] tools = await Task.WhenAll(ffmpegTask, ffprobeTask).ConfigureAwait(false);

            FfmpegValidationResult result = new(location.ToolsDirectory, tools[0], tools[1], DateTimeOffset.UtcNow);
            _cachedResult = result;
            return result;
        }
        finally
        {
            _validationLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _validationLock.Dispose();
        _disposed = true;
    }

    private async Task<FfmpegToolValidationResult> ValidateToolAsync(
        BundledToolPath tool,
        CancellationToken cancellationToken)
    {
        string displayName = tool.Tool == FfmpegToolKind.Ffmpeg ? "FFmpeg" : "ffprobe";
        if (tool.IsDirectory)
        {
            return Failure(
                tool,
                FfmpegToolValidationStatus.InvalidPath,
                $"Bundled {displayName} is unavailable because its executable path is a directory.",
                $"Expected a file at '{tool.ResolvedPath}'.");
        }

        if (!tool.Exists)
        {
            return Failure(
                tool,
                FfmpegToolValidationStatus.Missing,
                $"Bundled {displayName} is unavailable because {Path.GetFileName(tool.ResolvedPath)} is missing.",
                $"No file was found at '{tool.ResolvedPath}'.");
        }

        ProcessExecutionResult processResult = await _processRunner.RunAsync(
            new ProcessExecutionRequest(tool.ResolvedPath, ["-version"], ValidationTimeout),
            cancellationToken).ConfigureAwait(false);

        if (processResult.Status == ProcessExecutionStatus.AccessDenied)
        {
            return Failure(tool, FfmpegToolValidationStatus.AccessDenied,
                $"GlyphFrame does not have permission to start bundled {displayName}.", processResult.TechnicalDetail);
        }

        if (processResult.Status == ProcessExecutionStatus.StartFailed)
        {
            return Failure(tool, FfmpegToolValidationStatus.StartFailed,
                $"Bundled {displayName} could not be started.", processResult.TechnicalDetail);
        }

        if (processResult.Status == ProcessExecutionStatus.TimedOut)
        {
            return Failure(tool, FfmpegToolValidationStatus.TimedOut,
                $"Bundled {displayName} did not respond in time.", processResult.TechnicalDetail);
        }

        if (processResult.ExitCode != 0)
        {
            return Failure(tool, FfmpegToolValidationStatus.NonZeroExitCode,
                $"Bundled {displayName} reported an error during validation.",
                $"Exit code: {processResult.ExitCode}{Environment.NewLine}{processResult.StandardError}");
        }

        string combinedOutput = string.Concat(processResult.StandardOutput, Environment.NewLine, processResult.StandardError);
        Match match = VersionLineRegex().Match(combinedOutput);
        string expectedName = tool.Tool == FfmpegToolKind.Ffmpeg ? "ffmpeg" : "ffprobe";
        if (!match.Success || !match.Groups["tool"].Value.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
        {
            return Failure(tool, FfmpegToolValidationStatus.InvalidVersionOutput,
                $"Bundled {displayName} returned an unexpected version response.",
                string.IsNullOrWhiteSpace(combinedOutput) ? "Version output was empty." : combinedOutput.Trim());
        }

        string version = match.Groups["version"].Value;
        return new FfmpegToolValidationResult(
            tool.Tool,
            FfmpegToolValidationStatus.Available,
            tool.ResolvedPath,
            version,
            $"{displayName} {version} is available.",
            $"Validated successfully in {processResult.Duration.TotalMilliseconds:F0} ms.{Environment.NewLine}{match.Value.Trim()}");
    }

    private static FfmpegToolValidationResult Failure(
        BundledToolPath tool,
        FfmpegToolValidationStatus status,
        string userMessage,
        string? technicalDetails) =>
        new(tool.Tool, status, tool.ResolvedPath, null, userMessage, technicalDetails);

    [GeneratedRegex(@"(?im)^(?<tool>ffmpeg|ffprobe)\s+version\s+(?<version>\S+).*$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionLineRegex();
}
