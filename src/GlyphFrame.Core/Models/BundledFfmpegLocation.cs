using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

public sealed record BundledToolPath(
    FfmpegToolKind Tool,
    string ResolvedPath,
    bool Exists,
    bool IsDirectory);

public sealed record BundledFfmpegLocation(
    string ApplicationDirectory,
    string ToolsDirectory,
    BundledToolPath Ffmpeg,
    BundledToolPath Ffprobe);
