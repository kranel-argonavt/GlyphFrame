using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;

namespace GlyphFrame.App.Views;

/// <summary>The adjacent product text supplies the name; the brand drawing is not a second reader stop.</summary>
public sealed class DecorativeImage : Image
{
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        // A shared DrawingImage can acquire multiple inheritance contexts (for
        // example Window.Icon and artwork), leaving its resource expressions
        // resolved to an old accent. Each view owns its vector drawing context.
        // Clone, not CloneCurrentValue, retains the DynamicResource expressions.
        if (Source is DrawingImage drawing) { Source = drawing.Clone(); }
    }

    protected override AutomationPeer? OnCreateAutomationPeer() => null;
}
