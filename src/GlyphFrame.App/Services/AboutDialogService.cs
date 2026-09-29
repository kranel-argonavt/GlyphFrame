using System.Reflection;
using System.Windows;
using GlyphFrame.App.Views;
using GlyphFrame.Core.Models;

namespace GlyphFrame.App.Services;

public interface IAboutDialogService
{
    string VersionText { get; }

    void Show(ApplicationInfo applicationInfo);
}

public sealed class AboutDialogService : IAboutDialogService
{
    public AboutDialogService() => VersionText = FormatVersion(typeof(App).Assembly);

    public string VersionText { get; }

    public void Show(ApplicationInfo applicationInfo)
    {
        ArgumentNullException.ThrowIfNull(applicationInfo);
        Application? application = Application.Current;
        Window? owner = ResolveOwner(
            application?.Windows.OfType<Window>() ?? [],
            application?.MainWindow,
            window => window.IsActive);
        AboutWindow dialog = new(applicationInfo.Name, VersionText);
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

    internal static string FormatVersion(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        Version? version = assembly.GetName().Version;
        return version is null
            ? "Version unavailable"
            : version.Build < 0
                ? $"v{version.Major}.{version.Minor}"
                : $"v{version.Major}.{version.Minor}.{version.Build}";
    }
}
