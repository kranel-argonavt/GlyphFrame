using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace GlyphFrame.Infrastructure.Ffmpeg;

internal static class StreamingProcessSupport
{
    public const int MaximumErrorCharacters = 32_768;

    public static async Task<string> ReadStandardErrorAsync(TextReader reader)
    {
        char[] buffer = new char[4096];
        StringBuilder captured = new(MaximumErrorCharacters);
        while (true)
        {
            int read = await reader.ReadAsync(buffer).ConfigureAwait(false);
            if (read == 0)
            {
                return captured.ToString();
            }

            int remaining = MaximumErrorCharacters - captured.Length;
            if (remaining > 0)
            {
                captured.Append(buffer, 0, Math.Min(remaining, read));
            }
        }
    }

    public static async Task TerminateAsync(IStreamingProcessSession process)
    {
        if (process.HasExited)
        {
            return;
        }

        try
        {
            process.KillEntireProcessTree();
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or Win32Exception
            or NotSupportedException)
        {
            _ = exception;
        }

        if (process.HasExited)
        {
            return;
        }

        try
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException
            or InvalidOperationException
            or ObjectDisposedException)
        {
            _ = exception;
        }
    }

    public static ProcessStartInfo CreateStartInfo(
        string executablePath,
        IReadOnlyList<string> arguments,
        bool redirectStandardInput,
        bool redirectStandardOutput)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = redirectStandardInput,
            RedirectStandardOutput = redirectStandardOutput,
            RedirectStandardError = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
