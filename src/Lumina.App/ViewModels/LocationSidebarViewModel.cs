using System.Collections.ObjectModel;

using Lumina.Core.Models;

namespace Lumina.App.ViewModels;

public sealed class LocationSidebarViewModel : ObservableObject
{
    public ObservableCollection<Location> Locations { get; } = [];
}
