namespace GlyphFrame.Core.Settings;

public enum SettingsLoadState
{
    Loaded,
    DefaultsUsed,
    Normalized,
    RecoveredFromInvalidFile
}

public sealed record SettingsLoadResult(
    AppSettings Settings,
    SettingsLoadState State,
    string? UserMessage = null,
    string? PreservedInvalidFilePath = null);
