using System.Collections.ObjectModel;
using System.Globalization;

using Microsoft.UI.Xaml;

using Lumina.Core.Models;
using Lumina.Core.Services;

namespace Lumina.App.ViewModels;

public sealed class FileExplorerViewModel : ObservableObject
{
    private static readonly double[] CardWidthZoomLevels = [176, 208, 240, 280, 320, 368];
    private const double InfoPanelHeight = 48;
    private const int DefaultZoomLevelIndex = 2;

    private readonly IFileBrowserService _fileBrowserService;
    private readonly List<string> _backStack = [];
    private readonly List<string> _forwardStack = [];

    private string _currentPath = string.Empty;
    private string _currentLocationName = string.Empty;
    private string? _errorMessage;
    private bool _isBusy;
    private double _cardHeight = CalculateCardHeight(CardWidthZoomLevels[DefaultZoomLevelIndex]);
    private double _cardWidth = CardWidthZoomLevels[DefaultZoomLevelIndex];
    private double _thumbnailIconFontSize = CalculateThumbnailIconFontSize(CardWidthZoomLevels[DefaultZoomLevelIndex]);
    private Location? _currentLocation;
    private CancellationTokenSource? _loadCancellation;
    private FileExplorerItemViewModel? _selectedFile;
    private FileExplorerItemViewModel? _selectionAnchor;
    private int _zoomLevelIndex = DefaultZoomLevelIndex;

    public FileExplorerViewModel()
        : this(new FileSystemBrowserService())
    {
    }

    public FileExplorerViewModel(IFileBrowserService fileBrowserService)
    {
        _fileBrowserService = fileBrowserService;
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

    public bool CanNavigateBack => _backStack.Count > 0;

    public bool CanNavigateForward => _forwardStack.Count > 0;

    public bool CanNavigateToParent => !string.IsNullOrWhiteSpace(GetParentDirectoryPath(CurrentPath));

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

    public async Task OpenLocationAsync(
        Location? location,
        CancellationToken cancellationToken = default)
    {
        _loadCancellation?.Cancel();

        _currentLocation = location;
        CurrentLocationName = location?.Name ?? string.Empty;
        CurrentPath = location?.Path ?? string.Empty;
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

    private async Task LoadCurrentDirectoryAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(CurrentPath))
        {
            return;
        }

        _loadCancellation?.Cancel();
        var loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loadCancellation = loadCancellation;

        IsBusy = true;
        ErrorMessage = null;
        ClearSelection();
        Files.Clear();
        OnComputedStateChanged();

        try
        {
            var files = await _fileBrowserService.LoadDirectoryAsync(
                CurrentPath,
                loadCancellation.Token);

            loadCancellation.Token.ThrowIfCancellationRequested();

            foreach (var file in files)
            {
                Files.Add(new FileExplorerItemViewModel(
                    file,
                    CardWidth,
                    CardHeight,
                    ThumbnailIconFontSize));
            }

            OnComputedStateChanged();
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

    private void OnComputedStateChanged()
    {
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(BusyVisibility));
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(ErrorVisibility));
        OnPropertyChanged(nameof(FileGridVisibility));
        OnPropertyChanged(nameof(NoLocationVisibility));
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

    private static string NormalizeDirectoryPath(string directoryPath)
    {
        return Path.GetFullPath(directoryPath.Trim());
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

public sealed class FileExplorerItemViewModel : ObservableObject
{
    private double _cardHeight;
    private double _cardWidth;
    private bool _isFocused;
    private bool _isSelected;
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

    public Visibility SelectionVisibility => IsSelected
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility FocusVisibility => IsFocused && !IsSelected
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string Name => string.IsNullOrWhiteSpace(File.DisplayName)
        ? File.Name
        : File.DisplayName;

    public string Path => File.Path;

    public bool IsDirectory => File.IsDirectory;

    public IReadOnlyList<string> Tags => File.Tags;

    public Visibility TagVisibility => Tags.Count > 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string IconGlyph => IsDirectory
        ? "\uE8B7"
        : ResolveFileGlyph(File.Name);

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

    private static string ResolveFileGlyph(string fileName)
    {
        return System.IO.Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".bmp" or ".gif" or ".heic" or ".jpeg" or ".jpg" or ".png" or ".webp" => "\uEB9F",
            ".avi" or ".mkv" or ".mov" or ".mp4" or ".webm" or ".wmv" => "\uE8B2",
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
