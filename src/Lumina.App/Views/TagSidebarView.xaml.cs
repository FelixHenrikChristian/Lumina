using Microsoft.UI.Xaml.Controls;

using Lumina.App.ViewModels;

namespace Lumina.App.Views;

public sealed partial class TagSidebarView : Page
{
    public TagSidebarView()
    {
        ViewModel = new TagSidebarViewModel();
        InitializeComponent();
    }

    public TagSidebarViewModel ViewModel { get; }
}
