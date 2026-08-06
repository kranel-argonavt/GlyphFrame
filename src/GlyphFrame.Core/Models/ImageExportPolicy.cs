using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

public static class ImageExportPolicy
{
    public static ImageExportDestinationRequirement Evaluate(
        string sourcePath,
        string outputPath,
        bool outputExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        try
        {
            string sourceFullPath = Path.GetFullPath(sourcePath);
            string outputFullPath = Path.GetFullPath(outputPath);
            if (sourceFullPath.Equals(outputFullPath, StringComparison.OrdinalIgnoreCase))
            {
                return ImageExportDestinationRequirement.RejectSourcePath;
            }

            return outputExists
                ? ImageExportDestinationRequirement.ConfirmOverwrite
                : ImageExportDestinationRequirement.Ready;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ImageExportDestinationRequirement.InvalidPath;
        }
    }
}
