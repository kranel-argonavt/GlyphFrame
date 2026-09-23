using System.Windows;
using GlyphFrame.App.Views;

namespace GlyphFrame.App.Services;

public sealed class ErrorDetailsDialogService(IClipboardService clipboardService) : IErrorDetailsDialogService
{
    private readonly IClipboardService _clipboardService = clipboardService
        ?? throw new ArgumentNullException(nameof(clipboardService));

    public void Show(ErrorPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (string.IsNullOrWhiteSpace(presentation.TechnicalDetail))
        {
            return;
        }

        Application? application = Application.Current;
        Window? owner = ResolveOwner(
            application?.Windows.OfType<Window>() ?? [],
            application?.MainWindow,
            window => window.IsActive);
        ErrorDetailsWindow dialog = new(presentation, _clipboardService);
        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        dialog.ShowDialog();
    }

    internal static Window? ResolveOwner(
        IEnumerable<Window> windows,
        Window? mainWindow,
        Func<Window, bool> isActive) => windows.FirstOrDefault(isActive) ?? mainWindow;
}
