using Microsoft.UI.Xaml.Controls;

using Windows.Foundation;

namespace Lumina.App.Controls;

public sealed class WrapPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var maxWidth = double.IsInfinity(availableSize.Width)
            ? double.PositiveInfinity
            : Math.Max(0, availableSize.Width);

        var rowWidth = 0d;
        var rowHeight = 0d;
        var desiredWidth = 0d;
        var desiredHeight = 0d;

        foreach (var child in Children)
        {
            child.Measure(new Size(maxWidth, double.PositiveInfinity));
            var childSize = child.DesiredSize;

            if (rowWidth > 0 && rowWidth + childSize.Width > maxWidth)
            {
                desiredWidth = Math.Max(desiredWidth, rowWidth);
                desiredHeight += rowHeight;
                rowWidth = childSize.Width;
                rowHeight = childSize.Height;
            }
            else
            {
                rowWidth += childSize.Width;
                rowHeight = Math.Max(rowHeight, childSize.Height);
            }
        }

        desiredWidth = Math.Max(desiredWidth, rowWidth);
        desiredHeight += rowHeight;

        return new Size(
            double.IsInfinity(availableSize.Width) ? desiredWidth : availableSize.Width,
            desiredHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var maxWidth = double.IsInfinity(finalSize.Width)
            ? double.PositiveInfinity
            : Math.Max(0, finalSize.Width);

        var x = 0d;
        var y = 0d;
        var rowHeight = 0d;

        foreach (var child in Children)
        {
            var childSize = child.DesiredSize;
            if (x > 0 && x + childSize.Width > maxWidth)
            {
                x = 0;
                y += rowHeight;
                rowHeight = 0;
            }

            child.Arrange(new Rect(x, y, Math.Min(childSize.Width, maxWidth), childSize.Height));
            x += childSize.Width;
            rowHeight = Math.Max(rowHeight, childSize.Height);
        }

        return finalSize;
    }
}
