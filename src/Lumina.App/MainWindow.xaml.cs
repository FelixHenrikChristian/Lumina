using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Lumina.App.Views;

namespace Lumina.App;

public sealed partial class MainWindow : Window
{
    private bool _isUpdatingSidebarSelection;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        SidebarFrame.Navigate(typeof(LocationSidebarView));
        LocationsSelectorItem.IsSelected = true;
        SidebarSelector.SelectionChanged += SidebarSelector_SelectionChanged;
        ShellNav.Loaded += (_, _) => UpdateSidebarPaneContentVisibility();
        ShellNav.SizeChanged += (_, _) => UpdateSidebarPaneContentVisibility();
        ShellNav.PaneOpening += (_, _) => SidebarPaneContent.Visibility = Visibility.Visible;
        ShellNav.PaneOpened += (_, _) => UpdateSidebarPaneContentVisibility();
        ShellNav.PaneClosing += (_, _) => SidebarPaneContent.Visibility = Visibility.Collapsed;
        ShellNav.PaneClosed += (_, _) => UpdateSidebarPaneContentVisibility();
    }

    private void UpdateSidebarPaneContentVisibility()
    {
        SidebarPaneContent.Visibility = ShellNav.IsPaneOpen
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ClearSidebarSelection()
    {
        _isUpdatingSidebarSelection = true;

        try
        {
            LocationsSelectorItem.IsSelected = false;
            TagsSelectorItem.IsSelected = false;
        }
        finally
        {
            _isUpdatingSidebarSelection = false;
        }
    }

    private void SidebarSelector_SelectionChanged(
        SelectorBar sender,
        SelectorBarSelectionChangedEventArgs args)
    {
        if (_isUpdatingSidebarSelection)
        {
            return;
        }

        ShellNav.SelectedItem = null;

        if (sender.SelectedItem == LocationsSelectorItem)
        {
            SidebarFrame.Navigate(typeof(LocationSidebarView));
            return;
        }

        if (sender.SelectedItem == TagsSelectorItem)
        {
            SidebarFrame.Navigate(typeof(TagSidebarView));
        }
    }

    private void ShellNav_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ClearSidebarSelection();
            SidebarFrame.Navigate(typeof(SettingsView));
        }
    }
}
