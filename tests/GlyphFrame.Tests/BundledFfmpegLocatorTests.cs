using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;
using GlyphFrame.Infrastructure.Ffmpeg;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class BundledFfmpegLocatorTests
{
    [Fact]
    public void Locate_ResolvesExpectedBundledPaths()
    {
        using TemporaryMediaDirectory directory = new();
        string appDirectory = Path.Combine(directory.RootPath, "app");
        Directory.CreateDirectory(Path.Combine(appDirectory, "Tools", "ffmpeg"));
        File.WriteAllBytes(Path.Combine(appDirectory, "Tools", "ffmpeg", "ffmpeg.exe"), [1]);
        File.WriteAllBytes(Path.Combine(appDirectory, "Tools", "ffmpeg", "ffprobe.exe"), [2]);

        BundledFfmpegLocation location = new BundledFfmpegLocator(appDirectory).Locate();

        Assert.Equal(Path.Combine(appDirectory, "Tools", "ffmpeg"), location.ToolsDirectory);
        Assert.Equal(Path.Combine(location.ToolsDirectory, "ffmpeg.exe"), location.Ffmpeg.ResolvedPath);
        Assert.Equal(Path.Combine(location.ToolsDirectory, "ffprobe.exe"), location.Ffprobe.ResolvedPath);
        Assert.True(location.Ffmpeg.Exists);
        Assert.True(location.Ffprobe.Exists);
    }

    [Fact]
    public void Locate_DoesNotDependOnCurrentWorkingDirectory()
    {
        using TemporaryMediaDirectory application = new();
        using TemporaryMediaDirectory working = new();
        string original = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = working.RootPath;
            BundledFfmpegLocation location = new BundledFfmpegLocator(application.RootPath).Locate();

            Assert.StartsWith(application.RootPath, location.ToolsDirectory, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(working.RootPath, location.ToolsDirectory, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.CurrentDirectory = original;
        }
    }

    [Theory]
    [InlineData("application with spaces")]
    [InlineData("应用程序-Глиф")]
    public void Locate_PreservesApplicationDirectoryCharacters(string childDirectory)
    {
        using TemporaryMediaDirectory directory = new();
        string appDirectory = Path.Combine(directory.RootPath, childDirectory);
        Directory.CreateDirectory(appDirectory);

        BundledFfmpegLocation location = new BundledFfmpegLocator(appDirectory).Locate();

        Assert.Contains(childDirectory, location.Ffmpeg.ResolvedPath, StringComparison.Ordinal);
        Assert.Contains(childDirectory, location.Ffprobe.ResolvedPath, StringComparison.Ordinal);
    }

    [Fact]
    public void Locate_ReportsMissingToolsDirectoryWithoutThrowing()
    {
        using TemporaryMediaDirectory directory = new();

        BundledFfmpegLocation location = new BundledFfmpegLocator(directory.RootPath).Locate();

        Assert.False(Directory.Exists(location.ToolsDirectory));
        Assert.False(location.Ffmpeg.Exists);
        Assert.False(location.Ffprobe.Exists);
    }

    [Fact]
    public void Locate_ReportsEachToolIndependently()
    {
        using TemporaryMediaDirectory directory = new();
        string tools = Path.Combine(directory.RootPath, "Tools", "ffmpeg");
        Directory.CreateDirectory(tools);
        File.WriteAllBytes(Path.Combine(tools, "ffmpeg.exe"), [1]);

        BundledFfmpegLocation location = new BundledFfmpegLocator(directory.RootPath).Locate();

        Assert.Equal(FfmpegToolKind.Ffmpeg, location.Ffmpeg.Tool);
        Assert.True(location.Ffmpeg.Exists);
        Assert.False(location.Ffprobe.Exists);
    }
}
