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
    private const double FileGridMinColumnSpacing = 16;
    private const double FileGridRowSpacing = 18;

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
        FileGridScrollViewer.Focus(FocusState.Pointer);
    }

    private async void FileCard_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (GetFileFromSender(sender) is not { } file)
        {
            return;
        }

        ViewModel.SelectFile(file);
        await OpenFileAsync(file);
        e.Handled = true;
    }

    private void FileGridScrollViewer_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Left:
                MoveSelectionBy(-1);
                e.Handled = true;
                break;
            case VirtualKey.Right:
                MoveSelectionBy(1);
                e.Handled = true;
                break;
            case VirtualKey.Up:
                MoveSelectionBy(-GetFileGridColumnCount());
                e.Handled = true;
                break;
            case VirtualKey.Down:
                MoveSelectionBy(GetFileGridColumnCount());
                e.Handled = true;
                break;
            case VirtualKey.Home:
                SelectFileAt(0);
                e.Handled = true;
                break;
            case VirtualKey.End:
                SelectFileAt(ViewModel.Files.Count - 1);
                e.Handled = true;
                break;
            case VirtualKey.Enter:
                OpenSelectedFile();
                e.Handled = true;
                break;
            case VirtualKey.Escape:
                ViewModel.SelectFile(null);
                e.Handled = true;
                break;
            case VirtualKey.F5:
                RefreshCurrentFolder();
                e.Handled = true;
                break;
        }
    }

    private async Task OpenFileAsync(FileExplorerItemViewModel file)
    {
        if (file.IsDirectory)
        {
            await ViewModel.OpenDirectoryAsync(file.Path);
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
        EnsureSelectedFileVisible();
        e.Handled = true;
    }

    private void FileGridScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        FileGridScrollViewer.Focus(FocusState.Pointer);

        if (IsWithinFileCard(e.OriginalSource))
        {
            return;
        }

        ViewModel.SelectFile(null);
    }

    private void MoveSelectionBy(int offset)
    {
        if (ViewModel.Files.Count == 0)
        {
            return;
        }

        if (ViewModel.SelectedFile is null)
        {
            SelectFileAt(offset < 0 ? ViewModel.Files.Count - 1 : 0);
            return;
        }

        var currentIndex = ViewModel.Files.IndexOf(ViewModel.SelectedFile);
        if (currentIndex < 0)
        {
            SelectFileAt(0);
            return;
        }

        SelectFileAt(Math.Clamp(currentIndex + offset, 0, ViewModel.Files.Count - 1));
    }

    private void SelectFileAt(int index)
    {
        if (ViewModel.Files.Count == 0)
        {
            return;
        }

        var clampedIndex = Math.Clamp(index, 0, ViewModel.Files.Count - 1);
        ViewModel.SelectFile(ViewModel.Files[clampedIndex]);
        EnsureSelectedFileVisible();
    }

    private async void OpenSelectedFile()
    {
        if (ViewModel.SelectedFile is null)
        {
            return;
        }

        await OpenFileAsync(ViewModel.SelectedFile);
    }

    private async void RefreshCurrentFolder()
    {
        await ViewModel.RefreshAsync();
    }

    private int GetFileGridColumnCount()
    {
        if (ViewModel.Files.Count == 0)
        {
            return 1;
        }

        var width = Math.Max(0, FileGridScrollViewer.ActualWidth);
        var slotWidth = ViewModel.CardWidth + FileGridMinColumnSpacing;

        return Math.Max(1, (int)Math.Floor(width / slotWidth));
    }

    private void EnsureSelectedFileVisible()
    {
        if (ViewModel.SelectedFile is null)
        {
            return;
        }

        var selectedIndex = ViewModel.Files.IndexOf(ViewModel.SelectedFile);
        if (selectedIndex < 0)
        {
            return;
        }

        var columns = GetFileGridColumnCount();
        var row = selectedIndex / columns;
        var itemTop = row * (ViewModel.CardHeight + FileGridRowSpacing);
        var itemBottom = itemTop + ViewModel.CardHeight;
        var viewportTop = FileGridScrollViewer.VerticalOffset;
        var viewportBottom = viewportTop + FileGridScrollViewer.ViewportHeight;

        if (itemTop < viewportTop)
        {
            FileGridScrollViewer.ChangeView(null, itemTop, null, true);
            return;
        }

        if (itemBottom > viewportBottom)
        {
            var targetOffset = Math.Max(0, itemBottom - FileGridScrollViewer.ViewportHeight);
            FileGridScrollViewer.ChangeView(null, targetOffset, null, true);
        }
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
