using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Xml.Linq;
using GlyphFrame.App.Services;

namespace GlyphFrame.App.Tests;

public sealed class ErrorDetailsTests
{
    [Fact]
    public void ReportFormatter_IsDeterministicAndIncludesEveryContractField()
    {
        ErrorPresentation presentation = ErrorPresentation.Create(
            ApplicationErrorCode.VideoEncodeFailed,
            ErrorPresentationContext.VideoRendering,
            "ffmpeg stderr\r\nHRESULT=0x80004005");

        string first = ErrorDetailsReportFormatter.Format(presentation);
        string second = ErrorDetailsReportFormatter.Format(presentation);

        Assert.Equal(first, second);
        Assert.Equal(
            "GlyphFrame error details" + Environment.NewLine
            + "Code: VideoEncodeFailed" + Environment.NewLine
            + "Operation: VideoRendering" + Environment.NewLine
            + "Message: GlyphFrame could not encode the rendered video. Check the bundled video tools and try again."
            + Environment.NewLine + Environment.NewLine
            + "Technical details:" + Environment.NewLine
            + "ffmpeg stderr\r\nHRESULT=0x80004005",
            first);
    }

    [Fact]
    public void ReportFormatter_RejectsPresentationWithoutTechnicalDetails()
    {
        ErrorPresentation presentation = ErrorPresentation.Create(
            ApplicationErrorCode.InvalidVideo,
            ErrorPresentationContext.MediaInput);

        Assert.Throws<ArgumentException>(() => ErrorDetailsReportFormatter.Format(presentation));
    }

    [Theory]
    [InlineData(ErrorPresentationContext.MediaInput, "Media input")]
    [InlineData(ErrorPresentationContext.ImageRendering, "Image rendering")]
    [InlineData(ErrorPresentationContext.ImageExport, "Image export")]
    [InlineData(ErrorPresentationContext.VideoFramePreview, "Video frame preview")]
    [InlineData(ErrorPresentationContext.FrameSymbolization, "Frame symbolization")]
    [InlineData(ErrorPresentationContext.VideoRendering, "Video rendering")]
    [InlineData(ErrorPresentationContext.VideoPublication, "Video export")]
    [InlineData(ErrorPresentationContext.ManagedArtifact, "Managed review artifact")]
    [InlineData(ErrorPresentationContext.OriginalPlayback, "Original video playback")]
    [InlineData(ErrorPresentationContext.SymbolizedPlayback, "Symbolized video playback")]
    [InlineData(ErrorPresentationContext.ToolValidation, "Bundled video-tool validation")]
    public void OperationName_CoversEveryErrorContext(
        ErrorPresentationContext context,
        string expected) => Assert.Equal(expected, ErrorDetailsReportFormatter.OperationName(context));

    [Fact]
    public void CopyDetails_UsesExactReportAndReportsSuccessOnlyAfterClipboardSuccess()
    {
        FakeClipboard clipboard = new(ClipboardWriteResult.Completed());
        ErrorDetailsDialogViewModel viewModel = CreateViewModel(clipboard);

        viewModel.CopyDetailsCommand.Execute(null);

        Assert.Equal(viewModel.Report, clipboard.LastText);
        Assert.True(viewModel.CopySucceeded);
        Assert.Equal("Copied", viewModel.CopyStatusMessage);
    }

