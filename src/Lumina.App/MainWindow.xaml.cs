using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Lumina.App.Views;

namespace Lumina.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        ShellNav.SelectedItem = LocationsNavItem;
        SidebarFrame.Navigate(typeof(LocationSidebarView));
    }

    private void ShellNav_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            SidebarFrame.Navigate(typeof(SettingsView));
            return;
        }

        if (args.SelectedItem is not NavigationViewItem item)
        {
            return;
        }

        switch (item.Tag?.ToString())
        {
            case "locations":
                SidebarFrame.Navigate(typeof(LocationSidebarView));
                break;
            case "tags":
                SidebarFrame.Navigate(typeof(TagSidebarView));
                break;
        }
    }
}
