using System.Diagnostics;
using System.Runtime.InteropServices;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;

using Lumina.App.Services;
using Lumina.App.ViewModels;
using Lumina.Core.Models;

namespace Lumina.App.Views;

public sealed partial class FileExplorerView : UserControl
{
    private const double FileGridMinColumnSpacing = 16;
    private const double FileGridRowSpacing = 18;
    private const int KeyDownStateMask = 0x8000;

    private enum SelectionNavigationMode
    {
        Select,
        Extend,
        Focus,
    }

    private enum FileClipboardOperation
    {
        Copy,
        Cut,
    }

    private sealed record FileClipboardState(
        IReadOnlyList<string> Paths,
        FileClipboardOperation Operation);

    private FileClipboardState? _fileClipboard;

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
        LocationSelectionEvents.SelectionChanged -= LocationSelectionEvents_SelectionChanged;
        LocationSelectionEvents.SelectionChanged += LocationSelectionEvents_SelectionChanged;
        Clipboard.ContentChanged -= Clipboard_ContentChanged;
        Clipboard.ContentChanged += Clipboard_ContentChanged;
        await ViewModel.OpenLocationAsync(LocationSelectionEvents.CurrentLocation);
    }

    private void FileExplorerView_Unloaded(object sender, RoutedEventArgs e)
    {
        LocationSelectionEvents.SelectionChanged -= LocationSelectionEvents_SelectionChanged;
        Clipboard.ContentChanged -= Clipboard_ContentChanged;
    }

    private void Clipboard_ContentChanged(object? sender, object e)
    {
        try
        {
            if (!Clipboard.GetContent().Contains(StandardDataFormats.StorageItems))
            {
                _fileClipboard = null;
            }
        }
        catch (Exception)
        {
            _fileClipboard = null;
        }
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

        if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift))
        {
            ViewModel.ExtendSelectionTo(file);
        }
        else if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            ViewModel.ToggleFileSelection(file);
        }
        else
        {
            ViewModel.SelectFile(file);
        }

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
        var isAltDown = e.KeyStatus.IsMenuKeyDown || IsKeyDown(VirtualKey.Menu);
        var isControlDown = IsKeyDown(VirtualKey.Control);
        var isShiftDown = IsKeyDown(VirtualKey.Shift);
        var selectionMode = ResolveSelectionNavigationMode(isShiftDown, isControlDown);

        if (isAltDown)
        {
            switch (e.Key)
            {
                case VirtualKey.Up:
                    NavigateToParentDirectory();
                    e.Handled = true;
                    return;
                case VirtualKey.Left:
                    NavigateBack();
                    e.Handled = true;
                    return;
                case VirtualKey.Right:
                    NavigateForward();
                    e.Handled = true;
                    return;
            }
        }

        switch (e.Key)
        {
            case VirtualKey.A:
                if (isControlDown)
                {
                    ViewModel.SelectAllFiles();
                    e.Handled = true;
                }

                break;
            case VirtualKey.C:
                if (isControlDown)
                {
                    CopySelectedFiles();
                    e.Handled = true;
                }

                break;
            case VirtualKey.X:
                if (isControlDown)
                {
                    CutSelectedFiles();
                    e.Handled = true;
                }

                break;
            case VirtualKey.V:
                if (isControlDown)
                {
                    PasteFiles();
                    e.Handled = true;
                }

                break;
            case VirtualKey.F:
            case VirtualKey.E:
                if (isControlDown)
                {
                    FocusSearchBox();
                    e.Handled = true;
                }

                break;
            case VirtualKey.Left:
                MoveSelectionBy(-1, selectionMode);
                e.Handled = true;
                break;
            case VirtualKey.Right:
                MoveSelectionBy(1, selectionMode);
                e.Handled = true;
                break;
            case VirtualKey.Up:
                MoveSelectionBy(-GetFileGridColumnCount(), selectionMode);
                e.Handled = true;
                break;
            case VirtualKey.Down:
                MoveSelectionBy(GetFileGridColumnCount(), selectionMode);
                e.Handled = true;
                break;
            case VirtualKey.Home:
                SelectFileAt(0, selectionMode);
                e.Handled = true;
                break;
            case VirtualKey.End:
                SelectFileAt(ViewModel.Files.Count - 1, selectionMode);
                e.Handled = true;
                break;
            case VirtualKey.Enter:
                OpenSelectedFile();
                e.Handled = true;
                break;
            case VirtualKey.F2:
                RenameFocusedFile();
                e.Handled = true;
                break;
            case VirtualKey.Delete:
                DeleteSelectedFiles(isShiftDown);
                e.Handled = true;
                break;
            case VirtualKey.Space:
                if (isControlDown)
                {
                    ToggleFocusedFileSelection();
                    e.Handled = true;
                }

                break;
            case VirtualKey.Escape:
                ViewModel.SelectFile(null);
                e.Handled = true;
                break;
            case VirtualKey.F5:
                RefreshCurrentFolder();
                e.Handled = true;
                break;
            case VirtualKey.Back:
                if (!isControlDown && !isShiftDown)
                {
                    NavigateBack();
                    e.Handled = true;
                }

                break;
        }
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.SearchQuery = SearchBox.Text;
        if (SearchBox.FocusState == FocusState.Unfocused)
        {
            return;
        }

        await ViewModel.SearchAsync();
        ScrollToTop();
    }

    private async Task OpenFileAsync(FileExplorerItemViewModel file)
    {
        if (file.IsDirectory)
        {
            await ViewModel.OpenDirectoryAsync(file.Path);
            ScrollToTop();
            FileGridScrollViewer.Focus(FocusState.Programmatic);
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

    private void MoveSelectionBy(int offset, SelectionNavigationMode mode)
    {
        if (ViewModel.Files.Count == 0)
        {
            return;
        }

        if (ViewModel.SelectedFile is null)
        {
            SelectFileAt(offset < 0 ? ViewModel.Files.Count - 1 : 0, mode);
            return;
        }

        var currentIndex = ViewModel.Files.IndexOf(ViewModel.SelectedFile);
        if (currentIndex < 0)
        {
            SelectFileAt(0, mode);
            return;
        }

        SelectFileAt(
            Math.Clamp(currentIndex + offset, 0, ViewModel.Files.Count - 1),
            mode);
    }

    private void SelectFileAt(int index, SelectionNavigationMode mode)
    {
        if (ViewModel.Files.Count == 0)
        {
            return;
        }

        var clampedIndex = Math.Clamp(index, 0, ViewModel.Files.Count - 1);
        var file = ViewModel.Files[clampedIndex];

        switch (mode)
        {
            case SelectionNavigationMode.Extend:
                ViewModel.ExtendSelectionTo(file);
                break;
            case SelectionNavigationMode.Focus:
                ViewModel.FocusFile(file);
                break;
            default:
                ViewModel.SelectFile(file);
                break;
        }

        EnsureSelectedFileVisible();
    }

    private void FocusSearchBox()
    {
        if (!ViewModel.CanSearch)
        {
            return;
        }

        SearchBox.Focus(FocusState.Programmatic);
        SearchBox.SelectAll();
    }

    private async void RenameFocusedFile()
    {
        if (ViewModel.SelectedFile is not { } file)
        {
            return;
        }

        var newName = await PromptForFileNameAsync(file);
        if (string.IsNullOrWhiteSpace(newName) ||
            string.Equals(file.FileSystemName, newName, StringComparison.Ordinal))
        {
            FileGridScrollViewer.Focus(FocusState.Programmatic);
            return;
        }

        await RunFileOperationAsync(
            () => ViewModel.RenameFileAsync(file, newName),
            "Rename failed");
        FileGridScrollViewer.Focus(FocusState.Programmatic);
    }

    private async void DeleteSelectedFiles(bool permanently)
    {
        var files = ViewModel.GetSelectedFilesOrFocusedFile();
        if (files.Count == 0)
        {
            return;
        }

        if (permanently && !await ConfirmPermanentDeleteAsync(files))
        {
            FileGridScrollViewer.Focus(FocusState.Programmatic);
            return;
        }

        await RunFileOperationAsync(
            () => ViewModel.DeleteFilesAsync(
                files,
                permanently
                    ? FileDeleteBehavior.Permanent
                    : FileDeleteBehavior.RecycleBin),
            "Delete failed");
        FileGridScrollViewer.Focus(FocusState.Programmatic);
    }

    private async void CopySelectedFiles()
    {
        await SetFileClipboardAsync(FileClipboardOperation.Copy);
    }

    private async void CutSelectedFiles()
    {
        await SetFileClipboardAsync(FileClipboardOperation.Cut);
    }

    private async void PasteFiles()
    {
        if (string.IsNullOrWhiteSpace(ViewModel.CurrentPath))
        {
            return;
        }

        var clipboard = await GetFileClipboardAsync();
        if (clipboard is null || clipboard.Paths.Count == 0)
        {
            return;
        }

        await RunFileOperationAsync(
            async () =>
            {
                if (clipboard.Operation == FileClipboardOperation.Cut)
                {
                    await ViewModel.MoveFilesIntoCurrentDirectoryAsync(clipboard.Paths);
                    ClearCutClipboard(clipboard);
                    return;
                }

                await ViewModel.CopyFilesIntoCurrentDirectoryAsync(clipboard.Paths);
            },
            "Paste failed");
        FileGridScrollViewer.Focus(FocusState.Programmatic);
    }

    private void ToggleFocusedFileSelection()
    {
        if (ViewModel.SelectedFile is null)
        {
            if (ViewModel.Files.Count > 0)
            {
                ViewModel.ToggleFileSelection(ViewModel.Files[0]);
                EnsureSelectedFileVisible();
            }

            return;
        }

        ViewModel.ToggleFileSelection(ViewModel.SelectedFile);
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

    private async void NavigateToParentDirectory()
    {
        if (!ViewModel.CanNavigateToParent)
        {
            return;
        }

        await ViewModel.NavigateToParentAsync();
        ScrollToTop();
        FileGridScrollViewer.Focus(FocusState.Programmatic);
    }

    private async void NavigateBack()
    {
        if (!ViewModel.CanNavigateBack)
        {
            return;
        }

        await ViewModel.NavigateBackAsync();
        ScrollToTop();
        FileGridScrollViewer.Focus(FocusState.Programmatic);
    }

    private async void NavigateForward()
    {
        if (!ViewModel.CanNavigateForward)
        {
            return;
        }

        await ViewModel.NavigateForwardAsync();
        ScrollToTop();
        FileGridScrollViewer.Focus(FocusState.Programmatic);
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

    private void ScrollToTop()
    {
        FileGridScrollViewer.ChangeView(null, 0, null, true);
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

    private async Task<string?> PromptForFileNameAsync(FileExplorerItemViewModel file)
    {
        var nameBox = new TextBox
        {
            Header = "Name",
            PlaceholderText = "Enter a file or folder name",
            Text = file.FileSystemName,
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Rename",
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = nameBox,
        };

        void UpdatePrimaryButtonState()
        {
            dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(nameBox.Text);
        }

        nameBox.TextChanged += (_, _) => UpdatePrimaryButtonState();
        dialog.Opened += (_, _) =>
        {
            UpdatePrimaryButtonState();
            nameBox.Focus(FocusState.Programmatic);

            var selectionLength = GetRenameSelectionLength(file);
            nameBox.Select(0, selectionLength);
        };

        var result = await dialog.ShowAsync();

        return result == ContentDialogResult.Primary ? nameBox.Text.Trim() : null;
    }

    private async Task<bool> ConfirmPermanentDeleteAsync(
        IReadOnlyList<FileExplorerItemViewModel> files)
    {
        var count = files.Count;
        var title = count == 1
            ? "Permanently delete item?"
            : $"Permanently delete {count} items?";
        var content = count == 1
            ? $"\"{files[0].Name}\" will be deleted permanently."
            : "The selected items will be deleted permanently.";

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        var result = await dialog.ShowAsync();

        return result == ContentDialogResult.Primary;
    }

    private async Task RunFileOperationAsync(
        Func<Task> operation,
        string errorTitle)
    {
        try
        {
            await operation();
            EnsureSelectedFileVisible();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await ShowFileOperationErrorDialogAsync(errorTitle, ex.Message);
        }
    }

    private async Task ShowFileOperationErrorDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "OK",
        };

        await dialog.ShowAsync();
    }

    private async Task SetFileClipboardAsync(FileClipboardOperation operation)
    {
        var files = ViewModel.GetSelectedFilesOrFocusedFile();
        if (files.Count == 0)
        {
            return;
        }

        var paths = files.Select(file => file.Path).ToList();
        _fileClipboard = new FileClipboardState(paths, operation);
        await TrySetSystemClipboardAsync(paths, operation);
    }

    private async Task<FileClipboardState?> GetFileClipboardAsync()
    {
        return await TryGetSystemClipboardAsync() ?? _fileClipboard;
    }

    private async Task TrySetSystemClipboardAsync(
        IReadOnlyList<string> paths,
        FileClipboardOperation operation)
    {
        try
        {
            var storageItems = new List<IStorageItem>(paths.Count);
            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    storageItems.Add(await StorageFolder.GetFolderFromPathAsync(path));
                    continue;
                }

                if (File.Exists(path))
                {
                    storageItems.Add(await StorageFile.GetFileFromPathAsync(path));
                }
            }

            if (storageItems.Count != paths.Count)
            {
                return;
            }

            var dataPackage = new DataPackage
            {
                RequestedOperation = operation == FileClipboardOperation.Cut
                    ? DataPackageOperation.Move
                    : DataPackageOperation.Copy,
            };
            dataPackage.SetStorageItems(storageItems);
            Clipboard.SetContent(dataPackage);
        }
        catch (Exception)
        {
        }
    }

    private static async Task<FileClipboardState?> TryGetSystemClipboardAsync()
    {
        try
        {
            var dataPackageView = Clipboard.GetContent();
            if (!dataPackageView.Contains(StandardDataFormats.StorageItems))
            {
                return null;
            }

            var storageItems = await dataPackageView.GetStorageItemsAsync();
            var paths = storageItems
                .Select(item => item.Path)
                .Where(path => !string.IsNullOrWhiteSpace(path) && PathExists(path))
                .ToList();
            if (paths.Count == 0)
            {
                return null;
            }

            var operation = dataPackageView.RequestedOperation == DataPackageOperation.Move
                ? FileClipboardOperation.Cut
                : FileClipboardOperation.Copy;

            return new FileClipboardState(paths, operation);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void ClearCutClipboard(FileClipboardState clipboard)
    {
        if (_fileClipboard is not null &&
            _fileClipboard.Operation == FileClipboardOperation.Cut &&
            _fileClipboard.Paths.SequenceEqual(clipboard.Paths, StringComparer.OrdinalIgnoreCase))
        {
            _fileClipboard = null;
        }

        try
        {
            Clipboard.Clear();
        }
        catch (Exception)
        {
        }
    }

    private static FileExplorerItemViewModel? GetFileFromSender(object sender)
    {
        return sender is FrameworkElement { DataContext: FileExplorerItemViewModel file }
            ? file
            : null;
    }

    private static SelectionNavigationMode ResolveSelectionNavigationMode(
        bool isShiftDown,
        bool isControlDown)
    {
        if (isShiftDown)
        {
            return SelectionNavigationMode.Extend;
        }

        return isControlDown
            ? SelectionNavigationMode.Focus
            : SelectionNavigationMode.Select;
    }

    private static bool IsKeyDown(VirtualKey key)
    {
        return (GetKeyState((int)key) & KeyDownStateMask) != 0;
    }

    private static int GetRenameSelectionLength(FileExplorerItemViewModel file)
    {
        if (file.IsDirectory)
        {
            return file.FileSystemName.Length;
        }

        var extensionLength = Path.GetExtension(file.FileSystemName).Length;
        var selectionLength = file.FileSystemName.Length - extensionLength;

        return selectionLength > 0 ? selectionLength : file.FileSystemName.Length;
    }

    private static bool PathExists(string path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);

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
