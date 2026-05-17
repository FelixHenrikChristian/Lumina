using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
}
