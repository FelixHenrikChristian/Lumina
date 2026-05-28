using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.System;

using Lumina.App.Services;
using Lumina.App.ViewModels;
using Lumina.Core.Models;
using Lumina.Core.Services;

namespace Lumina.App.Views;

public sealed partial class FileExplorerView : UserControl
{
    private const double FileGridMinColumnSpacing = 16;
    private const double FileGridRowSpacing = 18;
    private const int KeyDownStateMask = 0x8000;
    private const uint InvalidFileSize = 0xFFFFFFFF;
    private const long DefaultAllocationUnitSize = 4096;
    private const string LuminaFilePathsFormat = "Lumina.FileExplorer.Paths";
    private static readonly TimeSpan ProgressDialogShowDelay = TimeSpan.FromMilliseconds(300);
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

    private sealed record FileTransferOperationResult(
        bool Succeeded,
        bool Cancelled,
        string? ErrorMessage,
        FileOperationResult? OperationResult);

    private sealed record FileConflictDialogResult(
        FileConflictAction Action,
        bool ApplyToAll);

    private sealed record FilePropertiesDialogInfo(
        string Name,
        string IconGlyph,
        string Type,
        string Location,
        long Size,
        long SizeOnDisk,
        DateTimeOffset Created,
        DateTimeOffset Modified);

    private sealed class FileOperationProgressDialogState : ObservableObject
    {
        private string _statusText = "Preparing...";
        private string _currentItemText = string.Empty;
        private string _detailText = string.Empty;
        private double _progressValue;
        private bool _isIndeterminate = true;

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public string CurrentItemText
        {
            get => _currentItemText;
            private set => SetProperty(ref _currentItemText, value);
        }

        public string DetailText
        {
            get => _detailText;
            private set => SetProperty(ref _detailText, value);
        }

        public double ProgressValue
        {
            get => _progressValue;
            private set => SetProperty(ref _progressValue, value);
        }

        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            private set => SetProperty(ref _isIndeterminate, value);
        }

        public void Apply(FileOperationProgress progress)
        {
            ProgressValue = progress.PercentComplete;
            IsIndeterminate = progress.Stage == FileOperationProgressStage.Preparing ||
                (progress.TotalBytes <= 0 && progress.TotalItems <= 0);

            StatusText = progress.Stage switch
            {
                FileOperationProgressStage.Preparing => $"Preparing to {FormatOperationVerb(progress.Operation)}...",
                FileOperationProgressStage.Completed => $"{FormatOperationName(progress.Operation)} complete",
                _ => $"{FormatOperationGerund(progress.Operation)}...",
            };

            CurrentItemText = string.IsNullOrWhiteSpace(progress.CurrentItemName)
                ? string.Empty
                : progress.CurrentItemName;

            DetailText = FormatProgressDetail(progress);
        }

        public void MarkCancelling()
        {
            StatusText = "Cancelling...";
            IsIndeterminate = true;
        }

        public void MarkCancelled()
        {
            StatusText = "Cancelled";
            IsIndeterminate = false;
        }

        private static string FormatProgressDetail(FileOperationProgress progress)
        {
            if (progress.TotalBytes > 0)
            {
                return $"{FormatCompactByteCount(progress.CompletedBytes)} of {FormatCompactByteCount(progress.TotalBytes)}";
            }

            if (progress.TotalItems > 0)
            {
                return $"{Math.Min(progress.CompletedItems, progress.TotalItems)} of {progress.TotalItems} items";
            }

            return string.Empty;
        }

        private static string FormatOperationVerb(FileOperationKind operation)
        {
            return operation switch
            {
                FileOperationKind.Create => "create",
                FileOperationKind.Rename => "rename",
                FileOperationKind.Delete => "delete",
                FileOperationKind.Move => "move",
                _ => "copy",
            };
        }

        private static string FormatOperationName(FileOperationKind operation)
        {
            return operation switch
            {
                FileOperationKind.Create => "Create",
                FileOperationKind.Rename => "Rename",
                FileOperationKind.Delete => "Delete",
                FileOperationKind.Move => "Move",
                _ => "Copy",
            };
        }

