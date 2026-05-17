using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

using Lumina.App.Services;
using Lumina.App.ViewModels;

namespace Lumina.App.Views;

public sealed partial class FileExplorerView : UserControl
{
    public FileExplorerView()
    {
        ViewModel = new FileExplorerViewModel();
        InitializeComponent();
        DataContext = ViewModel;

        Loaded += FileExplorerView_Loaded;
        Unloaded += FileExplorerView_Unloaded;
    }

    public FileExplorerViewModel ViewModel { get; }

    private async void FileExplorerView_Loaded(object sender, RoutedEventArgs e)
    {
        LocationSelectionEvents.SelectionChanged += LocationSelectionEvents_SelectionChanged;
        await ViewModel.OpenLocationAsync(LocationSelectionEvents.CurrentLocation);
    }

    private void FileExplorerView_Unloaded(object sender, RoutedEventArgs e)
    {
        LocationSelectionEvents.SelectionChanged -= LocationSelectionEvents_SelectionChanged;
    }

    private async void LocationSelectionEvents_SelectionChanged(
        object? sender,
        LocationSelectionChangedEventArgs e)
    {
        await ViewModel.OpenLocationAsync(e.Location);
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync();
    }

    private void FileGridScrollViewer_PointerWheelChanged(
        object sender,
        PointerRoutedEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            return;
        }

        var wheelDelta = e.GetCurrentPoint((UIElement)sender).Properties.MouseWheelDelta;
        if (wheelDelta == 0)
        {
            return;
        }

        ViewModel.ZoomByWheelDelta(wheelDelta);
        e.Handled = true;
    }
}
