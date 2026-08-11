using GlyphFrame.Core.Models;
using GlyphFrame.Infrastructure.Ffmpeg;

namespace GlyphFrame.Tests;

public sealed class BundledFfmpegIntegrationTests
{
    [Fact]
    public async Task BundledDistribution_ExecutesAndReportsExpectedVersion()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            ".."));
        using FfmpegValidationService validator = new(
            new BundledFfmpegLocator(repositoryRoot),
            new ProcessRunner());

        FfmpegValidationResult result = await validator.ValidateAsync();

        Assert.True(result.IsAvailable, result.TechnicalDetails);
        Assert.StartsWith("9.0-essentials_build", result.Ffmpeg.Version, StringComparison.Ordinal);
        Assert.StartsWith("9.0-essentials_build", result.Ffprobe.Version, StringComparison.Ordinal);
    }
}
