using System.Text.Json;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;
using GlyphFrame.Core.Settings;
using GlyphFrame.Rendering.Skia;

namespace GlyphFrame.Tests;

public sealed class ImageExportPolicyTests
{
    [Fact]
    public void ExistingOutput_RequiresOverwriteConfirmation()
    {
        ImageExportDestinationRequirement requirement = ImageExportPolicy.Evaluate(
            Path.Combine(Path.GetTempPath(), "source.png"),
            Path.Combine(Path.GetTempPath(), "output.png"),
            outputExists: true);

        Assert.Equal(ImageExportDestinationRequirement.ConfirmOverwrite, requirement);
    }

    [Fact]
    public void ExportToSourcePath_IsRejectedCaseInsensitively()
    {
        string source = Path.Combine(Path.GetTempPath(), "Input.PNG");

        ImageExportDestinationRequirement requirement = ImageExportPolicy.Evaluate(
            source,
            source.ToLowerInvariant(),
            outputExists: true);

        Assert.Equal(ImageExportDestinationRequirement.RejectSourcePath, requirement);
    }

    [Fact]
    public void NewDistinctOutput_IsReadyWithoutConfirmation()
    {
        ImageExportDestinationRequirement requirement = ImageExportPolicy.Evaluate(
            Path.Combine(Path.GetTempPath(), "source.png"),
            Path.Combine(Path.GetTempPath(), "new-output.png"),
            outputExists: false);

        Assert.Equal(ImageExportDestinationRequirement.Ready, requirement);
    }

    [Fact]
    public void InvalidOutputPath_IsRejectedBeforeExport()
    {
        ImageExportDestinationRequirement requirement = ImageExportPolicy.Evaluate(
            Path.Combine(Path.GetTempPath(), "source.png"),
            "invalid\0output.png",
            outputExists: false);

        Assert.Equal(ImageExportDestinationRequirement.InvalidPath, requirement);
    }

    [Fact]
    public void ApprovedDefaults_UsePngAndCentralizedJpegQuality()
    {
        AppSettings defaults = new();

        Assert.Equal(ImageExportFormat.Png, defaults.DefaultImageExportFormat);
        Assert.Equal(90, SkiaImageExporter.JpegQuality);
    }

    [Fact]
    public void ApplicationSettings_DoNotPersistTransientMediaOrOutputPaths()
    {
        string json = JsonSerializer.Serialize(new AppSettings());

        Assert.DoesNotContain("MediaPath", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InputPath", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OutputPath", json, StringComparison.OrdinalIgnoreCase);
    }
}
