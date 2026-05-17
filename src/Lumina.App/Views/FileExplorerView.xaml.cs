using System.Diagnostics;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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

    private void FileCard_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (GetFileFromSender(sender) is not { } file)
        {
            return;
        }

        ViewModel.SelectFile(file);
    }

    private async void FileCard_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (GetFileFromSender(sender) is not { } file)
        {
            return;
        }

        ViewModel.SelectFile(file);

        if (file.IsDirectory)
        {
            await ViewModel.OpenDirectoryAsync(file.Path);
            e.Handled = true;
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = file.Path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            await ShowOpenFileErrorDialogAsync(file.Name, ex.Message);
        }

        e.Handled = true;
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

    private void FileGridScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (IsWithinFileCard(e.OriginalSource))
        {
            return;
        }

        ViewModel.SelectFile(null);
    }

    private async Task ShowOpenFileErrorDialogAsync(string fileName, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Open file failed",
            Content = $"Could not open \"{fileName}\".\n\n{message}",
            CloseButtonText = "OK",
        };

        await dialog.ShowAsync();
    }

    private static FileExplorerItemViewModel? GetFileFromSender(object sender)
    {
        return sender is FrameworkElement { DataContext: FileExplorerItemViewModel file }
            ? file
            : null;
    }

    private static bool IsWithinFileCard(object? source)
    {
        var dependencyObject = source as DependencyObject;
        while (dependencyObject is not null)
        {
            if (dependencyObject is FrameworkElement { Name: "FileCard" })
            {
                return true;
            }

            dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
        }

        return false;
    }
}
