using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GlyphFrame.App.Services;

public static class ErrorDetailsReportFormatter
{
    public static string Format(ErrorPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (string.IsNullOrWhiteSpace(presentation.TechnicalDetail))
        {
            throw new ArgumentException("Technical details are required.", nameof(presentation));
        }

        StringBuilder report = new();
        report.AppendLine("GlyphFrame error details");
        report.Append("Code: ").AppendLine(presentation.Code.ToString());
        report.Append("Operation: ").AppendLine(presentation.Context.ToString());
        report.Append("Message: ").AppendLine(presentation.UserMessage);
        report.AppendLine();
        report.AppendLine("Technical details:");
        report.Append(presentation.TechnicalDetail);
        return report.ToString();
    }

    public static string OperationName(ErrorPresentationContext context) => context switch
    {
        ErrorPresentationContext.MediaInput => "Media input",
        ErrorPresentationContext.ImageRendering => "Image rendering",
        ErrorPresentationContext.ImageExport => "Image export",
        ErrorPresentationContext.VideoFramePreview => "Video frame preview",
        ErrorPresentationContext.FrameSymbolization => "Frame symbolization",
        ErrorPresentationContext.VideoRendering => "Video rendering",
        ErrorPresentationContext.VideoPublication => "Video export",
        ErrorPresentationContext.ManagedArtifact => "Managed review artifact",
        ErrorPresentationContext.OriginalPlayback => "Original video playback",
        ErrorPresentationContext.SymbolizedPlayback => "Symbolized video playback",
        ErrorPresentationContext.ToolValidation => "Bundled video-tool validation",
        _ => throw new ArgumentOutOfRangeException(nameof(context))
    };
}

public readonly record struct ClipboardWriteResult(bool Success, string? FailureMessage)
{
    public static ClipboardWriteResult Completed() => new(true, null);

    public static ClipboardWriteResult Failed() => new(
        false,
        "Could not access the clipboard. Close other clipboard tools and try again.");
}

public interface IClipboardService
{
    ClipboardWriteResult SetText(string text);
}

public sealed class WpfClipboardService : IClipboardService
{
    public ClipboardWriteResult SetText(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        try
        {
            Clipboard.SetText(text, TextDataFormat.UnicodeText);
            return ClipboardWriteResult.Completed();
        }
        catch (ExternalException)
        {
            return ClipboardWriteResult.Failed();
        }
        catch (ThreadStateException)
        {
            return ClipboardWriteResult.Failed();
        }
    }
}

public sealed class ErrorDetailsDialogViewModel : ObservableObject
{
    private readonly IClipboardService _clipboardService;
    private string? _copyStatusMessage;
    private bool _copySucceeded;

    public ErrorDetailsDialogViewModel(
        ErrorPresentation presentation,
        IClipboardService clipboardService)
    {
        Presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        Report = ErrorDetailsReportFormatter.Format(presentation);
        CopyDetailsCommand = new RelayCommand(CopyDetails);
    }

    public ErrorPresentation Presentation { get; }

    public string Code => Presentation.Code.ToString();

    public string Operation => ErrorDetailsReportFormatter.OperationName(Presentation.Context);

    public string Message => Presentation.UserMessage;

    public string TechnicalDetail => Presentation.TechnicalDetail!;

    public string Report { get; }

    public IRelayCommand CopyDetailsCommand { get; }

    public string? CopyStatusMessage
    {
        get => _copyStatusMessage;
        private set => SetProperty(ref _copyStatusMessage, value);
    }

    public bool CopySucceeded
    {
        get => _copySucceeded;
        private set => SetProperty(ref _copySucceeded, value);
    }

    private void CopyDetails()
    {
        ClipboardWriteResult result = _clipboardService.SetText(Report);
        CopySucceeded = result.Success;
        CopyStatusMessage = result.Success
            ? "Copied"
            : string.IsNullOrWhiteSpace(result.FailureMessage)
                ? "Could not access the clipboard. Close other clipboard tools and try again."
                : result.FailureMessage;
    }
}

public interface IErrorDetailsDialogService
{
    void Show(ErrorPresentation presentation);
}
