using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class ApplicationPathsTests
{
    [Fact]
    public void EnsureDirectories_CreatesOnlyTheExpectedGlyphFrameStructure()
    {
        using TemporaryApplicationData temporaryData = new();

        temporaryData.Paths.EnsureDirectories();

        Assert.Equal(
            Path.Combine(temporaryData.RootPath, "GlyphFrame"),
            temporaryData.Paths.ApplicationDataDirectory);
        Assert.Equal(
            Path.Combine(temporaryData.RootPath, "GlyphFrame", "settings.json"),
            temporaryData.Paths.SettingsFilePath);
        Assert.Equal(
            Path.Combine(temporaryData.RootPath, "GlyphFrame", "logs"),
            temporaryData.Paths.LogsDirectory);
        Assert.True(Directory.Exists(temporaryData.Paths.ApplicationDataDirectory));
        Assert.True(Directory.Exists(temporaryData.Paths.LogsDirectory));
    }
}
