using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Windows.Foundation;

namespace Lumina.App.Controls;

public sealed class ExplorerGridPanel : Panel
{
    public static readonly DependencyProperty ItemWidthProperty =
        DependencyProperty.Register(
            nameof(ItemWidth),
            typeof(double),
            typeof(ExplorerGridPanel),
            new PropertyMetadata(176d, OnLayoutPropertyChanged));

    public static readonly DependencyProperty MinColumnSpacingProperty =
        DependencyProperty.Register(
            nameof(MinColumnSpacing),
            typeof(double),
            typeof(ExplorerGridPanel),
            new PropertyMetadata(12d, OnLayoutPropertyChanged));

    public static readonly DependencyProperty RowSpacingProperty =
        DependencyProperty.Register(
            nameof(RowSpacing),
            typeof(double),
            typeof(ExplorerGridPanel),
            new PropertyMetadata(18d, OnLayoutPropertyChanged));

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double MinColumnSpacing
    {
        get => (double)GetValue(MinColumnSpacingProperty);
        set => SetValue(MinColumnSpacingProperty, value);
    }

    public double RowSpacing
    {
        get => (double)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = ResolveWidth(availableSize.Width);
        var columns = CalculateColumnCount(width);
        var rowHeight = MeasureChildren();
        var rowCount = CalculateRowCount(columns);
        var desiredHeight = rowCount == 0
            ? 0
            : rowCount * rowHeight + (rowCount - 1) * Math.Max(0, RowSpacing);

        return new Size(width, desiredHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = ResolveWidth(finalSize.Width);
        var columns = CalculateColumnCount(width);
        var cellWidth = columns == 0 ? width : width / columns;
        var rowHeight = GetMeasuredRowHeight();
        var rowSpacing = Math.Max(0, RowSpacing);

        for (var index = 0; index < Children.Count; index++)
        {
            var child = Children[index];
            var column = index % columns;
            var row = index / columns;
            var childWidth = Math.Min(ItemWidth, child.DesiredSize.Width);
            var childHeight = child.DesiredSize.Height;
            var x = column * cellWidth + Math.Max(0, (cellWidth - childWidth) / 2);
            var y = row * (rowHeight + rowSpacing);

            child.Arrange(new Rect(x, y, childWidth, childHeight));
        }

        return finalSize;
    }

    private int CalculateColumnCount(double width)
    {
        if (Children.Count == 0)
        {
            return 0;
        }

        var itemWidth = Math.Max(1, ItemWidth);
        var slotWidth = itemWidth + Math.Max(0, MinColumnSpacing);

        return Math.Max(1, (int)Math.Floor(width / slotWidth));
    }

    private int CalculateRowCount(int columns)
    {
        return columns <= 0
            ? 0
            : (int)Math.Ceiling((double)Children.Count / columns);
    }

    private double MeasureChildren()
    {
        var rowHeight = 0d;
        var itemWidth = Math.Max(1, ItemWidth);

        foreach (var child in Children)
        {
            child.Measure(new Size(itemWidth, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
        }

        return rowHeight;
    }

    private double GetMeasuredRowHeight()
    {
        var rowHeight = 0d;

        foreach (var child in Children)
        {
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
        }

        return rowHeight;
    }

    private double ResolveWidth(double width)
    {
        return double.IsInfinity(width) || double.IsNaN(width) || width <= 0
            ? Math.Max(1, ItemWidth)
            : Math.Max(0, width);
    }

    private static void OnLayoutPropertyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ExplorerGridPanel panel)
        {
            return;
        }

        panel.InvalidateMeasure();
        panel.InvalidateArrange();
    }
}
