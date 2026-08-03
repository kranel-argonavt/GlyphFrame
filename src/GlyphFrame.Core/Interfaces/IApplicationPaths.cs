namespace GlyphFrame.Core.Interfaces;

public interface IApplicationPaths
{
    string ApplicationDataDirectory { get; }

    string SettingsFilePath { get; }

    string LogsDirectory { get; }

    void EnsureDirectories();
}
