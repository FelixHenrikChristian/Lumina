using System.Diagnostics;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using WinRT.Interop;

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

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await ViewModel.LoadAsync();
        QueueSynchronizeSelectedLocation();
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
        }
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
            "Add location",
            folderPath,
            LocationSidebarViewModel.GetDefaultLocationName(folderPath),
            "Add");
        if (displayName is null)
        {
            return;
        }

        await ViewModel.AddLocationAsync(folderPath, displayName);
        QueueSynchronizeSelectedLocation();
    }

    private async void EditLocation_Click(object sender, RoutedEventArgs e)
    {
        if (GetLocationFromSender(sender) is not { } location)
        {
            return;
        }

        await ViewModel.SelectLocationAsync(location);

        var displayName = await PromptForDisplayNameAsync(
            "Edit location",
            location.Path,
            location.Name,
            "Save");
        if (displayName is null)
        {
            return;
        }

        await ViewModel.RenameLocationAsync(location, displayName);
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
    }

    private async void OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (GetLocationFromSender(sender) is not { } location)
        {
            return;
        }

        await ViewModel.SelectLocationAsync(location);

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
            await ShowErrorDialogAsync("Open location failed", ex.Message);
        }
    }

    private static async Task<string?> PickFolderPathAsync()
    {
        if (App.MainWindow is null)
        {
            throw new InvalidOperationException("The main window must exist before opening a folder picker.");
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
            Header = "Location name",
            PlaceholderText = "Enter a display name",
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
            CloseButtonText = "Cancel",
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
            Title = "Delete location",
            Content = $"Remove \"{location.Name}\" from Lumina? Files on disk will not be deleted.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
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
            CloseButtonText = "OK",
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
}
