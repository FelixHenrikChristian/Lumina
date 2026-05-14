using Microsoft.UI.Xaml.Controls;

using Lumina.App.ViewModels;

namespace Lumina.App.Views;

public sealed partial class FileExplorerView : UserControl
{
    public FileExplorerView()
    {
        ViewModel = new FileExplorerViewModel();
        InitializeComponent();
    }

    public FileExplorerViewModel ViewModel { get; }
}
