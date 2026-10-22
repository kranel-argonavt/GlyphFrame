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
        Assert.Contains("PlaybackSource", player, StringComparison.Ordinal);
        string playerBindings = File.ReadAllText(Path.Combine(
            root,
            "src",
            "GlyphFrame.App",
            "Views",
            "VideoReviewSurface.xaml.cs"));
        Assert.Contains("RenderedVideoPlaybackSource", playerBindings, StringComparison.Ordinal);
        Assert.Contains("OriginalVideoPlaybackSource", playerBindings, StringComparison.Ordinal);
        System.Xml.Linq.XDocument document = System.Xml.Linq.XDocument.Parse(player);
        Assert.Single(document.Descendants(), element =>
            element.Name.LocalName == "ComboBox"
            && element.Attribute("SelectedItem")?.Value == "{Binding SelectedScaleMode, ElementName=Surface, Mode=TwoWay}");
    }

    [Fact]
    public void VideoPublication_ExistsOnlyInResultPanel_NotSettingsOrPreview()
    {
        string views = Path.Combine(FindSolutionDirectory(), "src", "GlyphFrame.App", "Views");
        System.Xml.Linq.XDocument result = System.Xml.Linq.XDocument.Load(Path.Combine(views, "ExportStatusView.xaml"));
        Assert.Single(result.Descendants(), element => element.Attribute("Command")?.Value == "{Binding ExportVideoCommand}");
        foreach (string name in new[] { "SettingsPanelView.xaml", "PreviewPanelView.xaml" })
        {
            System.Xml.Linq.XDocument document = System.Xml.Linq.XDocument.Load(Path.Combine(views, name));
            Assert.DoesNotContain(document.Descendants().SelectMany(element => element.Attributes()), attribute =>
                attribute.Value.Contains("ExportVideoCommand", StringComparison.Ordinal)
                || attribute.Value.Contains("BrowseOutputCommand", StringComparison.Ordinal)
                || attribute.Value.Contains("OutputPathText", StringComparison.Ordinal));
        }
        Assert.Null(typeof(MainWindowViewModel).GetProperty("ShowOutputCard"));
        Assert.Null(typeof(MainWindowViewModel).GetProperty("BrowseOutputCommand"));
    }

    [Fact]
    public void VideoResult_UsesAcceptedArtifactMetricsAndShrinkableTextLayout()
    {
        System.Xml.Linq.XDocument result = System.Xml.Linq.XDocument.Load(Path.Combine(
            FindSolutionDirectory(), "src", "GlyphFrame.App", "Views", "ExportStatusView.xaml"));
        System.Xml.Linq.XElement videoPanel = Assert.Single(result.Descendants(), element =>
            element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "VideoResultCard"));
        Assert.Contains(videoPanel.Descendants(), element => element.Name.LocalName == "Setter"
            && element.Attribute("Property")?.Value == "Visibility"
            && element.Attribute("Value")?.Value == "{Binding ShowVideoResultPanel, Converter={StaticResource BooleanToVisibilityConverter}}");
        Assert.Contains(videoPanel.Descendants(), element => element.Name.LocalName == "DataTrigger"
            && element.Attribute("Binding")?.Value == "{Binding IsExportStatusVisible}"
            && element.Elements().Any(setter => setter.Attribute("Property")?.Value == "Visibility" && setter.Attribute("Value")?.Value == "Collapsed"));
        Assert.Single(videoPanel.Descendants(), element => element.Attribute("Text")?.Value == "FILE SIZE");
        Assert.Single(videoPanel.Descendants(), element => element.Attribute("Text")?.Value == "OUTPUT");
        Assert.Single(videoPanel.Descendants(), element => element.Attribute("Text")?.Value == "AUDIO");
        Assert.Single(videoPanel.Descendants(), element => element.Attribute("Text")?.Value == "{Binding VideoFileSizeText}");
        Assert.Single(videoPanel.Descendants(), element => element.Attribute("Text")?.Value == "{Binding VideoOutputDimensionsText}");
        Assert.Single(videoPanel.Descendants(), element => element.Attribute("Text")?.Value == "{Binding VideoAudioOutcomeText}");
        System.Xml.Linq.XElement metrics = Assert.Single(videoPanel.Descendants(), element =>
            element.Attribute("Visibility")?.Value.Contains("ShowVideoRenderMetrics", StringComparison.Ordinal) == true);
        Assert.Equal(3, metrics.Elements().Count(element => element.Name.LocalName == "Border"));
        Assert.Equal("MetricCardsPanel", metrics.Name.LocalName);
        Assert.All(metrics.Descendants().Where(element => element.Attribute("Text")?.Value.StartsWith("{Binding Video", StringComparison.Ordinal) == true),
            value => { Assert.Equal("Wrap", value.Attribute("TextWrapping")?.Value); Assert.Equal("None", value.Attribute("TextTrimming")?.Value); });
        Assert.Equal("Metrics", metrics.Attributes().Single(attribute => attribute.Name.LocalName == "AdaptiveResultPanel.Role").Value);
        Assert.Contains(videoPanel.Descendants(), element =>
            element.Name.LocalName == "AdaptiveResultPanel"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "VideoResultLayout"));
        Assert.Single(metrics.DescendantsAndSelf().SelectMany(element => element.Attributes()), attribute =>
            attribute.Value == "{Binding VideoAudioOutcomeDescription}");
        Assert.Contains(videoPanel.Descendants(), element => element.Attribute("TextWrapping")?.Value == "Wrap"
            && element.Attribute("Text")?.Value == "{Binding VideoResultSummary}");
        Assert.Single(videoPanel.Descendants(), element =>
            element.Attribute("Command")?.Value == "{Binding ExportVideoCommand}");
        Assert.Single(videoPanel.Descendants(), element =>
            element.Attribute("Command")?.Value == "{Binding OpenExportFolderCommand}");
        foreach (string name in new[]
        {
            "VideoResultTitleText", "VideoResultSummaryText", "CopyVideoExportPathButton",
            "VideoFileSizeMetricCard", "VideoOutputMetricCard", "VideoAudioMetricCard"
        })
        {
            Assert.Single(videoPanel.Descendants(), element =>
                element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == name));
        }

        System.Xml.Linq.XElement imagePanel = Assert.Single(result.Descendants(), element =>
            element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "ImageResultCard"));
        Assert.Contains(imagePanel.Descendants(), element => element.Name.LocalName == "Setter"
            && element.Attribute("Property")?.Value == "Visibility"
            && element.Attribute("Value")?.Value == "{Binding ShowImageResultPanel, Converter={StaticResource BooleanToVisibilityConverter}}");
        Assert.Single(imagePanel.Descendants(), element => element.Attribute("Text")?.Value == "{Binding ImageGridDimensionsText}");
        Assert.Single(imagePanel.Descendants(), element => element.Attribute("Text")?.Value == "{Binding ImageOutputDimensionsText}");
        System.Xml.Linq.XElement fontWarning = Assert.Single(imagePanel.Descendants(), element =>
            element.Attribute("Text")?.Value == "{Binding ImageRenderWarningText}");
        Assert.Contains("ShowImageRenderWarning", fontWarning.Attribute("Visibility")?.Value, StringComparison.Ordinal);
        Assert.DoesNotContain(imagePanel.Descendants().SelectMany(element => element.Attributes()), attribute =>
            attribute.Value.Contains("VideoAudioOutcome", StringComparison.Ordinal));
        Assert.DoesNotContain(videoPanel.Descendants(), element => element.Attribute("Text")?.Value == "{Binding VideoExportDestinationText}");
        var copyStyle = Assert.Single(result.Descendants(), element =>
            element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == "CopyPathButtonStyle"));
        Assert.Contains(copyStyle.Elements(), element => element.Attribute("Property")?.Value == "Command"
            && element.Attribute("Value")?.Value == "{Binding ExportPathCopy.Command}");
        Assert.Contains(copyStyle.Elements(), element => element.Attribute("Property")?.Value == "ToolTip"
            && element.Attribute("Value")?.Value == "{Binding ExportPathCopy.ToolTip}");
    }

    [Fact]
    public void PreviewBusyOverlay_IsCompactAndDetailedProgressRemainsInResultPanel()
    {
        string views = Path.Combine(FindSolutionDirectory(), "src", "GlyphFrame.App", "Views");
        System.Xml.Linq.XDocument preview = System.Xml.Linq.XDocument.Load(Path.Combine(views, "PreviewPanelView.xaml"));
        System.Xml.Linq.XElement overlay = Assert.Single(preview.Descendants(), element =>
            element.Name.LocalName == "Border"
            && element.Attribute("Visibility")?.Value == "{Binding IsExportActive, Converter={StaticResource BooleanToVisibilityConverter}}");
        System.Xml.Linq.XElement indicator = Assert.Single(overlay.Descendants(), element =>
            element.Name.LocalName == "ExportActivityIndicator");
        Assert.Equal("False", indicator.Attribute("ShowPercentageText")?.Value);
        Assert.Null(indicator.Attribute("PercentageText"));
        Assert.Equal("False", indicator.Attribute("IsCompleted")?.Value);
        Assert.DoesNotContain(overlay.Descendants(), element => element.Name.LocalName is "TextBlock" or "ProgressBar" or "Button");
        Assert.DoesNotContain(preview.Descendants().SelectMany(element => element.Attributes()), attribute =>
            attribute.Value.Contains("IsExportStatusVisible", StringComparison.Ordinal)
            || attribute.Value.Contains("ExportOperationTitle", StringComparison.Ordinal)
            || attribute.Value.Contains("ExportProgressText", StringComparison.Ordinal)
            || attribute.Value.Contains("ExportFrameText", StringComparison.Ordinal)
            || attribute.Value.Contains("ElapsedText", StringComparison.Ordinal)
            || attribute.Value.Contains("RemainingText", StringComparison.Ordinal));

        System.Xml.Linq.XDocument result = System.Xml.Linq.XDocument.Load(Path.Combine(views, "ExportStatusView.xaml"));
        System.Xml.Linq.XElement progressPanel = Assert.Single(result.Descendants(), element =>
            element.Name.LocalName == "Border"
            && element.Attribute("Visibility")?.Value == "{Binding IsExportStatusVisible, Converter={StaticResource BooleanToVisibilityConverter}}");
        System.Xml.Linq.XElement detailedIndicator = Assert.Single(progressPanel.Descendants(), element =>
            element.Name.LocalName == "ExportActivityIndicator");
        Assert.Equal("True", detailedIndicator.Attribute("ShowPercentageText")?.Value);
        Assert.Equal("{Binding ExportPercentageText}", detailedIndicator.Attribute("PercentageText")?.Value);
        foreach (string binding in new[]
        {
            "ExportOperationTitle", "ExportProgressText", "ExportProgress", "ExportFrameText", "ElapsedText", "RemainingText"
        })
        {
            Assert.Contains(progressPanel.Descendants().SelectMany(element => element.Attributes()), attribute =>
                attribute.Value.Contains(binding, StringComparison.Ordinal));
        }
        Assert.Contains(progressPanel.Descendants(), element => element.Attribute("Command")?.Value == "{Binding CancelExportCommand}");
    }

    [Fact]
    public void PreviewFrameAction_ExplainsItsPreArtifactScopeAccessibly()
    {
        System.Xml.Linq.XDocument preview = System.Xml.Linq.XDocument.Load(Path.Combine(
            FindSolutionDirectory(), "src", "GlyphFrame.App", "Views", "PreviewPanelView.xaml"));
        System.Xml.Linq.XElement action = Assert.Single(preview.Descendants(), element =>
            element.Attribute("Command")?.Value == "{Binding PreviewFrameCommand}");
        Assert.Contains("currently selected", action.Attribute("AutomationProperties.Name")?.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("before a complete rendered video", action.Attribute("AutomationProperties.HelpText")?.Value,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OverwriteConfirmation_UsesOperationTitleRatherThanSettingsResetHeading()
    {
        System.Xml.Linq.XDocument document = System.Xml.Linq.XDocument.Load(Path.Combine(
            FindSolutionDirectory(), "src", "GlyphFrame.App", "Views", "ConfirmationWindow.xaml"));
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "TextBlock"
            && element.Attribute("Text")?.Value == "{Binding Title}"
            && element.Attribute("FontWeight")?.Value == "SemiBold");
        Assert.DoesNotContain(document.Descendants(), element => element.Attribute("Text")?.Value == "Reset preferences?");
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
