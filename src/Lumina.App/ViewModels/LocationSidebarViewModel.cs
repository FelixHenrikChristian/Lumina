using System.Collections.ObjectModel;
using System.Collections.Specialized;

using Microsoft.UI.Xaml;

using Lumina.Core.Models;
using Lumina.Core.Services;

namespace Lumina.App.ViewModels;

public sealed class LocationSidebarViewModel : ObservableObject
{
    private readonly IAppStateStore _appStateStore;
    private readonly ILocationStore _locationStore;

    private AppState _appState = new();
    private string? _errorMessage;
    private bool _isBusy;
    private Location? _selectedLocation;

    public LocationSidebarViewModel()
        : this(new JsonLocationStore(), new JsonAppStateStore())
    {
    }

    public LocationSidebarViewModel(
        ILocationStore locationStore,
        IAppStateStore appStateStore)
    {
        _locationStore = locationStore;
        _appStateStore = appStateStore;
        Locations.CollectionChanged += Locations_CollectionChanged;
    }

    public ObservableCollection<Location> Locations { get; } = [];

    public Location? SelectedLocation
    {
        get => _selectedLocation;
        set => SetProperty(ref _selectedLocation, value);
    }

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

    public bool CanAddLocation => !IsBusy;

    public bool CanClearLocations => !IsBusy && HasLocations;

    public bool HasLocations => Locations.Count > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyStateVisibility =>
        !IsBusy && !HasError && !HasLocations ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ErrorVisibility => HasError ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LocationListVisibility =>
        !IsBusy && HasLocations ? Visibility.Visible : Visibility.Collapsed;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var savedLocations = await _locationStore.LoadAsync(cancellationToken);
            _appState = await _appStateStore.LoadAsync(cancellationToken);

            Locations.Clear();
            foreach (var location in savedLocations)
            {
                Locations.Add(location);
            }

            SelectedLocation = ResolveSelectedLocation();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load locations: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AddLocationAsync(
        string folderPath,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        ErrorMessage = null;

        var location = new Location
        {
            Name = NormalizeDisplayName(folderPath, displayName),
            Path = folderPath.Trim(),
        };

        Locations.Add(location);
        SelectedLocation = location;

        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Locations.Remove(location);
            SelectedLocation = Locations.FirstOrDefault();
            ErrorMessage = $"Failed to save location: {ex.Message}";
            return;
        }

        await SaveSelectedLocationIdAsync(location.Id, cancellationToken);
    }

    public async Task SelectLocationAsync(
        Location location,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);

        if (!Locations.Contains(location))
        {
            return;
        }

        SelectedLocation = location;
        await SaveSelectedLocationIdAsync(location.Id, cancellationToken);
    }

    public async Task RenameLocationAsync(
        Location location,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);

        var index = IndexOfLocation(location);
        if (index < 0)
        {
            return;
        }

        ErrorMessage = null;

        var originalLocation = Locations[index];
        var updatedLocation = originalLocation with
        {
            Name = NormalizeDisplayName(originalLocation.Path, displayName),
        };

        Locations[index] = updatedLocation;
        if (SelectedLocation?.Id == originalLocation.Id)
        {
            SelectedLocation = updatedLocation;
        }

        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Locations[index] = originalLocation;
            if (SelectedLocation?.Id == originalLocation.Id)
            {
                SelectedLocation = originalLocation;
            }

            ErrorMessage = $"Failed to save location: {ex.Message}";
        }
    }

    public async Task DeleteLocationAsync(
        Location location,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);

        var index = IndexOfLocation(location);
        if (index < 0)
        {
            return;
        }

        ErrorMessage = null;

        var originalSelection = SelectedLocation;
        var removedLocation = Locations[index];
        Locations.RemoveAt(index);

        if (originalSelection?.Id == removedLocation.Id)
        {
            SelectedLocation = Locations.Count == 0
                ? null
                : Locations[Math.Min(index, Locations.Count - 1)];
        }

        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Locations.Insert(index, removedLocation);
            SelectedLocation = originalSelection;
            ErrorMessage = $"Failed to delete location: {ex.Message}";
            return;
        }

        await SaveSelectedLocationIdAsync(SelectedLocation?.Id, cancellationToken);
    }

    public async Task ClearLocationsAsync(CancellationToken cancellationToken = default)
    {
        if (Locations.Count == 0)
        {
            return;
        }

        ErrorMessage = null;

        var originalLocations = Locations.ToList();
        var originalSelection = SelectedLocation;

        Locations.Clear();
        SelectedLocation = null;

        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            foreach (var location in originalLocations)
            {
                Locations.Add(location);
            }

            SelectedLocation = originalSelection;
            ErrorMessage = $"Failed to clear locations: {ex.Message}";
            return;
        }

        await SaveSelectedLocationIdAsync(null, cancellationToken);
    }

    public static string GetDefaultLocationName(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        var trimmedPath = folderPath.Trim();
        var directoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(trimmedPath));

        return string.IsNullOrWhiteSpace(directoryName)
            ? trimmedPath
            : directoryName;
    }

    private Task SaveAsync(CancellationToken cancellationToken)
    {
        return _locationStore.SaveAsync(Locations.ToList(), cancellationToken);
    }

    private int IndexOfLocation(Location location)
    {
        for (var i = 0; i < Locations.Count; i++)
        {
            if (Locations[i].Id == location.Id)
            {
                return i;
            }
        }

        return -1;
    }

    private Location? ResolveSelectedLocation()
    {
        if (!string.IsNullOrWhiteSpace(_appState.SelectedLocationId))
        {
            var savedSelection = Locations.FirstOrDefault(
                location => location.Id == _appState.SelectedLocationId);
            if (savedSelection is not null)
            {
                return savedSelection;
            }
        }

        return Locations.FirstOrDefault();
    }

    private async Task SaveSelectedLocationIdAsync(
        string? selectedLocationId,
        CancellationToken cancellationToken)
    {
        var previousAppState = _appState;
        _appState = _appState with
        {
            SelectedLocationId = selectedLocationId,
        };

        try
        {
            await _appStateStore.SaveAsync(_appState, cancellationToken);
        }
        catch (Exception ex)
        {
            _appState = previousAppState;
            ErrorMessage = $"Failed to save selected location: {ex.Message}";
        }
    }

    private static string NormalizeDisplayName(string folderPath, string? displayName)
    {
        var trimmedName = displayName?.Trim();

        return string.IsNullOrWhiteSpace(trimmedName)
            ? GetDefaultLocationName(folderPath)
            : trimmedName;
    }

    private void Locations_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnComputedStateChanged();
    }

    private void OnComputedStateChanged()
    {
        OnPropertyChanged(nameof(CanAddLocation));
        OnPropertyChanged(nameof(CanClearLocations));
        OnPropertyChanged(nameof(HasLocations));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(BusyVisibility));
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(ErrorVisibility));
        OnPropertyChanged(nameof(LocationListVisibility));
    }
}
