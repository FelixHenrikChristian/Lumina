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
    private static readonly TimeSpan SearchDebounceDelay = TimeSpan.FromMilliseconds(400);

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
    private CancellationTokenSource? _searchDebounceCancellation;
    private TextBox? _searchTextBox;
    private FileExplorerItemViewModel? _renamingFile;
    private bool _isInlineRenameCommitInProgress;
    private bool _isSearchTextComposing;

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
        AttachSearchTextBoxEvents();
        await ViewModel.OpenLocationAsync(LocationSelectionEvents.CurrentLocation);
    }

    private void FileExplorerView_Unloaded(object sender, RoutedEventArgs e)
    {
        LocationSelectionEvents.SelectionChanged -= LocationSelectionEvents_SelectionChanged;
        Clipboard.ContentChanged -= Clipboard_ContentChanged;
        CancelPendingSearch();
        CancelInlineRename();
        DetachSearchTextBoxEvents();
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
        CancelPendingSearch();
        CancelInlineRename();
        _isSearchTextComposing = false;
        await ViewModel.OpenLocationAsync(e.Location);
        ScrollToTop();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync();
    }

    private async void AddressBreadcrumbBar_ItemClicked(
        BreadcrumbBar sender,
        BreadcrumbBarItemClickedEventArgs args)
    {
        var items = ViewModel.BreadcrumbItems;
        if (args.Index < 0 || args.Index >= items.Count - 1)
        {
            return;
        }

        var targetPath = items[args.Index].Path;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return;
        }

        await ViewModel.OpenDirectoryAsync(targetPath);
        ScrollToTop();
        FileGridScrollViewer.Focus(FocusState.Programmatic);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateBack();
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateForward();
    }

    private void UpButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToParentDirectory();
    }

    private void CutButton_Click(object sender, RoutedEventArgs e)
    {
        CutSelectedFiles();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        CopySelectedFiles();
    }

    private void PasteButton_Click(object sender, RoutedEventArgs e)
    {
        PasteFiles();
    }

    private void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        RenameFocusedFile();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelectedFiles(permanently: false);
    }

    private void FileCard_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (IsWithinInlineRenameBox(e.OriginalSource))
        {
            return;
        }

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
        if (IsWithinInlineRenameBox(e.OriginalSource))
        {
            e.Handled = true;
            return;
        }

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
        if (IsWithinInlineRenameBox(e.OriginalSource))
        {
            return;
        }

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
            case VirtualKey.D:
                if (isControlDown)
                {
                    DeleteSelectedFiles(permanently: false);
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

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        ViewModel.SearchQuery = sender.Text;
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            CancelPendingSearch();
            return;
        }

        if (_isSearchTextComposing)
        {
            CancelPendingSearch();
            return;
        }

        ScheduleSearch();
    }

    private async void SearchBox_QuerySubmitted(
        AutoSuggestBox sender,
        AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        CancelPendingSearch();
        _isSearchTextComposing = false;
        ViewModel.SearchQuery = sender.Text;
        await ViewModel.SearchAsync();
        ScrollToTop();
    }

    private void SearchTextBox_TextCompositionStarted(
        TextBox sender,
        TextCompositionStartedEventArgs args)
    {
        _isSearchTextComposing = true;
        CancelPendingSearch();
    }

    private void SearchTextBox_TextCompositionEnded(
        TextBox sender,
        TextCompositionEndedEventArgs args)
    {
        _isSearchTextComposing = false;
        ViewModel.SearchQuery = SearchBox.Text;
        ScheduleSearch();
    }

    private void ScheduleSearch()
    {
        CancelPendingSearch();

        var searchCancellation = new CancellationTokenSource();
        _searchDebounceCancellation = searchCancellation;
        _ = RunSearchAfterDelayAsync(searchCancellation);
    }

    private void CancelPendingSearch()
    {
        _searchDebounceCancellation?.Cancel();
        _searchDebounceCancellation = null;
    }

    private async Task RunSearchAfterDelayAsync(CancellationTokenSource searchCancellation)
    {
        try
        {
            await Task.Delay(SearchDebounceDelay, searchCancellation.Token);
            await ViewModel.SearchAsync(searchCancellation.Token);
            searchCancellation.Token.ThrowIfCancellationRequested();
            ScrollToTop();
        }
        catch (OperationCanceledException) when (searchCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (_searchDebounceCancellation == searchCancellation)
            {
                _searchDebounceCancellation = null;
            }

            searchCancellation.Dispose();
        }
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
        if (IsWithinInlineRenameBox(e.OriginalSource))
        {
            return;
        }

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
        AttachSearchTextBoxEvents();
        _searchTextBox?.SelectAll();
    }

    private void RenameFocusedFile()
    {
        if (ViewModel.SelectedFile is not { } file)
        {
            return;
        }

        BeginInlineRename(file);
    }

    private void BeginInlineRename(FileExplorerItemViewModel file)
    {
        if (_isInlineRenameCommitInProgress)
        {
            return;
        }

        CancelInlineRename();
        ViewModel.SelectFile(file);
        _renamingFile = file;
        file.BeginRename();
        FocusInlineRenameBox(file);
    }

    private void CancelInlineRename()
    {
        if (_renamingFile is not { } file)
        {
            return;
        }

        _renamingFile = null;
        file.CancelRename();
    }

    private void EndInlineRename(FileExplorerItemViewModel file)
    {
        if (ReferenceEquals(_renamingFile, file))
        {
            _renamingFile = null;
        }

        file.EndRename();
    }

    private void InlineRenameBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (textBox.DataContext is FileExplorerItemViewModel file &&
            ReferenceEquals(_renamingFile, file))
        {
            FocusInlineRenameBox(file);
        }
    }

    private async void InlineRenameBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: FileExplorerItemViewModel file } textBox ||
            !ReferenceEquals(_renamingFile, file))
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Enter:
                e.Handled = true;
                await CommitInlineRenameAsync(file, textBox.Text, restoreGridFocus: true);
                break;
            case VirtualKey.Escape:
                e.Handled = true;
                CancelInlineRename();
                FileGridScrollViewer.Focus(FocusState.Programmatic);
                break;
        }
    }

    private async void InlineRenameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isInlineRenameCommitInProgress ||
            sender is not TextBox { DataContext: FileExplorerItemViewModel file } textBox ||
            !ReferenceEquals(_renamingFile, file))
        {
            return;
        }

        await CommitInlineRenameAsync(file, textBox.Text, restoreGridFocus: false);
    }

    private async Task CommitInlineRenameAsync(
        FileExplorerItemViewModel file,
        string requestedName,
        bool restoreGridFocus)
    {
        if (_isInlineRenameCommitInProgress || !ReferenceEquals(_renamingFile, file))
        {
            return;
        }

        _isInlineRenameCommitInProgress = true;
        var newDisplayName = requestedName.Trim();

        try
        {
            if (newDisplayName.Length == 0)
            {
                await ShowFileOperationErrorDialogAsync(
                    "Rename failed",
                    "The name cannot be empty.");
                FocusInlineRenameBox(file);
                return;
            }

            var newFileSystemName = file.BuildFileSystemNameFromDisplayName(newDisplayName);
            if (string.Equals(file.FileSystemName, newFileSystemName, StringComparison.Ordinal))
            {
                EndInlineRename(file);
                if (restoreGridFocus)
                {
                    FileGridScrollViewer.Focus(FocusState.Programmatic);
                }

                return;
            }

            await ViewModel.RenameFileAsync(file, newFileSystemName);
            EndInlineRename(file);
            if (restoreGridFocus)
            {
                FileGridScrollViewer.Focus(FocusState.Programmatic);
            }
        }
        catch (OperationCanceledException)
        {
            EndInlineRename(file);
        }
        catch (Exception ex)
        {
            await ShowFileOperationErrorDialogAsync("Rename failed", ex.Message);
            if (ReferenceEquals(_renamingFile, file))
            {
                FocusInlineRenameBox(file);
            }
        }
        finally
        {
            _isInlineRenameCommitInProgress = false;
        }
    }

    private void FocusInlineRenameBox(FileExplorerItemViewModel file)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var textBox = FindInlineRenameTextBox(file);
            if (textBox is null)
            {
                return;
            }

            textBox.Focus(FocusState.Programmatic);
            textBox.Select(0, GetRenameSelectionLength(file));
        });
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

    private TextBox? FindInlineRenameTextBox(FileExplorerItemViewModel file)
    {
        return FindDescendant<TextBox>(
            FilesItemsControl,
            textBox => textBox.Name == "InlineRenameBox" &&
                ReferenceEquals(textBox.DataContext, file));
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
        var renameText = file.RenameText;
        if (file.IsDirectory)
        {
            return renameText.Length;
        }

        var extensionLength = Path.GetExtension(renameText).Length;
        var selectionLength = renameText.Length - extensionLength;

        return selectionLength > 0 ? selectionLength : renameText.Length;
    }

    private static bool PathExists(string path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }

    private void AttachSearchTextBoxEvents()
    {
        SearchBox.ApplyTemplate();
        var textBox = FindDescendant<TextBox>(SearchBox);
        if (ReferenceEquals(_searchTextBox, textBox))
        {
            return;
        }

        DetachSearchTextBoxEvents();
        _searchTextBox = textBox;

        if (_searchTextBox is null)
        {
            return;
        }

        _searchTextBox.TextCompositionStarted += SearchTextBox_TextCompositionStarted;
        _searchTextBox.TextCompositionEnded += SearchTextBox_TextCompositionEnded;
    }

    private void DetachSearchTextBoxEvents()
    {
        if (_searchTextBox is null)
        {
            return;
        }

        _searchTextBox.TextCompositionStarted -= SearchTextBox_TextCompositionStarted;
        _searchTextBox.TextCompositionEnded -= SearchTextBox_TextCompositionEnded;
        _searchTextBox = null;
        _isSearchTextComposing = false;
    }

    private static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        return FindDescendant<T>(parent, _ => true);
    }

    private static T? FindDescendant<T>(
        DependencyObject parent,
        Func<T, bool> predicate)
        where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match && predicate(match))
            {
                return match;
            }

            var descendant = FindDescendant(child, predicate);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);

    private static bool IsWithinInlineRenameBox(object? source)
    {
        var dependencyObject = source as DependencyObject;
        while (dependencyObject is not null)
        {
            if (dependencyObject is TextBox { Name: "InlineRenameBox" })
            {
                return true;
            }

            dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
        }

        return false;
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
