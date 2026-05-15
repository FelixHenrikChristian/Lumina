using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Windows.Foundation;

using Lumina.App.Views;

namespace Lumina.App;

public sealed partial class MainWindow : Window
{
    private const double SidebarSettingsItemReservedHeight = 56;

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
        SidebarSelector.SizeChanged += (_, _) => UpdateSidebarPaneContentLayout();
        ShellNav.Loaded += (_, _) => UpdateSidebarPaneContentLayout();
        ShellNav.SizeChanged += (_, _) => UpdateSidebarPaneContentLayout();
        ShellNav.PaneOpening += (_, _) => SidebarPaneContent.Visibility = Visibility.Visible;
        ShellNav.PaneOpened += (_, _) => UpdateSidebarPaneContentLayout();
        ShellNav.PaneClosing += (_, _) => SidebarPaneContent.Visibility = Visibility.Collapsed;
        ShellNav.PaneClosed += (_, _) => UpdateSidebarPaneContentLayout();
    }

    private void AppTitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        ShellNav.IsPaneOpen = !ShellNav.IsPaneOpen;
    }

    private void UpdateSidebarPaneContentLayout()
    {
        SidebarPaneContent.Visibility = ShellNav.IsPaneOpen
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (SidebarFrame.XamlRoot is null || ShellNav.ActualHeight <= 0)
        {
            SidebarFrame.ClearValue(FrameworkElement.MaxHeightProperty);
            return;
        }

        var frameTop = SidebarFrame
            .TransformToVisual(ShellNav)
            .TransformPoint(new Point(0, 0))
            .Y;
        var reservedFooterHeight = ShellNav.IsSettingsVisible
            ? SidebarSettingsItemReservedHeight
            : 0;

        SidebarFrame.MaxHeight = Math.Max(
            0,
            ShellNav.ActualHeight - frameTop - reservedFooterHeight);
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