        private static string FormatOperationGerund(FileOperationKind operation)
        {
            return operation switch
            {
                FileOperationKind.Create => "Creating",
                FileOperationKind.Rename => "Renaming",
                FileOperationKind.Delete => "Deleting",
                FileOperationKind.Move => "Moving",
                _ => "Copying",
            };
        }
    }

    private sealed class PlannedFileOperationConflictResolver : IFileOperationConflictResolver
    {
        private readonly Dictionary<string, FileConflictAction> _decisions = new(StringComparer.OrdinalIgnoreCase);

        public bool HasDecisions => _decisions.Count > 0;

        public void SetDecision(string destinationPath, FileConflictAction action)
        {
            _decisions[FileExplorerView.NormalizeClipboardPath(destinationPath)] = action;
        }

        public Task<FileConflictAction> ResolveAsync(
            FileConflictInfo conflict,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _decisions.TryGetValue(
                    FileExplorerView.NormalizeClipboardPath(conflict.DestinationPath),
                    out var action)
                    ? action
                    : FileConflictAction.KeepBoth);
        }
    }

    private FileClipboardState? _fileClipboard;
    private FileExplorerItemViewModel? _dropTargetFile;
    private FileExplorerItemViewModel? _pendingSingleSelectionFile;
    private FileExplorerItemViewModel? _pendingToggleSelectionFile;
    private IReadOnlyList<string>? _draggedPaths;
    private readonly List<FileOperationResult> _undoStack = [];
    private readonly List<FileOperationResult> _redoStack = [];
    private CancellationTokenSource? _searchDebounceCancellation;
    private TextBox? _searchTextBox;
    private FileExplorerItemViewModel? _renamingFile;
    private bool _isInlineRenameCommitInProgress;
    private bool _isSearchTextComposing;

    public FileExplorerView()
    {
        ViewModel = new FileExplorerViewModel();
        ViewModel.Files.CollectionChanged += Files_CollectionChanged;
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
        await RefreshClipboardVisualStateAsync();
        UpdateHistoryCommandState();
    }

    private void FileExplorerView_Unloaded(object sender, RoutedEventArgs e)
    {
        LocationSelectionEvents.SelectionChanged -= LocationSelectionEvents_SelectionChanged;
        Clipboard.ContentChanged -= Clipboard_ContentChanged;
        CancelPendingSearch();
        CancelInlineRename();
        DetachSearchTextBoxEvents();
    }

    private async void Clipboard_ContentChanged(object? sender, object e)
    {
        await RefreshClipboardVisualStateAsync();
    }

    private void Files_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is null)
        {
            return;
        }

        var cutPaths = CreateCutPathSet(_fileClipboard);
        foreach (var item in e.NewItems)
        {
            if (item is FileExplorerItemViewModel file)
            {
                ApplyFileCutState(file, cutPaths);
            }
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
        _undoStack.Clear();
        _redoStack.Clear();
        UpdateHistoryCommandState();
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

    private async void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        await UndoLastFileOperationAsync();
    }

    private async void RedoButton_Click(object sender, RoutedEventArgs e)
    {
        await RedoLastFileOperationAsync();
    }

    private async void NewFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await CreateFolderAsync();
    }

    private void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        RenameFocusedFile();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelectedFiles(permanently: false);
    }

    private void OpenFileContextButton_Click(object sender, RoutedEventArgs e)
    {
        OpenSelectedFile();
    }

    private async void ShowInFileExplorerContextButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowSelectedFileInFileExplorerAsync();
    }

    private async void PropertiesContextButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowSelectedFilePropertiesAsync();
    }

    private void SortButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement anchor || !ViewModel.CanSort)
        {
            return;
        }

        CreateSortMenuFlyout().ShowAt(anchor);
    }

    private async void RelativePathButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetFileFromSender(sender) is not { } file)
        {
            return;
        }

        CancelPendingSearch();
        CancelInlineRename();
        _isSearchTextComposing = false;

        await ViewModel.OpenContainingDirectoryAsync(file);
        EnsureSelectedFileVisible();
        FileGridScrollViewer.Focus(FocusState.Programmatic);
    }

    private async void SortFieldMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: FileSortField sortField } ||
            !ViewModel.CanSort)
        {
            return;
        }

        CancelInlineRename();
        await ViewModel.SortByAsync(sortField);
        ScrollToTop();
        FileGridScrollViewer.Focus(FocusState.Programmatic);
    }

    private async void SortDirectionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: FileSortDirection sortDirection } ||
            !ViewModel.CanSort)
        {
            return;
        }

        CancelInlineRename();
        await ViewModel.SortDirectionAsync(sortDirection);
        ScrollToTop();
        FileGridScrollViewer.Focus(FocusState.Programmatic);
    }

    private void FileCard_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (IsRightButtonPressed(sender, e))
        {
            return;
        }

        if (IsWithinInlineRenameBox(e.OriginalSource))
        {
            return;
        }

        if (IsWithinRelativePathButton(e.OriginalSource))
        {
            return;
        }

        if (GetFileFromSender(sender) is not { } file)
        {
            return;
        }

        ClearPendingFileCardSelection();

        if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift))
        {
            ViewModel.ExtendSelectionTo(file);
        }
        else if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            if (file.IsSelected && ViewModel.GetSelectedFilesOrFocusedFile().Count > 1)
            {
                _pendingToggleSelectionFile = file;
                ViewModel.FocusFile(file);
            }
            else
            {
                ViewModel.ToggleFileSelection(file);
            }
        }
        else if (file.IsSelected && ViewModel.GetSelectedFilesOrFocusedFile().Count > 1)
        {
            _pendingSingleSelectionFile = file;
            ViewModel.FocusFile(file);
        }
        else
        {
            ViewModel.SelectFile(file);
        }

        FileGridScrollViewer.Focus(FocusState.Pointer);
    }

    private void FileCard_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (GetFileFromSender(sender) is not { } file ||
            (!ReferenceEquals(_pendingSingleSelectionFile, file) &&
                !ReferenceEquals(_pendingToggleSelectionFile, file)))
        {
            ClearPendingFileCardSelection();
            return;
        }

        var shouldSelectSingle = ReferenceEquals(_pendingSingleSelectionFile, file);
        var shouldToggleSelection = ReferenceEquals(_pendingToggleSelectionFile, file);
        ClearPendingFileCardSelection();

        if (shouldSelectSingle)
        {
            ViewModel.SelectFile(file);
        }
        else if (shouldToggleSelection)
        {
            ViewModel.ToggleFileSelection(file);
        }

        FileGridScrollViewer.Focus(FocusState.Pointer);
    }

    private void FileCard_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        ClearPendingFileCardSelection();
    }

    private async void FileCard_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (IsWithinInlineRenameBox(e.OriginalSource) ||
            IsWithinRelativePathButton(e.OriginalSource))
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

    private void FileCard_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (IsWithinInlineRenameBox(e.OriginalSource))
        {
            return;
        }

        if (sender is not FrameworkElement anchor ||
            GetFileFromSender(sender) is not { } file)
        {
            return;
        }

        CancelInlineRename();
        FocusFileForContextMenu(file);

        CreateFileContextFlyout().ShowAt(
            anchor,
            new FlyoutShowOptions
            {
                Position = e.GetPosition(anchor),
            });

        e.Handled = true;
    }

    private async void FileCard_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        ClearPendingFileCardSelection();

        if (GetFileFromSender(sender) is not { } file || file.IsRenaming)
        {
            args.Cancel = true;
            return;
        }

        if (!file.IsSelected)
        {
            ViewModel.SelectFile(file);
        }

        var paths = ViewModel
            .GetSelectedFilesOrFocusedFile()
            .Select(selectedFile => selectedFile.Path)
            .Where(path => !string.IsNullOrWhiteSpace(path) && PathExists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (paths.Count == 0)
        {
            args.Cancel = true;
            return;
        }

        _draggedPaths = paths;
        args.AllowedOperations = DataPackageOperation.Copy | DataPackageOperation.Move;
        args.Data.RequestedOperation = DataPackageOperation.Move;
        args.Data.Properties.Title = paths.Count == 1
            ? file.FileSystemName
            : $"{paths.Count} items";
        args.Data.SetData(LuminaFilePathsFormat, SerializeFilePaths(paths));

        var deferral = args.GetDeferral();
        try
        {
            var storageItems = await GetStorageItemsFromPathsAsync(paths);
            if (storageItems.Count > 0)
            {
                args.Data.SetStorageItems(storageItems);
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void FileCard_DropCompleted(UIElement sender, DropCompletedEventArgs args)
    {
        _draggedPaths = null;
        ClearDropTarget();
    }

    private void FileCard_DragOver(object sender, DragEventArgs e)
    {
        if (GetFileFromSender(sender) is not { IsDirectory: true } file)
        {
            return;
        }

        var handled = TrySetAcceptedFileDropOperation(
            e,
            file.Path,
            out var acceptedOperation);
        if (!handled)
        {
            return;
        }

        SetDropTarget(acceptedOperation == DataPackageOperation.None ? null : file);
        e.Handled = true;
    }

    private void FileCard_DragLeave(object sender, DragEventArgs e)
    {
        if (GetFileFromSender(sender) is { } file && ReferenceEquals(_dropTargetFile, file))
        {
            ClearDropTarget();
        }
    }

    private async void FileCard_Drop(object sender, DragEventArgs e)
    {
        if (GetFileFromSender(sender) is not { IsDirectory: true } file)
        {
            return;
        }

        e.Handled = true;
        await DropFilesIntoDirectoryAsync(e, file.Path);
    }

    private void FileDropSurface_DragOver(object sender, DragEventArgs e)
    {
        ClearDropTarget();
        if (TrySetAcceptedFileDropOperation(
                e,
                ViewModel.CurrentPath,
                out _))
        {
            e.Handled = true;
        }
    }

    private void FileDropSurface_DragLeave(object sender, DragEventArgs e)
    {
        ClearDropTarget();
    }

    private async void FileDropSurface_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        await DropFilesIntoDirectoryAsync(e, ViewModel.CurrentPath);
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
                case VirtualKey.Enter:
                    _ = ShowSelectedFilePropertiesAsync();
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
            case VirtualKey.N:
                if (isControlDown && isShiftDown)
                {
                    _ = CreateFolderAsync();
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
            case VirtualKey.Y:
                if (isControlDown)
                {
                    _ = RedoLastFileOperationAsync();
                    e.Handled = true;
                }

                break;
            case VirtualKey.Z:
                if (isControlDown)
                {
                    if (isShiftDown)
                    {
                        _ = RedoLastFileOperationAsync();
                    }
                    else
                    {
                        _ = UndoLastFileOperationAsync();
                    }

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

    private async Task CreateFolderAsync()
    {
        if (!ViewModel.CanUseFolderCommands)
        {
            return;
        }

        CancelPendingSearch();
        _isSearchTextComposing = false;

        await RunFileOperationAsync(
            async () =>
            {
                var operationResult = await ViewModel.CreateFolderAsync();
                RecordFileOperation(operationResult);

                var folder = ViewModel.SelectedFile;
                if (folder is null)
                {
                    return;
                }

                EnsureSelectedFileVisible();
                BeginInlineRename(folder);
            },
            "New folder failed");
        FileGridScrollViewer.Focus(FocusState.Programmatic);
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

            var operationResult = await ViewModel.RenameFileAsync(file, newFileSystemName);
            RecordFileOperation(operationResult);
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

        FileOperationResult? operationResult = null;
        await RunFileOperationAsync(
            async () =>
            {
                operationResult = await ViewModel.DeleteFilesAsync(
                    files,
                    permanently
                        ? FileDeleteBehavior.Permanent
                        : FileDeleteBehavior.RecycleBin);
            },
            "Delete failed");
        RecordFileOperation(operationResult);
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

        if (!ViewModel.ContainsPathsInCurrentLocation(clipboard.Paths))
        {
            await ShowFileOperationErrorDialogAsync(
                "Paste failed",
                "Only files inside the current location can be copied or moved.");
            return;
        }

        var operationKind = clipboard.Operation == FileClipboardOperation.Cut
            ? FileOperationKind.Move
            : FileOperationKind.Copy;
        var conflictResolver = await CreateConflictResolverAsync(
            operationKind,
            clipboard.Paths,
            ViewModel.CurrentPath);
        var result = await RunFileTransferOperationAsync(
            operationKind,
            clipboard.Paths.Count,
            async (progress, cancellationToken) =>
            {
                if (clipboard.Operation == FileClipboardOperation.Cut)
                {
                    return await ViewModel.MoveFilesIntoCurrentDirectoryAsync(
                        clipboard.Paths,
                        progress,
                        conflictResolver,
                        cancellationToken);
                }

                return await ViewModel.CopyFilesIntoCurrentDirectoryAsync(
                    clipboard.Paths,
                    progress,
                    conflictResolver,
                    cancellationToken);
            });

        if (result.Succeeded && clipboard.Operation == FileClipboardOperation.Cut)
        {
            ClearCutClipboard();
        }

        if (result.Succeeded)
        {
            RecordFileOperation(result.OperationResult);
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            await ShowFileOperationErrorDialogAsync("Paste failed", result.ErrorMessage);
        }

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

    private async Task ShowSelectedFileInFileExplorerAsync()
    {
        if (ViewModel.SelectedFile is null)
        {
            return;
        }

        await ShowFileInFileExplorerAsync(ViewModel.SelectedFile);
    }

    private async Task ShowSelectedFilePropertiesAsync()
    {
        if (ViewModel.SelectedFile is null)
        {
            return;
        }

        await ShowFilePropertiesAsync(ViewModel.SelectedFile);
    }

    private async Task ShowFileInFileExplorerAsync(FileExplorerItemViewModel file)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{file.Path}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            await ShowFileOperationErrorDialogAsync(
                "Open in File Explorer failed",
                ex.Message);
        }
    }

    private async Task ShowFilePropertiesAsync(FileExplorerItemViewModel file)
    {
        try
        {
            var properties = await Task.Run(() => LoadFileProperties(file));
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = $"{properties.Name} Properties",
                Content = CreateFilePropertiesContent(properties),
                CloseButtonText = "OK",
                DefaultButton = ContentDialogButton.Close,
            };

            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            await ShowFileOperationErrorDialogAsync("Properties failed", ex.Message);
        }
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

    private async Task<FileTransferOperationResult> RunFileTransferOperationAsync(
        FileOperationKind operationKind,
        int itemCount,
        Func<IProgress<FileOperationProgress>, CancellationToken, Task<FileOperationResult?>> operation,
        string? title = null)
    {
        using var operationCancellation = new CancellationTokenSource();
        var progressState = new FileOperationProgressDialogState();
        var progress = new Progress<FileOperationProgress>(progressState.Apply);
        var isOperationComplete = false;
        var dialog = CreateFileOperationProgressDialog(
            title ?? CreateFileOperationTitle(operationKind, itemCount),
            progressState);

        void RequestCancel(ContentDialogButtonClickEventArgs args)
        {
            if (isOperationComplete)
            {
                return;
            }

            args.Cancel = true;
            operationCancellation.Cancel();
            progressState.MarkCancelling();
        }

        dialog.CloseButtonClick += (_, args) => RequestCancel(args);
        dialog.Closing += (_, args) =>
        {
            if (isOperationComplete)
            {
                return;
            }

            args.Cancel = true;
            operationCancellation.Cancel();
            progressState.MarkCancelling();
        };

        IAsyncOperation<ContentDialogResult>? dialogOperation = null;
        var isDialogClosed = false;

        async Task CloseProgressDialogAsync()
        {
            if (isDialogClosed || dialogOperation is null)
            {
                return;
            }

            isOperationComplete = true;
            dialog.Hide();
            await dialogOperation;
            isDialogClosed = true;
        }

        try
        {
            var operationTask = operation(progress, operationCancellation.Token);
            var completedTask = await Task.WhenAny(
                operationTask,
                Task.Delay(ProgressDialogShowDelay));
            if (completedTask != operationTask && !operationTask.IsCompleted)
            {
                dialogOperation = dialog.ShowAsync();
            }

            var operationResult = await operationTask;
            isOperationComplete = true;
            return new FileTransferOperationResult(
                Succeeded: true,
                Cancelled: false,
                ErrorMessage: null,
                OperationResult: operationResult);
        }
        catch (OperationCanceledException) when (operationCancellation.IsCancellationRequested)
        {
            isOperationComplete = true;
            progressState.MarkCancelled();
            return new FileTransferOperationResult(
                Succeeded: false,
                Cancelled: true,
                ErrorMessage: null,
                OperationResult: null);
        }
        catch (Exception ex)
        {
            isOperationComplete = true;
            await CloseProgressDialogAsync();
            return new FileTransferOperationResult(
                Succeeded: false,
                Cancelled: false,
                ErrorMessage: ex.Message,
                OperationResult: null);
        }
        finally
        {
            await CloseProgressDialogAsync();
        }
    }

    private async Task<PlannedFileOperationConflictResolver?> CreateConflictResolverAsync(
        FileOperationKind operation,
        IReadOnlyList<string> sourcePaths,
        string destinationDirectoryPath)
    {
        var resolver = new PlannedFileOperationConflictResolver();
        FileConflictAction? applyToAllAction = null;

        foreach (var sourcePath in sourcePaths)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !PathExists(sourcePath))
            {
                continue;
            }

            var destinationPath = Path.Combine(
                destinationDirectoryPath,
                Path.GetFileName(sourcePath));
            if (!PathExists(destinationPath) ||
                IsSameFileSystemPath(sourcePath, destinationPath))
            {
                continue;
            }

            var action = applyToAllAction;
            if (action is null)
            {
                var dialogResult = await ShowFileConflictDialogAsync(
                    new FileConflictInfo
                    {
                        Operation = operation,
                        SourcePath = sourcePath,
                        DestinationPath = destinationPath,
                        SourceIsDirectory = Directory.Exists(sourcePath),
                        DestinationIsDirectory = Directory.Exists(destinationPath),
                    });
                action = dialogResult.Action;
                if (dialogResult.ApplyToAll)
                {
                    applyToAllAction = action;
                }
            }

            resolver.SetDecision(destinationPath, action.Value);
        }

        return resolver.HasDecisions ? resolver : null;
    }

    private async Task<FileConflictDialogResult> ShowFileConflictDialogAsync(
        FileConflictInfo conflict)
    {
        var isDirectoryMerge = conflict.SourceIsDirectory && conflict.DestinationIsDirectory;
        var applyToAllCheckBox = new CheckBox
        {
            Content = "Do this for all current conflicts",
        };

        var content = new StackPanel
        {
            Width = 460,
            Spacing = 12,
        };
        content.Children.Add(new TextBlock
        {
            Text = $"An item named \"{conflict.Name}\" already exists in the destination.",
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(CreatePropertiesRows(
            ("Source:", conflict.SourcePath),
            ("Destination:", conflict.DestinationPath)));
        content.Children.Add(applyToAllCheckBox);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = isDirectoryMerge ? "Merge folder?" : "Replace or skip file?",
            Content = content,
            PrimaryButtonText = isDirectoryMerge ? "Merge" : "Replace",
            SecondaryButtonText = "Keep both",
            CloseButtonText = "Skip",
            DefaultButton = ContentDialogButton.Secondary,
        };

        var result = await dialog.ShowAsync();
        var action = result switch
        {
            ContentDialogResult.Primary => isDirectoryMerge
                ? FileConflictAction.Merge
                : FileConflictAction.Replace,
            ContentDialogResult.Secondary => FileConflictAction.KeepBoth,
            _ => FileConflictAction.Skip,
        };

        return new FileConflictDialogResult(
            action,
            applyToAllCheckBox.IsChecked == true);
    }

    private void RecordFileOperation(FileOperationResult? operationResult)
    {
        if (operationResult is null || operationResult.Entries.Count == 0)
        {
            return;
        }

        _undoStack.Add(operationResult);
        _redoStack.Clear();
        UpdateHistoryCommandState();
    }

    private async Task UndoLastFileOperationAsync()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        var operationResult = PopLast(_undoStack);
        var result = await RunFileTransferOperationAsync(
            operationResult.Operation,
            operationResult.Entries.Count,
            async (progress, cancellationToken) =>
            {
                await ViewModel.UndoFileOperationAsync(
                    operationResult,
                    progress,
                    cancellationToken);
                return operationResult;
            },
            $"Undoing {FormatItemCount(operationResult.Entries.Count)}");

        if (result.Succeeded)
        {
            _redoStack.Add(operationResult);
        }
        else
        {
            _undoStack.Add(operationResult);
        }

        UpdateHistoryCommandState();

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            await ShowFileOperationErrorDialogAsync("Undo failed", result.ErrorMessage);
        }
    }

    private async Task RedoLastFileOperationAsync()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        var operationResult = PopLast(_redoStack);
        var result = await RunFileTransferOperationAsync(
            operationResult.Operation,
            operationResult.Entries.Count,
            async (progress, cancellationToken) =>
            {
                await ViewModel.RedoFileOperationAsync(
                    operationResult,
                    progress,
                    cancellationToken);
                return operationResult;
            },
            $"Redoing {FormatItemCount(operationResult.Entries.Count)}");

        if (result.Succeeded)
        {
            _undoStack.Add(operationResult);
        }
        else
        {
            _redoStack.Add(operationResult);
        }

        UpdateHistoryCommandState();

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            await ShowFileOperationErrorDialogAsync("Redo failed", result.ErrorMessage);
        }
    }

    private void UpdateHistoryCommandState()
    {
        if (UndoButton is not null)
        {
            UndoButton.IsEnabled = _undoStack.Count > 0;
        }

        if (RedoButton is not null)
        {
            RedoButton.IsEnabled = _redoStack.Count > 0;
        }
    }

    private async Task DropFilesIntoDirectoryAsync(
        DragEventArgs e,
        string destinationDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(destinationDirectoryPath))
        {
            ClearDropTarget();
            return;
        }

        var operation = ResolveDropOperation(e.AllowedOperations);
        if (operation == DataPackageOperation.None)
        {
            ClearDropTarget();
            return;
        }

        var deferral = e.GetDeferral();
        IReadOnlyList<string> paths;
        var errorTitle = operation == DataPackageOperation.Move
            ? "Move failed"
            : "Copy failed";
        try
        {
            paths = await GetDroppedFilePathsAsync(e.DataView);
        }
        finally
        {
            _draggedPaths = null;
            ClearDropTarget();
            deferral.Complete();
        }

        if (paths.Count == 0 ||
            !CanDropPathsIntoDirectory(paths, destinationDirectoryPath))
        {
            return;
        }

        if (!ViewModel.ContainsPathInCurrentLocation(destinationDirectoryPath) ||
            !ViewModel.ContainsPathsInCurrentLocation(paths))
        {
            await ShowFileOperationErrorDialogAsync(
                errorTitle,
                "Only files inside the current location can be copied or moved.");
            return;
        }

        var operationKind = operation == DataPackageOperation.Move
            ? FileOperationKind.Move
            : FileOperationKind.Copy;
        var conflictResolver = await CreateConflictResolverAsync(
            operationKind,
            paths,
            destinationDirectoryPath);
        var result = await RunFileTransferOperationAsync(
            operationKind,
            paths.Count,
            (progress, cancellationToken) => operation == DataPackageOperation.Move
                ? ViewModel.MoveFilesIntoDirectoryAsync(
                    paths,
                    destinationDirectoryPath,
                    progress,
                    conflictResolver,
                    cancellationToken)
                : ViewModel.CopyFilesIntoDirectoryAsync(
                    paths,
                    destinationDirectoryPath,
                    progress,
                    conflictResolver,
                    cancellationToken));
        FileGridScrollViewer.Focus(FocusState.Programmatic);

        if (result.Succeeded)
        {
            RecordFileOperation(result.OperationResult);
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            await ShowFileOperationErrorDialogAsync(errorTitle, result.ErrorMessage);
        }
    }

    private bool TrySetAcceptedFileDropOperation(
        DragEventArgs e,
        string destinationDirectoryPath,
        out DataPackageOperation acceptedOperation)
    {
        acceptedOperation = DataPackageOperation.None;
        if (string.IsNullOrWhiteSpace(destinationDirectoryPath) ||
            !ContainsFileDropData(e.DataView))
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return false;
        }

        acceptedOperation = ResolveDropOperation(e.AllowedOperations);
        if (acceptedOperation != DataPackageOperation.None &&
            _draggedPaths is not null &&
            (!ViewModel.ContainsPathInCurrentLocation(destinationDirectoryPath) ||
                !ViewModel.ContainsPathsInCurrentLocation(_draggedPaths) ||
                !CanDropPathsIntoDirectory(_draggedPaths, destinationDirectoryPath)))
        {
            acceptedOperation = DataPackageOperation.None;
        }

        e.AcceptedOperation = acceptedOperation;
        if (acceptedOperation != DataPackageOperation.None)
        {
            e.DragUIOverride.Caption = acceptedOperation == DataPackageOperation.Copy
                ? "Copy here"
                : "Move here";
            e.DragUIOverride.IsCaptionVisible = true;
        }

        return true;
    }

    private static DataPackageOperation ResolveDropOperation(
        DataPackageOperation allowedOperations)
    {
        if (allowedOperations == DataPackageOperation.None)
        {
            allowedOperations = DataPackageOperation.Copy | DataPackageOperation.Move;
        }

        if (IsKeyDown(VirtualKey.Control) &&
            allowedOperations.HasFlag(DataPackageOperation.Copy))
        {
            return DataPackageOperation.Copy;
        }

        if (allowedOperations.HasFlag(DataPackageOperation.Move))
        {
            return DataPackageOperation.Move;
        }

        return allowedOperations.HasFlag(DataPackageOperation.Copy)
            ? DataPackageOperation.Copy
            : DataPackageOperation.None;
    }

    private void SetDropTarget(FileExplorerItemViewModel? file)
    {
        if (ReferenceEquals(_dropTargetFile, file))
        {
            return;
        }

        if (_dropTargetFile is not null)
        {
            _dropTargetFile.IsDropTarget = false;
        }

        _dropTargetFile = file;

        if (_dropTargetFile is not null)
        {
            _dropTargetFile.IsDropTarget = true;
        }
    }

    private void ClearDropTarget()
    {
        SetDropTarget(null);
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

    private ContentDialog CreateFileOperationProgressDialog(
        string title,
        FileOperationProgressDialogState progressState)
    {
        var root = new StackPanel
        {
            Width = 460,
            Spacing = 12,
        };

        var statusText = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };
        statusText.SetBinding(
            TextBlock.TextProperty,
            CreateBinding(nameof(FileOperationProgressDialogState.StatusText), progressState));
        root.Children.Add(statusText);

        var progressBar = new ProgressBar
        {
            Height = 6,
            Minimum = 0,
            Maximum = 100,
        };
        progressBar.SetBinding(
            ProgressBar.ValueProperty,
            CreateBinding(nameof(FileOperationProgressDialogState.ProgressValue), progressState));
        progressBar.SetBinding(
            ProgressBar.IsIndeterminateProperty,
            CreateBinding(nameof(FileOperationProgressDialogState.IsIndeterminate), progressState));
        root.Children.Add(progressBar);

        var currentItemText = new TextBlock
        {
            Foreground = GetThemeBrush("TextFillColorSecondaryBrush"),
            MaxLines = 1,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        currentItemText.SetBinding(
            TextBlock.TextProperty,
            CreateBinding(nameof(FileOperationProgressDialogState.CurrentItemText), progressState));
        root.Children.Add(currentItemText);

        var detailText = new TextBlock
        {
            Foreground = GetThemeBrush("TextFillColorSecondaryBrush"),
            MaxLines = 1,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        detailText.SetBinding(
            TextBlock.TextProperty,
            CreateBinding(nameof(FileOperationProgressDialogState.DetailText), progressState));
        root.Children.Add(detailText);

        return new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = root,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
    }

    private static Binding CreateBinding(
        string path,
        object source)
    {
        return new Binding
        {
            Path = new PropertyPath(path),
            Source = source,
            Mode = BindingMode.OneWay,
        };
    }

    private static string CreateFileOperationTitle(
        FileOperationKind operation,
        int itemCount)
    {
        var itemText = itemCount == 1 ? "1 item" : $"{itemCount} items";

        return operation switch
        {
            FileOperationKind.Create => $"Creating {itemText}",
            FileOperationKind.Rename => $"Renaming {itemText}",
            FileOperationKind.Delete => $"Deleting {itemText}",
            FileOperationKind.Move => $"Moving {itemText}",
            _ => $"Copying {itemText}",
        };
    }

    private MenuFlyout CreateSortMenuFlyout()
    {
        var flyout = new MenuFlyout
        {
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
        };

        flyout.Items.Add(CreateSortFieldItem(FileSortField.Name, "Name"));
        flyout.Items.Add(CreateSortFieldItem(FileSortField.Modified, "Date modified"));
        flyout.Items.Add(CreateSortFieldItem(FileSortField.Type, "Type"));

        var moreItem = new MenuFlyoutSubItem
        {
            Text = "More",
        };
        moreItem.Items.Add(CreateSortFieldItem(FileSortField.Size, "Size"));
        moreItem.Items.Add(CreateSortFieldItem(FileSortField.Created, "Date created"));
        flyout.Items.Add(moreItem);

        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(CreateSortDirectionItem(FileSortDirection.Ascending, "Ascending"));
        flyout.Items.Add(CreateSortDirectionItem(FileSortDirection.Descending, "Descending"));

        return flyout;
    }

    private CommandBarFlyout CreateFileContextFlyout()
    {
        var flyout = new CommandBarFlyout
        {
            Placement = FlyoutPlacementMode.RightEdgeAlignedTop,
        };

        flyout.PrimaryCommands.Add(CreateContextCommandButton(
            "Cut",
            "\uE8C6",
            CutButton_Click));
        flyout.PrimaryCommands.Add(CreateContextCommandButton(
            "Copy",
            "\uE8C8",
            CopyButton_Click));
        flyout.PrimaryCommands.Add(CreateContextCommandButton(
            "Rename",
            "\uE8AC",
            RenameButton_Click));
        flyout.PrimaryCommands.Add(CreateContextCommandButton(
            "Delete",
            "\uE74D",
            DeleteButton_Click));

        var openButton = CreateContextCommandButton(
            "Open",
            "\uE8E5",
            OpenFileContextButton_Click);
        openButton.KeyboardAccelerators.Add(new KeyboardAccelerator
        {
            Key = VirtualKey.Enter,
        });
        flyout.SecondaryCommands.Add(openButton);

        flyout.SecondaryCommands.Add(CreateContextCommandButton(
            "Show in File Explorer",
            "\uE8A7",
            ShowInFileExplorerContextButton_Click));

        var propertiesButton = CreateContextCommandButton(
            "Properties",
            "\uE946",
            PropertiesContextButton_Click);
        propertiesButton.KeyboardAccelerators.Add(new KeyboardAccelerator
        {
            Key = VirtualKey.Enter,
            Modifiers = VirtualKeyModifiers.Menu,
        });
        flyout.SecondaryCommands.Add(propertiesButton);

        return flyout;
    }

    private static AppBarButton CreateContextCommandButton(
        string label,
        string glyph,
        RoutedEventHandler clickHandler)
    {
        var button = new AppBarButton
        {
            Label = label,
            Icon = new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 16,
                Glyph = glyph,
            },
        };
        button.Click += clickHandler;
        ToolTipService.SetToolTip(button, label);

        return button;
    }

    private static FilePropertiesDialogInfo LoadFileProperties(FileExplorerItemViewModel file)
    {
        if (Directory.Exists(file.Path))
        {
            var directory = new DirectoryInfo(file.Path);
            var totals = CalculateDirectoryStorage(directory.FullName);

            return new FilePropertiesDialogInfo(
                directory.Name,
                "\uE8B7",
                "Folder",
                directory.Parent?.FullName ?? directory.FullName,
                totals.Size,
                totals.SizeOnDisk,
                new DateTimeOffset(directory.CreationTimeUtc, TimeSpan.Zero),
                new DateTimeOffset(directory.LastWriteTimeUtc, TimeSpan.Zero));
        }

        if (File.Exists(file.Path))
        {
            var fileInfo = new FileInfo(file.Path);

            return new FilePropertiesDialogInfo(
                fileInfo.Name,
                file.IconGlyph,
                ResolvePropertyType(fileInfo),
                fileInfo.DirectoryName ?? string.Empty,
                fileInfo.Length,
                CalculateSizeOnDisk(fileInfo),
                new DateTimeOffset(fileInfo.CreationTimeUtc, TimeSpan.Zero),
                new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero));
        }

        return new FilePropertiesDialogInfo(
            file.FileSystemName,
            file.IconGlyph,
            file.IsDirectory ? "Folder" : "File",
            Path.GetDirectoryName(file.Path) ?? string.Empty,
            file.File.Size,
            CalculateFallbackSizeOnDisk(file.File.Size),
            file.File.Created,
            file.File.Modified);
    }

    private static FrameworkElement CreateFilePropertiesContent(FilePropertiesDialogInfo properties)
    {
        var root = new StackPanel
        {
            Width = 460,
            Spacing = 14,
        };

        root.Children.Add(CreatePropertiesHeader(properties));
        root.Children.Add(CreatePropertiesSeparator());
        root.Children.Add(CreatePropertiesRows(
            ("Type:", properties.Type),
            ("Location:", properties.Location),
            ("Size:", FormatByteCount(properties.Size)),
            ("Size on disk:", FormatByteCount(properties.SizeOnDisk))));
        root.Children.Add(CreatePropertiesSeparator());
        root.Children.Add(CreatePropertiesRows(
            ("Created:", FormatDateTime(properties.Created)),
            ("Modified:", FormatDateTime(properties.Modified))));

        return root;
    }

    private static FrameworkElement CreatePropertiesHeader(FilePropertiesDialogInfo properties)
    {
        var header = new Grid
        {
            ColumnSpacing = 16,
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new FontIcon
        {
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 40,
            Glyph = properties.IconGlyph,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(icon, 0);
        header.Children.Add(icon);

        var name = new TextBlock
        {
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            IsTextSelectionEnabled = true,
            Text = properties.Name,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(name, 1);
        header.Children.Add(name);

        return header;
    }

    private static Grid CreatePropertiesRows(params (string Label, string Value)[] rows)
    {
        var grid = new Grid
        {
            RowSpacing = 10,
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (var index = 0; index < rows.Length; index++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = new TextBlock
            {
                Text = rows[index].Label,
                VerticalAlignment = VerticalAlignment.Top,
            };
            Grid.SetRow(label, index);
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            var value = new TextBlock
            {
                IsTextSelectionEnabled = true,
                Text = rows[index].Value,
                TextWrapping = TextWrapping.Wrap,
            };
            Grid.SetRow(value, index);
            Grid.SetColumn(value, 1);
            grid.Children.Add(value);
        }

        return grid;
    }

    private static FrameworkElement CreatePropertiesSeparator()
    {
        return new Border
        {
            Height = 1,
            Background = GetThemeBrush("DividerStrokeColorDefaultBrush"),
        };
    }

    private static (long Size, long SizeOnDisk) CalculateDirectoryStorage(string directoryPath)
    {
        var size = 0L;
        var sizeOnDisk = 0L;
        var directory = new DirectoryInfo(directoryPath);
        var enumerationOptions = new EnumerationOptions
        {
            AttributesToSkip = System.IO.FileAttributes.ReparsePoint,
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
        };

        foreach (var file in directory.EnumerateFiles("*", enumerationOptions))
        {
            try
            {
                size += file.Length;
                sizeOnDisk += CalculateSizeOnDisk(file);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return (size, sizeOnDisk);
    }

    private static long CalculateSizeOnDisk(FileInfo file)
    {
        uint highSize;
        var lowSize = GetCompressedFileSize(file.FullName, out highSize);
        if (lowSize != InvalidFileSize || Marshal.GetLastWin32Error() == 0)
        {
            return ((long)highSize << 32) + lowSize;
        }

        return CalculateFallbackSizeOnDisk(file.Length);
    }

    private static long CalculateFallbackSizeOnDisk(long size)
    {
        if (size <= 0)
        {
            return 0;
        }

        return ((size + DefaultAllocationUnitSize - 1) / DefaultAllocationUnitSize) *
            DefaultAllocationUnitSize;
    }

    private static string ResolvePropertyType(FileInfo file)
    {
        var extension = file.Extension;
        if (string.IsNullOrWhiteSpace(extension))
        {
            return "File";
        }

        return $"{extension.TrimStart('.').ToUpperInvariant()} File ({extension})";
    }

    private static string FormatByteCount(long bytes)
    {
        var clampedBytes = Math.Max(0, bytes);
        if (clampedBytes == 0)
        {
            return "0 bytes";
        }

        string[] units = ["bytes", "KB", "MB", "GB", "TB"];
        var size = (double)clampedBytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        var readableSize = unitIndex == 0
            ? $"{clampedBytes.ToString("N0", CultureInfo.CurrentCulture)} bytes"
            : $"{size.ToString("0.#", CultureInfo.CurrentCulture)} {units[unitIndex]}";

        return $"{readableSize} ({clampedBytes.ToString("N0", CultureInfo.CurrentCulture)} bytes)";
    }

    private static string FormatCompactByteCount(long bytes)
    {
        var clampedBytes = Math.Max(0, bytes);
        if (clampedBytes == 0)
        {
            return "0 bytes";
        }

        string[] units = ["bytes", "KB", "MB", "GB", "TB"];
        var size = (double)clampedBytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{clampedBytes.ToString("N0", CultureInfo.CurrentCulture)} bytes"
            : $"{size.ToString("0.#", CultureInfo.CurrentCulture)} {units[unitIndex]}";
    }

    private static string FormatDateTime(DateTimeOffset dateTime)
    {
        return dateTime
            .ToLocalTime()
            .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
    }

    private static string FormatItemCount(int itemCount)
    {
        return itemCount == 1 ? "1 item" : $"{itemCount} items";
    }

    private static T PopLast<T>(List<T> items)
    {
        var lastIndex = items.Count - 1;
        var item = items[lastIndex];
        items.RemoveAt(lastIndex);

        return item;
    }

    private static Brush? GetThemeBrush(string resourceKey)
    {
        return Application.Current.Resources.TryGetValue(resourceKey, out var value) &&
            value is Brush brush
                ? brush
                : null;
    }

    private MenuFlyoutItem CreateSortFieldItem(FileSortField sortField, string text)
    {
        var item = new MenuFlyoutItem
        {
            Text = text,
            Tag = sortField,
            Icon = CreateMenuSelectionIcon(ViewModel.SortField == sortField),
        };
        item.Click += SortFieldMenuItem_Click;

        return item;
    }

    private MenuFlyoutItem CreateSortDirectionItem(
        FileSortDirection sortDirection,
        string text)
    {
        var item = new MenuFlyoutItem
        {
            Text = text,
            Tag = sortDirection,
            Icon = CreateMenuSelectionIcon(ViewModel.SortDirection == sortDirection),
        };
        item.Click += SortDirectionMenuItem_Click;

        return item;
    }

    private static IconElement CreateMenuSelectionIcon(bool isSelected)
    {
        return new FontIcon
        {
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 12,
            Glyph = "\uE73E",
            Opacity = isSelected ? 1 : 0,
        };
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
        ApplyFileClipboardVisualState();
        await TrySetSystemClipboardAsync(paths, operation);
    }

    private async Task<FileClipboardState?> GetFileClipboardAsync()
    {
        return await TryGetSystemClipboardAsync() ?? _fileClipboard;
    }

    private async Task RefreshClipboardVisualStateAsync()
    {
        _fileClipboard = await TryGetSystemClipboardAsync();
        ApplyFileClipboardVisualState();
    }

    private async Task TrySetSystemClipboardAsync(
        IReadOnlyList<string> paths,
        FileClipboardOperation operation)
    {
        try
        {
            var storageItems = await GetStorageItemsFromPathsAsync(paths);
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

    private static async Task<IReadOnlyList<IStorageItem>> GetStorageItemsFromPathsAsync(
        IReadOnlyList<string> paths)
    {
        var storageItems = new List<IStorageItem>(paths.Count);
        foreach (var path in paths)
        {
            try
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
            catch (Exception)
            {
            }
        }

        return storageItems;
    }

    private static bool ContainsFileDropData(DataPackageView dataView)
    {
        return dataView.Contains(LuminaFilePathsFormat) ||
            dataView.Contains(StandardDataFormats.StorageItems);
    }

    private static async Task<IReadOnlyList<string>> GetDroppedFilePathsAsync(
        DataPackageView dataView)
    {
        if (dataView.Contains(LuminaFilePathsFormat))
        {
            var data = await dataView.GetDataAsync(LuminaFilePathsFormat);
            return ParseFilePaths(data?.ToString() ?? string.Empty);
        }

        if (!dataView.Contains(StandardDataFormats.StorageItems))
        {
            return [];
        }

        var storageItems = await dataView.GetStorageItemsAsync();
        return storageItems
            .Select(item => item.Path)
            .Where(path => !string.IsNullOrWhiteSpace(path) && PathExists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string SerializeFilePaths(IReadOnlyList<string> paths)
    {
        return string.Join('\n', paths);
    }

    private static IReadOnlyList<string> ParseFilePaths(string serializedPaths)
    {
        return serializedPaths
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(path => PathExists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool CanDropPathsIntoDirectory(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(destinationDirectoryPath))
        {
            return false;
        }

        foreach (var sourcePath in sourcePaths)
        {
            if (!Directory.Exists(sourcePath))
            {
                continue;
            }

            if (IsSameFileSystemPath(sourcePath, destinationDirectoryPath) ||
                IsSubPathOf(destinationDirectoryPath, sourcePath))
            {
                return false;
            }
        }

        return true;
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

    private void ClearCutClipboard()
    {
        if (_fileClipboard?.Operation == FileClipboardOperation.Cut)
        {
            _fileClipboard = null;
        }

        ApplyFileClipboardVisualState();

        try
        {
            Clipboard.Clear();
        }
        catch (Exception)
        {
        }
    }

    private void ApplyFileClipboardVisualState()
    {
        var cutPaths = CreateCutPathSet(_fileClipboard);
        foreach (var file in ViewModel.Files)
        {
            ApplyFileCutState(file, cutPaths);
        }
    }

    private static HashSet<string>? CreateCutPathSet(FileClipboardState? clipboard)
    {
        if (clipboard?.Operation != FileClipboardOperation.Cut)
        {
            return null;
        }

        var paths = clipboard.Paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizeClipboardPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return paths.Count == 0 ? null : paths;
    }

    private static void ApplyFileCutState(
        FileExplorerItemViewModel file,
        HashSet<string>? cutPaths)
    {
        file.IsCut = cutPaths?.Contains(NormalizeClipboardPath(file.Path)) == true;
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

    private static string NormalizeClipboardPath(string path)
    {
        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch (Exception)
        {
            return path.Trim();
        }
    }

    private static bool IsSameFileSystemPath(string left, string right)
    {
        return string.Equals(
            NormalizeClipboardPath(left).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar),
            NormalizeClipboardPath(right).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSubPathOf(string candidatePath, string parentPath)
    {
        var normalizedCandidate = NormalizeClipboardPath(candidatePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var normalizedParent = NormalizeClipboardPath(parentPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        return normalizedCandidate.StartsWith(
            normalizedParent,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRightButtonPressed(
        object sender,
        PointerRoutedEventArgs e)
    {
        return sender is UIElement element &&
            e.GetCurrentPoint(element).Properties.IsRightButtonPressed;
    }

    private void FocusFileForContextMenu(FileExplorerItemViewModel file)
    {
        if (file.IsSelected)
        {
            ViewModel.FocusFile(file);
        }
        else
        {
            ViewModel.SelectFile(file);
        }

        FileGridScrollViewer.Focus(FocusState.Pointer);
    }

    private void ClearPendingFileCardSelection()
    {
        _pendingSingleSelectionFile = null;
        _pendingToggleSelectionFile = null;
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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetCompressedFileSize(
        string lpFileName,
        out uint lpFileSizeHigh);

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

    private static bool IsWithinRelativePathButton(object? source)
    {
        var dependencyObject = source as DependencyObject;
        while (dependencyObject is not null)
        {
            if (dependencyObject is FrameworkElement { Name: "RelativePathButton" })
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
