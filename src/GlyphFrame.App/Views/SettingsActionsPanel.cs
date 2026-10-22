using System.Windows;
using System.Windows.Controls;

namespace GlyphFrame.App.Views;

/// <summary>Keeps Reset left and the dismissal/save pair right, wrapping only on measured demand.</summary>
public sealed class SettingsActionsPanel : Panel
{
    private const double GroupGap = 16;
    private const double ActionGap = 10;
    private const double RowGap = 12;

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        }
        return Layout(availableSize.Width, arrange: false);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Layout(finalSize.Width, arrange: true);
        return finalSize;
    }

    private Size Layout(double width, bool arrange)
    {
        if (InternalChildren.Count != 3) { return new Size(); }
        UIElement reset = InternalChildren[0], cancel = InternalChildren[1], save = InternalChildren[2];
        double pairWidth = cancel.DesiredSize.Width + ActionGap + save.DesiredSize.Width;
        double pairHeight = Math.Max(cancel.DesiredSize.Height, save.DesiredSize.Height);
        bool singleRow = reset.DesiredSize.Width + GroupGap + pairWidth <= width;
        bool pairFits = pairWidth <= width;
        double height = singleRow ? Math.Max(reset.DesiredSize.Height, pairHeight)
            : reset.DesiredSize.Height + RowGap + (pairFits ? pairHeight : cancel.DesiredSize.Height + RowGap + save.DesiredSize.Height);
        double arrangedWidth = double.IsPositiveInfinity(width) ? reset.DesiredSize.Width + GroupGap + pairWidth : width;
        if (arrange)
        {
            reset.Arrange(new Rect(0, singleRow ? (height - reset.DesiredSize.Height) / 2 : 0,
                reset.DesiredSize.Width, reset.DesiredSize.Height));
            double y = singleRow ? (height - pairHeight) / 2 : reset.DesiredSize.Height + RowGap;
            cancel.Arrange(new Rect(Math.Max(0, arrangedWidth - (pairFits ? pairWidth : cancel.DesiredSize.Width)),
                y + (pairFits ? (pairHeight - cancel.DesiredSize.Height) / 2 : 0), cancel.DesiredSize.Width, cancel.DesiredSize.Height));
            save.Arrange(new Rect(Math.Max(0, arrangedWidth - save.DesiredSize.Width),
                pairFits ? y + (pairHeight - save.DesiredSize.Height) / 2 : y + cancel.DesiredSize.Height + RowGap,
                save.DesiredSize.Width, save.DesiredSize.Height));
        }
        return new Size(Math.Min(arrangedWidth, reset.DesiredSize.Width + GroupGap + pairWidth), height);
    }
}
