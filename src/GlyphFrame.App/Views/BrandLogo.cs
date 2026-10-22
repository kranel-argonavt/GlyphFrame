using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace GlyphFrame.App.Views;

/// <summary>Decorative host for the shared header/About vector artwork template.</summary>
public sealed class BrandLogo : Control
{
    protected override AutomationPeer? OnCreateAutomationPeer() => null;
}
