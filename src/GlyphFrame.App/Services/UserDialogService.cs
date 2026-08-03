using System.Windows;
using GlyphFrame.App.Views;

namespace GlyphFrame.App.Services;

public sealed class UserDialogService : IUserDialogService
{
    public bool Confirm(
        string title,
        string message,
        string confirmButtonText = "Confirm",
        string cancelButtonText = "Cancel")
    {
        Window? owner = System.Windows.Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive)
            ?? System.Windows.Application.Current.MainWindow;

        ConfirmationWindow dialog = new(
            title,
            message,
            confirmButtonText,
            cancelButtonText);

        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        return dialog.ShowDialog() == true;
    }
}
