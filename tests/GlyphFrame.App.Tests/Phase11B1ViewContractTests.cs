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

    [Fact]
    public void VideoRenderAndPreviewActions_AreBoundOnlyFromPreviewPanel()
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

        Assert.Equal(1, CountOccurrences(previewPanel, "RenderVideoCommand"));
        Assert.Equal(1, CountOccurrences(previewPanel, "PreviewFrameCommand"));
        Assert.DoesNotContain("RenderVideoCommand", resultPanel, StringComparison.Ordinal);
        Assert.Contains("views:VideoReviewSurface", previewPanel, StringComparison.Ordinal);
        Assert.Contains("ShowVideoResultPanel", resultPanel, StringComparison.Ordinal);
        string player = File.ReadAllText(Path.Combine(root, "src", "GlyphFrame.App", "Views", "VideoReviewSurface.xaml"));
        Assert.Contains("RenderedVideoPlaybackSource", player, StringComparison.Ordinal);
        System.Xml.Linq.XDocument document = System.Xml.Linq.XDocument.Parse(player);
        Assert.Single(document.Descendants(), element =>
            element.Name.LocalName == "ComboBox"
            && element.Attribute("SelectedItem")?.Value == "{Binding SelectedFitMode}");
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
