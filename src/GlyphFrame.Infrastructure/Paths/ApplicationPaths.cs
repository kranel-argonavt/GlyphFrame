using GlyphFrame.Core.Interfaces;

namespace GlyphFrame.Infrastructure.Paths;

public sealed class ApplicationPaths : IApplicationPaths
{
    public ApplicationPaths()
        : this(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
    {
    }

    public ApplicationPaths(string localApplicationDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationDataRoot);

        ApplicationDataDirectory = Path.Combine(
            Path.GetFullPath(localApplicationDataRoot),
            "GlyphFrame");
        SettingsFilePath = Path.Combine(ApplicationDataDirectory, "settings.json");
        LogsDirectory = Path.Combine(ApplicationDataDirectory, "logs");
    }

    public string ApplicationDataDirectory { get; }

    public string SettingsFilePath { get; }

    public string LogsDirectory { get; }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(ApplicationDataDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
