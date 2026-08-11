using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Infrastructure.Ffmpeg;

public sealed class BundledFfmpegLocator : IBundledFfmpegLocator
{
    private readonly string _applicationDirectory;

    public BundledFfmpegLocator()
        : this(AppContext.BaseDirectory)
    {
    }

    public BundledFfmpegLocator(string applicationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);
        _applicationDirectory = Path.GetFullPath(applicationDirectory);
    }

    public BundledFfmpegLocation Locate()
    {
        string toolsDirectory = Path.Combine(_applicationDirectory, "Tools", "ffmpeg");
        return new BundledFfmpegLocation(
            _applicationDirectory,
            toolsDirectory,
            Resolve(FfmpegToolKind.Ffmpeg, Path.Combine(toolsDirectory, "ffmpeg.exe")),
            Resolve(FfmpegToolKind.Ffprobe, Path.Combine(toolsDirectory, "ffprobe.exe")));
    }

    private static BundledToolPath Resolve(FfmpegToolKind tool, string path) =>
        new(tool, path, File.Exists(path), Directory.Exists(path));
}
