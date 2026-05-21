using System.Collections.ObjectModel;
using System.Globalization;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

using Lumina.App.Services;
using Lumina.Core.Models;
using Lumina.Core.Services;

namespace Lumina.App.ViewModels;

public sealed class FileExplorerViewModel : ObservableObject
{
    private static readonly double[] CardWidthZoomLevels = [176, 208, 240, 280, 320, 368];
    private const double InfoPanelHeight = 48;
    private const int DefaultZoomLevelIndex = 2;
    private const string DefaultNewFolderName = "New folder";

    private readonly IFileBrowserService _fileBrowserService;
    private readonly IFileThumbnailService _fileThumbnailService;
    private readonly List<string> _backStack = [];
    private readonly List<string> _forwardStack = [];

    private string _currentPath = string.Empty;
    private string _currentLocationName = string.Empty;
    private string? _errorMessage;
    private string _searchQuery = string.Empty;
    private bool _isBusy;
    private double _cardHeight = CalculateCardHeight(CardWidthZoomLevels[DefaultZoomLevelIndex]);
    private double _cardWidth = CardWidthZoomLevels[DefaultZoomLevelIndex];
    private double _thumbnailIconFontSize = CalculateThumbnailIconFontSize(CardWidthZoomLevels[DefaultZoomLevelIndex]);
    private Location? _currentLocation;
    private CancellationTokenSource? _loadCancellation;
    private CancellationTokenSource? _thumbnailCancellation;
    private FileExplorerItemViewModel? _selectedFile;
    private FileExplorerItemViewModel? _selectionAnchor;
    private FileSortOptions _sortOptions = FileSortOptions.Default;
    private int _zoomLevelIndex = DefaultZoomLevelIndex;

    public FileExplorerViewModel()
        : this(new FileSystemBrowserService(), new ShellFileThumbnailService())
    {
    }

    public FileExplorerViewModel(IFileBrowserService fileBrowserService)
        : this(fileBrowserService, new ShellFileThumbnailService())
    {
    }

    public FileExplorerViewModel(
        IFileBrowserService fileBrowserService,
        IFileThumbnailService fileThumbnailService)
    {
        _fileBrowserService = fileBrowserService;
        _fileThumbnailService = fileThumbnailService;
    }

    public ObservableCollection<FileExplorerItemViewModel> Files { get; } = [];

    public string CurrentPath
    {
        get => _currentPath;
        private set
        {
            if (SetProperty(ref _currentPath, value))
            {
                OnPropertyChanged(nameof(BreadcrumbText));
                OnPropertyChanged(nameof(BreadcrumbItems));
                OnPropertyChanged(nameof(SearchPlaceholderText));
                OnComputedStateChanged();
            }
        }
    }

    public string CurrentLocationName
    {
        get => _currentLocationName;
        private set => SetProperty(ref _currentLocationName, value);
    }

    public string BreadcrumbText => string.IsNullOrWhiteSpace(CurrentPath)
        ? "No location selected"
        : CurrentPath;

    public IReadOnlyList<FileExplorerBreadcrumbItemViewModel> BreadcrumbItems =>
        BuildBreadcrumbItems(CurrentPath);

