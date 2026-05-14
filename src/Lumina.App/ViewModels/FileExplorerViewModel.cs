using System.Collections.ObjectModel;

using Lumina.Core.Models;

namespace Lumina.App.ViewModels;

public sealed class FileExplorerViewModel : ObservableObject
{
    private string _currentPath = string.Empty;

    public ObservableCollection<FileItem> Files { get; } = [];

    public string CurrentPath
    {
        get => _currentPath;
        set => SetProperty(ref _currentPath, value);
    }

    public double CardWidth { get; set; } = 176;
}