    [Fact]
    public void ClipboardFailure_LeavesDetailsAvailableAndDoesNotReportSuccess()
    {
        FakeClipboard clipboard = new(ClipboardWriteResult.Failed());
        ErrorDetailsDialogViewModel viewModel = CreateViewModel(clipboard);

        viewModel.CopyDetailsCommand.Execute(null);

        Assert.Equal(viewModel.Report, clipboard.LastText);
        Assert.False(viewModel.CopySucceeded);
        Assert.Contains("clipboard", viewModel.CopyStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("native detail", viewModel.TechnicalDetail);
    }

    [Fact]
    public void ClipboardFailureWithoutProviderMessage_UsesSafeRecoveryGuidance()
    {
        FakeClipboard clipboard = new(new ClipboardWriteResult(false, null));
        ErrorDetailsDialogViewModel viewModel = CreateViewModel(clipboard);

        viewModel.CopyDetailsCommand.Execute(null);

        Assert.False(viewModel.CopySucceeded);
        Assert.Contains("clipboard", viewModel.CopyStatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OwnerResolution_PrefersActiveWindowAndFallsBackToMainWindow()
    {
        RunSta(() =>
        {
            Window main = new();
            Window active = new();
            Window other = new();
            Window? selected = ErrorDetailsDialogService.ResolveOwner(
                [main, other, active],
                main,
                window => ReferenceEquals(window, active));
            Window? fallback = ErrorDetailsDialogService.ResolveOwner(
                [other],
                main,
                _ => false);

            Assert.Same(active, selected);
            Assert.Same(main, fallback);
        });
    }

    [Fact]
    public void DialogContract_UsesOneScrollableMonospaceDetailsSurfaceAndExplicitActions()
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../src/GlyphFrame.App/Views/ErrorDetailsWindow.xaml"));
        XDocument document = XDocument.Load(path);
        XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        IEnumerable<XElement> textBoxes = document.Descendants(wpf + "TextBox");
        XElement details = Assert.Single(textBoxes);

        Assert.Equal("True", details.Attribute("IsReadOnly")?.Value);
        Assert.Equal("Wrap", details.Attribute("TextWrapping")?.Value);
        Assert.Equal("Auto", details.Attribute("VerticalScrollBarVisibility")?.Value);
        Assert.Contains("Mono", details.Attribute("FontFamily")?.Value, StringComparison.Ordinal);
        string textBinding = Assert.IsType<string>(details.Attribute("Text")?.Value);
        Assert.Contains("TechnicalDetail", textBinding, StringComparison.Ordinal);
        Assert.Contains("Mode=OneWay", textBinding.Replace(" ", string.Empty), StringComparison.Ordinal);
        Assert.Single(document.Descendants(wpf + "Button"), button =>
            button.Attribute("Content")?.Value == "Copy Details");
        Assert.Single(document.Descendants(wpf + "Button"), button =>
            button.Attribute("Content")?.Value == "Close");
    }

    [Fact]
    public void ErrorLocations_UseContextCommandsAndSettingsHasNoRawDiagnosticsExpander()
    {
        XDocument preview = LoadView("PreviewPanelView.xaml");
        XDocument result = LoadView("ExportStatusView.xaml");
        XDocument settings = LoadView("SettingsWindow.xaml");
        XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        AssertCommandBinding(preview, "ShowMediaInputErrorDetailsCommand");
        AssertCommandBinding(preview, "ShowVideoFrameErrorDetailsCommand");
        AssertCommandBinding(preview, "ShowOriginalPlaybackErrorDetailsCommand");
        AssertCommandBinding(preview, "ShowVideoReviewErrorDetailsCommand");
        AssertCommandBinding(result, "ShowImageResultErrorDetailsCommand");
        AssertCommandBinding(result, "ShowVideoResultErrorDetailsCommand");
        AssertCommandBinding(settings, "ShowFfmpegErrorDetailsCommand");
        Assert.DoesNotContain(settings.Descendants(wpf + "Expander"), element =>
            element.Attribute("Header")?.Value == "Technical details");
    }

    private static XDocument LoadView(string fileName)
    {
        string path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            $"../../../../../src/GlyphFrame.App/Views/{fileName}"));
        return XDocument.Load(path);
    }

    private static void AssertCommandBinding(XDocument document, string commandName)
    {
        XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        Assert.Single(document.Descendants(wpf + "Button"), button =>
            button.Attribute("Command")?.Value.Contains(commandName, StringComparison.Ordinal) == true);
    }

    private static ErrorDetailsDialogViewModel CreateViewModel(IClipboardService clipboard) => new(
        ErrorPresentation.Create(
            ApplicationErrorCode.SymbolizedPlaybackUnavailable,
            ErrorPresentationContext.SymbolizedPlayback,
            "native detail"),
        clipboard);

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private sealed class FakeClipboard(ClipboardWriteResult result) : IClipboardService
    {
        public string? LastText { get; private set; }

        public ClipboardWriteResult SetText(string text)
        {
            LastText = text;
            return result;
        }
    }
}
