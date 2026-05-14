using Microsoft.UI.Xaml.Controls;

using Lumina.App.ViewModels;

namespace Lumina.App.Views;

public sealed partial class LocationSidebarView : Page
{
    public LocationSidebarView()
    {
        ViewModel = new LocationSidebarViewModel();
        InitializeComponent();
    }

    public LocationSidebarViewModel ViewModel { get; }
}
