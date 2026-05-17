using Lumina.Core.Models;

namespace Lumina.App.Services;

public static class LocationSelectionEvents
{
    public static event EventHandler<LocationSelectionChangedEventArgs>? SelectionChanged;

    public static Location? CurrentLocation { get; private set; }

    public static void RaiseSelectionChanged(Location? location)
    {
        CurrentLocation = location;
        SelectionChanged?.Invoke(null, new LocationSelectionChangedEventArgs(location));
    }
}

public sealed class LocationSelectionChangedEventArgs : EventArgs
{
    public LocationSelectionChangedEventArgs(Location? location)
    {
        Location = location;
    }

    public Location? Location { get; }
}