    public string SearchPlaceholderText => string.IsNullOrWhiteSpace(CurrentPath)
        ? "Search"
        : $"Search in {GetCurrentFolderName(CurrentPath)}";

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnComputedStateChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnComputedStateChanged();
            }
        }
    }

    public bool CanRefresh => !IsBusy && !string.IsNullOrWhiteSpace(CurrentPath);

    public bool CanSearch => !string.IsNullOrWhiteSpace(CurrentPath);

    public bool CanNavigateBack => _backStack.Count > 0;

    public bool CanNavigateForward => _forwardStack.Count > 0;

    public bool CanNavigateToParent => !string.IsNullOrWhiteSpace(GetParentDirectoryPath(CurrentPath));

    public bool CanUseFolderCommands => !IsBusy && !string.IsNullOrWhiteSpace(CurrentPath);

    public bool CanUseSelectedFileCommands => CanUseFolderCommands && SelectedFile is not null;

    public bool CanSort => CanUseFolderCommands;

    public bool HasFiles => Files.Count > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyStateVisibility =>
        !IsBusy && !HasError && _currentLocation is not null && !HasFiles
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ErrorVisibility => HasError ? Visibility.Visible : Visibility.Collapsed;

    public Visibility FileGridVisibility =>
        !IsBusy && _currentLocation is not null && HasFiles
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility NoLocationVisibility =>
        !IsBusy && !HasError && _currentLocation is null
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public FileSortField SortField => _sortOptions.Field;

    public FileSortDirection SortDirection => _sortOptions.Direction;

    public async Task OpenLocationAsync(
        Location? location,
        CancellationToken cancellationToken = default)
    {
        _loadCancellation?.Cancel();
        CancelPendingThumbnailLoading();

        _currentLocation = location;
        CurrentLocationName = location?.Name ?? string.Empty;
        CurrentPath = location?.Path ?? string.Empty;
        ClearSearchQuery();
        _backStack.Clear();
        _forwardStack.Clear();
        ClearSelection();
        Files.Clear();
        ErrorMessage = null;
        OnComputedStateChanged();

        if (location is null)
        {
            return;
        }

        await LoadCurrentDirectoryAsync(cancellationToken);
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        return string.IsNullOrWhiteSpace(CurrentPath)
            ? Task.CompletedTask
            : LoadCurrentDirectoryAsync(cancellationToken);
    }

    public Task SearchAsync(CancellationToken cancellationToken = default)
    {
        return string.IsNullOrWhiteSpace(CurrentPath)
            ? Task.CompletedTask
            : LoadCurrentDirectoryAsync(cancellationToken);
    }

    public double CardHeight
    {
        get => _cardHeight;
        private set => SetProperty(ref _cardHeight, value);
    }

    public double CardWidth
    {
        get => _cardWidth;
        private set => SetProperty(ref _cardWidth, value);
    }

    public double ThumbnailIconFontSize
    {
        get => _thumbnailIconFontSize;
        private set => SetProperty(ref _thumbnailIconFontSize, value);
    }

    public FileExplorerItemViewModel? SelectedFile
    {
        get => _selectedFile;
        private set
        {
            if (ReferenceEquals(_selectedFile, value))
            {
                return;
            }

            if (_selectedFile is not null)
            {
                _selectedFile.IsFocused = false;
            }

            _selectedFile = value;

            if (_selectedFile is not null)
            {
                _selectedFile.IsFocused = true;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(CanUseSelectedFileCommands));
        }
    }

    public void SelectFile(FileExplorerItemViewModel? file)
    {
        ClearSelectedFiles();
        SelectedFile = file;
        _selectionAnchor = file;

        if (file is not null)
        {
            file.IsSelected = true;
        }
    }

    public void FocusFile(FileExplorerItemViewModel? file)
    {
        SelectedFile = file;
    }

    public void ToggleFileSelection(FileExplorerItemViewModel file)
    {
        ArgumentNullException.ThrowIfNull(file);

        SelectedFile = file;
        file.IsSelected = !file.IsSelected;
        _selectionAnchor = file;
    }

    public void ExtendSelectionTo(FileExplorerItemViewModel file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var anchor = _selectionAnchor ?? SelectedFile ?? file;
        var anchorIndex = Files.IndexOf(anchor);
        var targetIndex = Files.IndexOf(file);

        if (anchorIndex < 0 || targetIndex < 0)
        {
            SelectFile(file);
            return;
        }

        ClearSelectedFiles();
        SelectedFile = file;
        _selectionAnchor = anchor;

        var start = Math.Min(anchorIndex, targetIndex);
        var end = Math.Max(anchorIndex, targetIndex);

        for (var index = start; index <= end; index++)
        {
            Files[index].IsSelected = true;
        }
    }

    public void SelectAllFiles()
    {
        if (Files.Count == 0)
        {
            return;
        }

        foreach (var file in Files)
        {
            file.IsSelected = true;
        }

        _selectionAnchor ??= Files[0];
        SelectedFile ??= Files[0];
    }

    public IReadOnlyList<FileExplorerItemViewModel> GetSelectedFilesOrFocusedFile()
    {
        var selectedFiles = Files.Where(file => file.IsSelected).ToList();
        if (selectedFiles.Count > 0)
        {
            return selectedFiles;
        }

        return SelectedFile is null ? [] : [SelectedFile];
    }

    public async Task OpenDirectoryAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        var targetPath = NormalizeDirectoryPath(directoryPath);
        if (IsSameDirectory(CurrentPath, targetPath))
        {
            await RefreshAsync(cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(CurrentPath))
        {
            _backStack.Add(CurrentPath);
        }

        _forwardStack.Clear();
        CurrentPath = targetPath;
        ClearSearchQuery();
        ClearSelection();
        await LoadCurrentDirectoryAsync(cancellationToken);
    }

    public async Task NavigateBackAsync(CancellationToken cancellationToken = default)
    {
        if (_backStack.Count == 0)
        {
            return;
        }

        var targetPath = PopLast(_backStack);
        if (!string.IsNullOrWhiteSpace(CurrentPath))
        {
            _forwardStack.Add(CurrentPath);
        }

        CurrentPath = targetPath;
        ClearSearchQuery();
        ClearSelection();
        await LoadCurrentDirectoryAsync(cancellationToken);
    }

    public async Task NavigateForwardAsync(CancellationToken cancellationToken = default)
    {
        if (_forwardStack.Count == 0)
        {
            return;
        }

        var targetPath = PopLast(_forwardStack);
        if (!string.IsNullOrWhiteSpace(CurrentPath))
        {
            _backStack.Add(CurrentPath);
        }

        CurrentPath = targetPath;
        ClearSearchQuery();
        ClearSelection();
        await LoadCurrentDirectoryAsync(cancellationToken);
    }

    public async Task NavigateToParentAsync(CancellationToken cancellationToken = default)
    {
        var parentPath = GetParentDirectoryPath(CurrentPath);
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            return;
        }

        await OpenDirectoryAsync(parentPath, cancellationToken);
    }

    public void ZoomByWheelDelta(int wheelDelta)
    {
        if (wheelDelta == 0)
        {
            return;
        }

        var direction = wheelDelta > 0 ? 1 : -1;
        var nextZoomLevelIndex = Math.Clamp(
            _zoomLevelIndex + direction,
            0,
            CardWidthZoomLevels.Length - 1);

        if (nextZoomLevelIndex == _zoomLevelIndex)
        {
            return;
        }

        _zoomLevelIndex = nextZoomLevelIndex;
        var cardWidth = CardWidthZoomLevels[_zoomLevelIndex];

        CardWidth = cardWidth;
        CardHeight = CalculateCardHeight(cardWidth);
        ThumbnailIconFontSize = CalculateThumbnailIconFontSize(cardWidth);

        foreach (var file in Files)
        {
            file.UpdateCardLayout(CardWidth, CardHeight, ThumbnailIconFontSize);
        }
    }

    public Task SortByAsync(
        FileSortField sortField,
        CancellationToken cancellationToken = default)
    {
        return ApplySortOptionsAsync(
            _sortOptions with { Field = sortField },
            cancellationToken);
    }

    public Task SortDirectionAsync(
        FileSortDirection sortDirection,
        CancellationToken cancellationToken = default)
    {
        return ApplySortOptionsAsync(
            _sortOptions with { Direction = sortDirection },
            cancellationToken);
    }

    public async Task RenameFileAsync(
        FileExplorerItemViewModel file,
        string newName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        var renamedPath = await _fileBrowserService.RenameAsync(
            file.Path,
            newName,
            cancellationToken);
        await RefreshAndSelectAsync([renamedPath], cancellationToken);
    }

    public async Task<FileExplorerItemViewModel?> CreateFolderAsync(
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(CurrentPath))
        {
            return null;
        }

        ClearSearchQuery();

        var createdPath = await _fileBrowserService.CreateDirectoryAsync(
            CurrentPath,
            DefaultNewFolderName,
            cancellationToken);
        await RefreshAndSelectAsync([createdPath], cancellationToken);

        return SelectedFile;
    }

    public async Task DeleteFilesAsync(
        IReadOnlyList<FileExplorerItemViewModel> files,
        FileDeleteBehavior deleteBehavior,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        if (files.Count == 0)
        {
            return;
        }

        var nextSelectionIndex = files
            .Select(file => Files.IndexOf(file))
            .Where(index => index >= 0)
            .DefaultIfEmpty(0)
            .Min();
        var paths = files.Select(file => file.Path).ToList();

        await _fileBrowserService.DeleteAsync(paths, deleteBehavior, cancellationToken);
        await LoadCurrentDirectoryAsync(cancellationToken);

        if (Files.Count > 0)
        {
            SelectFile(Files[Math.Clamp(nextSelectionIndex, 0, Files.Count - 1)]);
        }
    }

    public async Task CopyFilesIntoCurrentDirectoryAsync(
        IReadOnlyList<string> sourcePaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        if (sourcePaths.Count == 0 || string.IsNullOrWhiteSpace(CurrentPath))
        {
            return;
        }

        var copiedPaths = await _fileBrowserService.CopyAsync(
            sourcePaths,
            CurrentPath,
            cancellationToken);
        await RefreshAndSelectAsync(copiedPaths, cancellationToken);
    }

    public async Task MoveFilesIntoCurrentDirectoryAsync(
        IReadOnlyList<string> sourcePaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        if (sourcePaths.Count == 0 || string.IsNullOrWhiteSpace(CurrentPath))
        {
            return;
        }

        var movedPaths = await _fileBrowserService.MoveAsync(
            sourcePaths,
            CurrentPath,
            cancellationToken);
        await RefreshAndSelectAsync(movedPaths, cancellationToken);
    }

    private async Task LoadCurrentDirectoryAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(CurrentPath))
        {
            return;
        }

        _loadCancellation?.Cancel();
        CancelPendingThumbnailLoading();
        var loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loadCancellation = loadCancellation;

        IsBusy = true;
        ErrorMessage = null;
        ClearSelection();
        Files.Clear();
        OnComputedStateChanged();

        try
        {
            var files = string.IsNullOrWhiteSpace(SearchQuery)
                ? await _fileBrowserService.LoadDirectoryAsync(
                    CurrentPath,
                    _sortOptions,
                    loadCancellation.Token)
                : await _fileBrowserService.SearchDirectoryAsync(
                    CurrentPath,
                    SearchQuery,
                    _sortOptions,
                    loadCancellation.Token);

            loadCancellation.Token.ThrowIfCancellationRequested();

            var thumbnailItems = new List<FileExplorerItemViewModel>();
            foreach (var file in files)
            {
                var item = new FileExplorerItemViewModel(
                    file,
                    CardWidth,
                    CardHeight,
                    ThumbnailIconFontSize);
                Files.Add(item);

                if (item.CanLoadThumbnail)
                {
                    thumbnailItems.Add(item);
                }
            }

            OnComputedStateChanged();
            StartThumbnailLoading(thumbnailItems);
        }
        catch (OperationCanceledException) when (loadCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load folder: {ex.Message}";
        }
        finally
        {
            if (_loadCancellation == loadCancellation)
            {
                IsBusy = false;
                _loadCancellation = null;
            }

            loadCancellation.Dispose();
        }
    }

    private void StartThumbnailLoading(IReadOnlyList<FileExplorerItemViewModel> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        var thumbnailCancellation = new CancellationTokenSource();
        _thumbnailCancellation = thumbnailCancellation;
        _ = LoadThumbnailsAsync(files, thumbnailCancellation);
    }

    private async Task LoadThumbnailsAsync(
        IReadOnlyList<FileExplorerItemViewModel> files,
        CancellationTokenSource thumbnailCancellation)
    {
        try
        {
            foreach (var file in files)
            {
                thumbnailCancellation.Token.ThrowIfCancellationRequested();

                var thumbnail = await _fileThumbnailService.LoadThumbnailAsync(
                    file.File,
                    CalculateThumbnailRequestSize(file.CardWidth),
                    thumbnailCancellation.Token);
                thumbnailCancellation.Token.ThrowIfCancellationRequested();

                if (thumbnail is not null)
                {
                    file.ThumbnailSource = thumbnail;
                }
            }
        }
        catch (OperationCanceledException) when (thumbnailCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (_thumbnailCancellation == thumbnailCancellation)
            {
                _thumbnailCancellation = null;
            }

            thumbnailCancellation.Dispose();
        }
    }

    private void CancelPendingThumbnailLoading()
    {
        var thumbnailCancellation = _thumbnailCancellation;
        if (thumbnailCancellation is null)
        {
            return;
        }

        _thumbnailCancellation = null;
        thumbnailCancellation.Cancel();
    }

    private void OnComputedStateChanged()
    {
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(CanSearch));
        OnPropertyChanged(nameof(CanNavigateBack));
        OnPropertyChanged(nameof(CanNavigateForward));
        OnPropertyChanged(nameof(CanNavigateToParent));
        OnPropertyChanged(nameof(CanUseFolderCommands));
        OnPropertyChanged(nameof(CanUseSelectedFileCommands));
        OnPropertyChanged(nameof(CanSort));
        OnPropertyChanged(nameof(BreadcrumbItems));
        OnPropertyChanged(nameof(SearchPlaceholderText));
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(BusyVisibility));
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(ErrorVisibility));
        OnPropertyChanged(nameof(FileGridVisibility));
        OnPropertyChanged(nameof(NoLocationVisibility));
    }

    private async Task RefreshAndSelectAsync(
        IReadOnlyList<string> selectedPaths,
        CancellationToken cancellationToken)
    {
        await LoadCurrentDirectoryAsync(cancellationToken);
        SelectFilesByPaths(selectedPaths);
    }

    private async Task ApplySortOptionsAsync(
        FileSortOptions sortOptions,
        CancellationToken cancellationToken)
    {
        if (_sortOptions == sortOptions)
        {
            return;
        }

        var selectedPaths = GetSelectedFilesOrFocusedFile()
            .Select(file => file.Path)
            .ToList();

        _sortOptions = sortOptions;
        OnPropertyChanged(nameof(SortField));
        OnPropertyChanged(nameof(SortDirection));

        await LoadCurrentDirectoryAsync(cancellationToken);
        SelectFilesByPaths(selectedPaths);
    }

    private void SelectFilesByPaths(IReadOnlyList<string> paths)
    {
        var normalizedPaths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizeDirectoryPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (normalizedPaths.Count == 0)
        {
            return;
        }

        ClearSelectedFiles();
        _selectionAnchor = null;

        foreach (var file in Files)
        {
            if (!normalizedPaths.Contains(NormalizeDirectoryPath(file.Path)))
            {
                continue;
            }

            file.IsSelected = true;
            _selectionAnchor ??= file;
            SelectedFile = file;
        }
    }

    private void ClearSearchQuery()
    {
        SearchQuery = string.Empty;
    }

    private void ClearSelection()
    {
        ClearSelectedFiles();
        SelectedFile = null;
        _selectionAnchor = null;
    }

    private void ClearSelectedFiles()
    {
        foreach (var file in Files)
        {
            file.IsSelected = false;
        }
    }

    private static double CalculateCardHeight(double cardWidth)
    {
        return Math.Round((cardWidth * 9 / 16) + InfoPanelHeight);
    }

    private static double CalculateThumbnailIconFontSize(double cardWidth)
    {
        return Math.Clamp(Math.Round(cardWidth * 0.22), 40, 78);
    }

    private static int CalculateThumbnailRequestSize(double cardWidth)
    {
        return Math.Clamp((int)Math.Ceiling(cardWidth * 2), 128, 1024);
    }

    private static string NormalizeDirectoryPath(string directoryPath)
    {
        return Path.GetFullPath(directoryPath.Trim());
    }

    private static IReadOnlyList<FileExplorerBreadcrumbItemViewModel> BuildBreadcrumbItems(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return [new FileExplorerBreadcrumbItemViewModel("No location selected", string.Empty)];
        }

        var normalizedPath = TryNormalizePath(directoryPath);
        var root = Path.GetPathRoot(normalizedPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            return [new FileExplorerBreadcrumbItemViewModel(normalizedPath, normalizedPath)];
        }

        var items = new List<FileExplorerBreadcrumbItemViewModel>
        {
            new("This PC", string.Empty),
            new(
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                root),
        };
        var remainder = normalizedPath[root.Length..]
            .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!string.IsNullOrWhiteSpace(remainder))
        {
            var currentPath = root;
            foreach (var segment in remainder.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries))
            {
                currentPath = Path.Combine(currentPath, segment);
                items.Add(new FileExplorerBreadcrumbItemViewModel(segment, currentPath));
            }
        }

        return items;
    }

    private static string GetCurrentFolderName(string directoryPath)
    {
        var normalizedPath = TryNormalizePath(directoryPath);
        var trimmedPath = normalizedPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var folderName = Path.GetFileName(trimmedPath);

        if (!string.IsNullOrWhiteSpace(folderName))
        {
            return folderName;
        }

        var root = Path.GetPathRoot(normalizedPath)?
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return string.IsNullOrWhiteSpace(root) ? "current folder" : root;
    }

    private static string TryNormalizePath(string directoryPath)
    {
        try
        {
            return NormalizeDirectoryPath(directoryPath);
        }
        catch (Exception)
        {
            return directoryPath.Trim();
        }
    }

    private static bool IsSameDirectory(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            NormalizeDirectoryPath(left),
            NormalizeDirectoryPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetParentDirectoryPath(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return null;
        }

        try
        {
            return Directory.GetParent(directoryPath)?.FullName;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string PopLast(List<string> paths)
    {
        var lastIndex = paths.Count - 1;
        var path = paths[lastIndex];
        paths.RemoveAt(lastIndex);

        return path;
    }
}

public sealed record FileExplorerBreadcrumbItemViewModel(string Name, string Path);

public sealed class FileExplorerItemViewModel : ObservableObject
{
    private const double DefaultCardOpacity = 1;
    private const double CutCardOpacity = 0.45;

    private double _cardHeight;
    private double _cardWidth;
    private bool _isFocused;
    private bool _isCut;
    private bool _isRenaming;
    private bool _isSelected;
    private string _renameText = string.Empty;
    private ImageSource? _thumbnailSource;
    private double _thumbnailIconFontSize;

    public FileExplorerItemViewModel(
        FileItem file,
        double cardWidth,
        double cardHeight,
        double thumbnailIconFontSize)
    {
        File = file;
        _cardWidth = cardWidth;
        _cardHeight = cardHeight;
        _renameText = Name;
        _thumbnailIconFontSize = thumbnailIconFontSize;
    }

    public FileItem File { get; }

    public double CardHeight
    {
        get => _cardHeight;
        private set => SetProperty(ref _cardHeight, value);
    }

    public double CardWidth
    {
        get => _cardWidth;
        private set => SetProperty(ref _cardWidth, value);
    }

    public double ThumbnailIconFontSize
    {
        get => _thumbnailIconFontSize;
        private set => SetProperty(ref _thumbnailIconFontSize, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(SelectionVisibility));
                OnPropertyChanged(nameof(FocusVisibility));
            }
        }
    }

    public bool IsFocused
    {
        get => _isFocused;
        set
        {
            if (SetProperty(ref _isFocused, value))
            {
                OnPropertyChanged(nameof(FocusVisibility));
            }
        }
    }

    public bool IsCut
    {
        get => _isCut;
        set
        {
            if (SetProperty(ref _isCut, value))
            {
                OnPropertyChanged(nameof(CardOpacity));
            }
        }
    }

    public bool IsRenaming
    {
        get => _isRenaming;
        private set
        {
            if (SetProperty(ref _isRenaming, value))
            {
                OnPropertyChanged(nameof(DisplayInfoVisibility));
                OnPropertyChanged(nameof(RenameEditorVisibility));
            }
        }
    }

    public string RenameText
    {
        get => _renameText;
        set => SetProperty(ref _renameText, value);
    }

    public ImageSource? ThumbnailSource
    {
        get => _thumbnailSource;
        set
        {
            if (SetProperty(ref _thumbnailSource, value))
            {
                OnPropertyChanged(nameof(ThumbnailVisibility));
                OnPropertyChanged(nameof(IconVisibility));
            }
        }
    }

    public Visibility SelectionVisibility => IsSelected
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility FocusVisibility => IsFocused && !IsSelected
        ? Visibility.Visible
        : Visibility.Collapsed;

    public double CardOpacity => IsCut
        ? CutCardOpacity
        : DefaultCardOpacity;

    public Visibility DisplayInfoVisibility => IsRenaming
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility RenameEditorVisibility => IsRenaming
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility ThumbnailVisibility => ThumbnailSource is null
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility IconVisibility => ThumbnailSource is null
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string Name => string.IsNullOrWhiteSpace(File.DisplayName)
        ? File.Name
        : File.DisplayName;

    public string FileSystemName => File.Name;

    public string Path => File.Path;

    public bool IsDirectory => File.IsDirectory;

    public bool CanLoadThumbnail => !IsDirectory && File.PreviewKind != FilePreviewKind.None;

    public IReadOnlyList<string> Tags => File.Tags;

    public Visibility TagVisibility => Tags.Count > 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string IconGlyph => IsDirectory
        ? "\uE8B7"
        : ResolveFileGlyph(File);

    public string DetailText => IsDirectory
        ? "Folder"
        : FormatSize(File.Size);

    public string SizeText => IsDirectory
        ? string.Empty
        : FormatSize(File.Size);

    public string ModifiedText => File.Modified.ToLocalTime().ToString(
        "yyyy-MM-dd HH:mm",
        CultureInfo.CurrentCulture);

    public void UpdateCardLayout(
        double cardWidth,
        double cardHeight,
        double thumbnailIconFontSize)
    {
        CardWidth = cardWidth;
        CardHeight = cardHeight;
        ThumbnailIconFontSize = thumbnailIconFontSize;
    }

    public void BeginRename()
    {
        RenameText = Name;
        IsRenaming = true;
    }

    public void CancelRename()
    {
        RenameText = Name;
        IsRenaming = false;
    }

    public void EndRename()
    {
        IsRenaming = false;
    }

    public string BuildFileSystemNameFromDisplayName(string displayName)
    {
        var trimmedDisplayName = displayName.Trim();
        if (trimmedDisplayName.Length == 0)
        {
            return string.Empty;
        }

        var currentDisplayName = Name;
        if (string.IsNullOrEmpty(currentDisplayName) ||
            !FileSystemName.EndsWith(currentDisplayName, StringComparison.Ordinal))
        {
            return trimmedDisplayName;
        }

        var tagPrefix = FileSystemName[..^currentDisplayName.Length];
        return tagPrefix + trimmedDisplayName;
    }

    private static string ResolveFileGlyph(FileItem file)
    {
        if (file.PreviewKind == FilePreviewKind.Image)
        {
            return "\uEB9F";
        }

        if (file.PreviewKind == FilePreviewKind.Video)
        {
            return "\uE8B2";
        }

        return System.IO.Path.GetExtension(file.Name).ToLowerInvariant() switch
        {
            ".flac" or ".m4a" or ".mp3" or ".wav" or ".wma" => "\uE8D6",
            ".doc" or ".docx" or ".md" or ".pdf" or ".rtf" or ".txt" => "\uE8A5",
            ".7z" or ".rar" or ".zip" => "\uF012",
            _ => "\uE8A5",
        };
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = (double)Math.Max(0, bytes);
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{size:0} {units[unitIndex]}"
            : $"{size:0.#} {units[unitIndex]}";
    }
}
