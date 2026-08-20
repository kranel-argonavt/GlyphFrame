using System.Diagnostics;

namespace GlyphFrame.Infrastructure.Ffmpeg;

internal interface IStreamingProcessSession : IAsyncDisposable
{
    Stream StandardInput { get; }

    Stream StandardOutput { get; }

    TextReader StandardError { get; }

    bool HasExited { get; }

    int ExitCode { get; }

    void CompleteStandardInput();

    Task WaitForExitAsync(CancellationToken cancellationToken);

    void KillEntireProcessTree();
}

internal interface IStreamingProcessFactory
{
    IStreamingProcessSession Start(
        string executablePath,
        IReadOnlyList<string> arguments,
        bool redirectStandardInput,
        bool redirectStandardOutput);
}

internal sealed class SystemStreamingProcessFactory : IStreamingProcessFactory
{
    public IStreamingProcessSession Start(
        string executablePath,
        IReadOnlyList<string> arguments,
        bool redirectStandardInput,
        bool redirectStandardOutput)
    {
        Process process = new()
        {
            StartInfo = StreamingProcessSupport.CreateStartInfo(
                executablePath,
                arguments,
                redirectStandardInput,
                redirectStandardOutput)
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("The operating system did not start FFmpeg.");
            }

            return new SystemStreamingProcessSession(process, redirectStandardInput, redirectStandardOutput);
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }
}

internal sealed class SystemStreamingProcessSession(
    Process process,
    bool hasStandardInput,
    bool hasStandardOutput) : IStreamingProcessSession
{
    private readonly Process _process = process ?? throw new ArgumentNullException(nameof(process));
    private bool _inputCompleted;

    public Stream StandardInput => hasStandardInput ? _process.StandardInput.BaseStream : Stream.Null;

    public Stream StandardOutput => hasStandardOutput ? _process.StandardOutput.BaseStream : Stream.Null;

    public TextReader StandardError => _process.StandardError;

    public bool HasExited => _process.HasExited;

    public int ExitCode => _process.ExitCode;

    public void CompleteStandardInput()
    {
        if (hasStandardInput && !_inputCompleted)
        {
            _process.StandardInput.Close();
            _inputCompleted = true;
        }
    }

    public Task WaitForExitAsync(CancellationToken cancellationToken) =>
        _process.WaitForExitAsync(cancellationToken);

    public void KillEntireProcessTree() => _process.Kill(entireProcessTree: true);

    public ValueTask DisposeAsync()
    {
        _process.Dispose();
        return ValueTask.CompletedTask;
    }
}
