using System.Security.Cryptography;
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
            new BundledFfmpegLocator(Path.Combine(repositoryRoot, "artifacts", "local-runtime", "controlled-9.0-glyphframe.2")),
            new ProcessRunner());

        FfmpegValidationResult result = await validator.ValidateAsync();

        Assert.True(result.IsAvailable, result.TechnicalDetails);
        Assert.Equal("9.0-glyphframe.2", result.Ffmpeg.Version);
        Assert.Equal("9.0-glyphframe.2", result.Ffprobe.Version);
        string tools = Path.Combine(repositoryRoot, "artifacts", "local-runtime", "controlled-9.0-glyphframe.2", "Tools", "ffmpeg");
        Assert.Equal("a01e45417cfa050c11151a2bf609e983408b8fa688e9d6ab66cb223eba9e7dbe", Hash(Path.Combine(tools, "ffmpeg.exe")));
        Assert.Equal("e9fde8cdd23dd3534d15ff49b6788f80328f916634ae81bf7f8b9331da4ee2a9", Hash(Path.Combine(tools, "ffprobe.exe")));
    }

    private static string Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
