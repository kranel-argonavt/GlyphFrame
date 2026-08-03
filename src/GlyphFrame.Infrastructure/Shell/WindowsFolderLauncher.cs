using System.Diagnostics;
using GlyphFrame.Core.Interfaces;

namespace GlyphFrame.Infrastructure.Shell;

public sealed class WindowsFolderLauncher : IFolderLauncher
{
    public Task OpenFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        cancellationToken.ThrowIfCancellationRequested();

        string resolvedPath = Path.GetFullPath(folderPath);
        Directory.CreateDirectory(resolvedPath);

        Process.Start(new ProcessStartInfo
        {
            FileName = resolvedPath,
            UseShellExecute = true,
            Verb = "open"
        });

        return Task.CompletedTask;
    }
}
