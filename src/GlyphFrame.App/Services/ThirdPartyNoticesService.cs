using System.IO;
using System.Security;
using System.Windows;
using GlyphFrame.App.Views;

namespace GlyphFrame.App.Services;

/// <summary>Opens the shipped document as inert, selectable local text, only on activation.</summary>
public sealed class ThirdPartyNoticesService
{
    private readonly string _path;
    private readonly Action<Window, string> _showDocument;

    public ThirdPartyNoticesService() : this(AppContext.BaseDirectory, (owner, text) =>
        new NoticesWindow(text) { Owner = owner }.ShowDialog())
    { }

    internal ThirdPartyNoticesService(string installationDirectory, Action<Window, string> showDocument)
    {
        _path = Path.Combine(installationDirectory, "THIRD_PARTY_NOTICES.md");
        _showDocument = showDocument;
    }

    public string? Show(Window owner)
    {
        string text;
        try { text = File.ReadAllText(_path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return "The third-party notices could not be read. Check that THIRD_PARTY_NOTICES.md is present and readable beside GlyphFrame, or extract the complete portable package again.";
        }
        _showDocument(owner, text);
        return null;
    }
}
