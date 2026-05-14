using System.Collections.ObjectModel;

using Lumina.Core.Models;

namespace Lumina.App.ViewModels;

public sealed class TagSidebarViewModel : ObservableObject
{
    public ObservableCollection<TagGroup> TagGroups { get; } = [];
}
