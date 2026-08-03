namespace GlyphFrame.App.Services;

public interface IUserDialogService
{
    bool Confirm(
        string title,
        string message,
        string confirmButtonText = "Confirm",
        string cancelButtonText = "Cancel");
}
