using System.IO;
using GlyphFrame.App.ViewModels;

namespace GlyphFrame.App.Tests;

public sealed class Phase11B1ViewContractTests
{
    [Fact]
    public void ImageRenderAction_IsBoundOnlyFromPreviewPanel()
    {
        string root = FindSolutionDirectory();
        string previewPanel = File.ReadAllText(Path.Combine(
            root,
            "src",
            "GlyphFrame.App",
            "Views",
            "PreviewPanelView.xaml"));
        string resultPanel = File.ReadAllText(Path.Combine(
            root,
            "src",
            "GlyphFrame.App",
            "Views",
            "ExportStatusView.xaml"));

        Assert.Equal(1, CountOccurrences(previewPanel, "RenderImageCommand"));
        Assert.DoesNotContain("RenderImageCommand", resultPanel, StringComparison.Ordinal);
        Assert.Contains("ShowImageRenderMetrics", resultPanel, StringComparison.Ordinal);
        Assert.Contains("ImageGridDimensionsText", resultPanel, StringComparison.Ordinal);
        Assert.Contains("ImageOutputDimensionsText", resultPanel, StringComparison.Ordinal);
        Assert.Null(typeof(MainWindowViewModel).GetProperty("ShowSecondaryRenderAgain"));
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int searchIndex = 0;
        while ((searchIndex = text.IndexOf(value, searchIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            searchIndex += value.Length;
        }

        return count;
    }

    private static string FindSolutionDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GlyphFrame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the GlyphFrame solution directory.");
    }
}
