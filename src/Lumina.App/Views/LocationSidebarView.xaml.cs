using System.Diagnostics;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using WinRT.Interop;

using Lumina.App.Services;
using Lumina.App.ViewModels;
using Lumina.Core.Models;

namespace Lumina.App.Views;

public sealed partial class LocationSidebarView : Page
{
    private bool _hasLoaded;
    private bool _isSynchronizingSelection;

    public LocationSidebarView()
    {
        ViewModel = new LocationSidebarViewModel();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        InitializeComponent();
        DataContext = ViewModel;
    }

    public LocationSidebarViewModel ViewModel { get; }

    private static string S(string key) => LocalizationService.Get(key);

    private static string F(string key, params object?[] args) => LocalizationService.Format(key, args);

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await ViewModel.LoadAsync();
        QueueSynchronizeSelectedLocation();
        LocationSelectionEvents.RaiseSelectionChanged(ViewModel.SelectedLocation);
    }

    private void RootGrid_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (IsWithinLocationCard(e.OriginalSource))
        {
            return;
        }

        var menuFlyout = (MenuFlyout)RootGrid.Resources["BlankAreaMenuFlyout"];
        menuFlyout.ShowAt(
            RootGrid,
            new FlyoutShowOptions
            {
                Position = e.GetPosition(RootGrid),
            });

        e.Handled = true;
    }

    private async void LocationsItemsView_SelectionChanged(ItemsView sender, ItemsViewSelectionChangedEventArgs args)
    {
        if (_isSynchronizingSelection)
        {
            return;
        }

        if (sender.SelectedItem is Location location)
        {
            await ViewModel.SelectLocationAsync(location);
            LocationSelectionEvents.RaiseSelectionChanged(ViewModel.SelectedLocation);
        }
    }

    private void LocationCard_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_isSynchronizingSelection || IsWithinLocationCommand(e.OriginalSource))
        {
            return;
        }

        if (GetLocationFromDataContext(sender) is not { } location ||
            ViewModel.SelectedLocation?.Id != location.Id)
        {
            return;
        }

        LocationSelectionEvents.RaiseSelectionChanged(location);
    }

    private async void AddLocation_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanAddLocation)
        {
            return;
        }

        var folderPath = await PickFolderPathAsync();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        var displayName = await PromptForDisplayNameAsync(
            S("AddLocation"),
            folderPath,
            LocationSidebarViewModel.GetDefaultLocationName(folderPath),
            S("Add"));
        if (displayName is null)
        {
            return;
        }

        await ViewModel.AddLocationAsync(folderPath, displayName);
        QueueSynchronizeSelectedLocation();
        LocationSelectionEvents.RaiseSelectionChanged(ViewModel.SelectedLocation);
    }

    private async void EditLocation_Click(object sender, RoutedEventArgs e)
    {
        if (GetLocationFromSender(sender) is not { } location)
        {
            return;
        }

        await ViewModel.SelectLocationAsync(location);

        var displayName = await PromptForDisplayNameAsync(
            S("EditLocation"),
            location.Path,
            location.Name,
            S("Save"));
        if (displayName is null)
        {
            return;
        }

        await ViewModel.RenameLocationAsync(location, displayName);
        QueueSynchronizeSelectedLocation();
        LocationSelectionEvents.RaiseSelectionChanged(ViewModel.SelectedLocation);
    }

    private async void DeleteLocation_Click(object sender, RoutedEventArgs e)
    {
        if (GetLocationFromSender(sender) is not { } location)
        {
            return;
        }

        await ViewModel.SelectLocationAsync(location);

        var shouldDelete = await ConfirmDeleteLocationAsync(location);
        if (!shouldDelete)
        {
            return;
        }

        await ViewModel.DeleteLocationAsync(location);
        QueueSynchronizeSelectedLocation();
        LocationSelectionEvents.RaiseSelectionChanged(ViewModel.SelectedLocation);
    }

    private async void ClearLocations_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanClearLocations)
        {
            return;
        }

        var shouldClear = await ConfirmClearLocationsAsync();
        if (!shouldClear)
        {
            return;
        }

        await ViewModel.ClearLocationsAsync();
        QueueSynchronizeSelectedLocation();
        LocationSelectionEvents.RaiseSelectionChanged(ViewModel.SelectedLocation);
    }

    private async void OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (GetLocationFromSender(sender) is not { } location)
        {
            return;
        }

        await ViewModel.SelectLocationAsync(location);
        LocationSelectionEvents.RaiseSelectionChanged(ViewModel.SelectedLocation);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = location.Path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync(S("OpenLocationFailed"), ex.Message);
        }
    }

    private static async Task<string?> PickFolderPathAsync()
    {
        if (App.MainWindow is null)
        {
            throw new InvalidOperationException("Main window is not available.");
        }

        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
        };
        picker.FileTypeFilter.Add("*");

        var windowHandle = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, windowHandle);

        var folder = await picker.PickSingleFolderAsync();

        return folder?.Path;
    }

    private async Task<string?> PromptForDisplayNameAsync(
        string title,
        string folderPath,
        string initialName,
        string primaryButtonText)
    {
        var nameBox = new TextBox
        {
            Header = S("LocationName"),
            PlaceholderText = S("EnterDisplayName"),
            Text = initialName,
        };

        var pathText = new TextBlock
        {
            Text = folderPath,
            TextWrapping = TextWrapping.WrapWholeWords,
        };

        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                nameBox,
                pathText,
            },
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = S("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            Content = content,
        };
        dialog.Opened += (_, _) =>
        {
            nameBox.Focus(FocusState.Programmatic);
            nameBox.SelectAll();
        };

        var result = await dialog.ShowAsync();

        return result == ContentDialogResult.Primary ? nameBox.Text : null;
    }

    private async Task<bool> ConfirmDeleteLocationAsync(Location location)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = S("DeleteLocation"),
            Content = F("DeleteLocationContent", location.Name),
            PrimaryButtonText = S("Delete"),
            CloseButtonText = S("Cancel"),
            DefaultButton = ContentDialogButton.Close,
        };

        var result = await dialog.ShowAsync();

        return result == ContentDialogResult.Primary;
    }

    private async Task<bool> ConfirmClearLocationsAsync()
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = S("ClearLocations"),
            Content = S("ClearLocationsContent"),
            PrimaryButtonText = S("Clear"),
            CloseButtonText = S("Cancel"),
            DefaultButton = ContentDialogButton.Close,
        };

        var result = await dialog.ShowAsync();

        return result == ContentDialogResult.Primary;
    }

    private async Task ShowErrorDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = S("OK"),
        };

        await dialog.ShowAsync();
    }

    private static Location? GetLocationFromSender(object sender)
    {
        if (sender is Button { CommandParameter: Location location })
        {
            return location;
        }

        return null;
    }

    private static Location? GetLocationFromDataContext(object sender)
    {
        return sender is FrameworkElement { DataContext: Location location }
            ? location
            : null;
    }

    private void QueueSynchronizeSelectedLocation()
    {
        DispatcherQueue.TryEnqueue(SynchronizeSelectedLocation);
    }

    private void SynchronizeSelectedLocation()
    {
        _isSynchronizingSelection = true;

        try
        {
            var selectedLocation = ViewModel.SelectedLocation;
            if (selectedLocation is null)
            {
                LocationsItemsView.DeselectAll();
                return;
            }

            var index = ViewModel.Locations.IndexOf(selectedLocation);
            if (index >= 0)
            {
                LocationsItemsView.Select(index);
            }
        }
        finally
        {
            _isSynchronizingSelection = false;
        }
    }

    private static bool IsWithinLocationCard(object? source)
    {
        var dependencyObject = source as DependencyObject;
        while (dependencyObject is not null)
        {
            if (dependencyObject is FrameworkElement { Name: "LocationCard" })
            {
                return true;
            }

            dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
        }

        return false;
    }

    private static bool IsWithinLocationCommand(object? source)
    {
        var dependencyObject = source as DependencyObject;
        while (dependencyObject is not null)
        {
            if (dependencyObject is Button)
            {
                return true;
            }

            if (dependencyObject is FrameworkElement { Name: "LocationCard" })
            {
                return false;
            }

            dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
        }

        return false;
    }
}
