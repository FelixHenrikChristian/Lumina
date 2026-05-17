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

    private string _currentPath = string.Empty;
    private string _currentLocationName = string.Empty;
    private string? _errorMessage;
    private bool _isBusy;
    private double _cardHeight = CalculateCardHeight(CardWidthZoomLevels[DefaultZoomLevelIndex]);
    private double _cardWidth = CardWidthZoomLevels[DefaultZoomLevelIndex];
    private double _thumbnailIconFontSize = CalculateThumbnailIconFontSize(CardWidthZoomLevels[DefaultZoomLevelIndex]);
    private Location? _currentLocation;
    private CancellationTokenSource? _loadCancellation;
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

    public bool CanRefresh => !IsBusy && _currentLocation is not null;

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
        Files.Clear();
        ErrorMessage = null;
        OnComputedStateChanged();

        if (location is null)
        {
            return;
        }

        await LoadCurrentLocationAsync(cancellationToken);
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        return _currentLocation is null
            ? Task.CompletedTask
            : LoadCurrentLocationAsync(cancellationToken);
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

    private async Task LoadCurrentLocationAsync(CancellationToken cancellationToken)
    {
        if (_currentLocation is null)
        {
            return;
        }

        _loadCancellation?.Cancel();
        var loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loadCancellation = loadCancellation;

        IsBusy = true;
        ErrorMessage = null;
        Files.Clear();
        OnComputedStateChanged();

        try
        {
            var files = await _fileBrowserService.LoadDirectoryAsync(
                _currentLocation.Path,
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

    private static double CalculateCardHeight(double cardWidth)
    {
        return Math.Round((cardWidth * 9 / 16) + InfoPanelHeight);
    }

    private static double CalculateThumbnailIconFontSize(double cardWidth)
    {
        return Math.Clamp(Math.Round(cardWidth * 0.22), 40, 78);
    }
}

public sealed class FileExplorerItemViewModel : ObservableObject
{
    private double _cardHeight;
    private double _cardWidth;
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
