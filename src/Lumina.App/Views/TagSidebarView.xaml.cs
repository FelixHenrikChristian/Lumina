using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using Lumina.App.ViewModels;

namespace Lumina.App.Views;

public sealed partial class TagSidebarView : Page
{
    private static readonly ColorChoice[] PresetColors =
    [
        new("Red", "#f44336"),
        new("Pink", "#e91e63"),
        new("Purple", "#9c27b0"),
        new("Indigo", "#3f51b5"),
        new("Blue", "#2196f3"),
        new("Cyan", "#00bcd4"),
        new("Teal", "#009688"),
        new("Green", "#4caf50"),
        new("Lime", "#8bc34a"),
        new("Yellow", "#ffeb3b"),
        new("Amber", "#ffc107"),
        new("Orange", "#ff9800"),
        new("Deep orange", "#ff5722"),
        new("Brown", "#795548"),
        new("Slate", "#607d8b"),
        new("Gray", "#9e9e9e"),
        new("White", "#ffffff"),
        new("Black", "#000000"),
    ];

    private bool _hasLoaded;

    public TagSidebarView()
    {
        ViewModel = new TagSidebarViewModel();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        InitializeComponent();
        DataContext = ViewModel;
    }

    public TagSidebarViewModel ViewModel { get; }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await ViewModel.LoadAsync();
    }

    private void GroupHeader_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: TagGroupItemViewModel group } header)
        {
            return;
        }

        var flyout = CreateGroupMenuFlyout(group);
        flyout.ShowAt(
            header,
            new FlyoutShowOptions
            {
                Position = e.GetPosition(header),
            });

        e.Handled = true;
    }

    private MenuFlyout CreateGroupMenuFlyout(TagGroupItemViewModel group)
    {
        var flyout = new MenuFlyout();
        var editItem = new MenuFlyoutItem
        {
            Icon = new FontIcon
            {
                FontSize = 14,
                Glyph = "\uE70F",
            },
            Text = "Edit group",
            Tag = group,
        };
        editItem.Click += EditGroup_Click;

        var deleteItem = new MenuFlyoutItem
        {
            Icon = new FontIcon
            {
                FontSize = 14,
                Glyph = "\uE74D",
            },
            Text = "Delete group",
            Tag = group,
        };
        deleteItem.Click += DeleteGroup_Click;

        flyout.Items.Add(editItem);
        flyout.Items.Add(deleteItem);

        return flyout;
    }

    private void GroupContent_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: TagGroupItemViewModel group } content ||
            IsInsideTagChip(e.OriginalSource))
        {
            return;
        }

        var flyout = CreateAddTagMenuFlyout(group);
        flyout.ShowAt(
            content,
            new FlyoutShowOptions
            {
                Position = e.GetPosition(content),
            });

        e.Handled = true;
    }

    private MenuFlyout CreateAddTagMenuFlyout(TagGroupItemViewModel group)
    {
        var flyout = new MenuFlyout();
        var addItem = new MenuFlyoutItem
        {
            Icon = new SymbolIcon(Symbol.Add),
            Text = "Add tag",
            Tag = group,
        };
        addItem.Click += AddTagToGroup_Click;
        flyout.Items.Add(addItem);

        return flyout;
    }

    private async void AddGroup_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanAddGroup)
        {
            return;
        }

        var result = await PromptForGroupAsync(
            "Add tag group",
            "Create",
            string.Empty,
            string.Empty,
            "#2196f3",
            "#ffffff");
        if (result is null)
        {
            return;
        }

        await ViewModel.AddGroupAsync(
            result.Name,
            result.Description,
            result.DefaultColor,
            result.DefaultTextColor);
    }

    private async void AddTagToGroup_Click(object sender, RoutedEventArgs e)
    {
        if (GetGroupFromSender(sender) is not { } group)
        {
            return;
        }

        var result = await PromptForTagAsync(
            "Add tag",
            "Create",
            initialGroupId: group.Id,
            initialName: string.Empty,
            initialColor: group.DefaultColor,
            initialTextColor: group.DefaultTextColor,
            allowGroupChange: false);
        if (result is null)
        {
            return;
        }

        await ViewModel.AddTagAsync(
            result.GroupId,
            result.Name,
            result.Color,
            result.TextColor);
    }

    private async void EditGroup_Click(object sender, RoutedEventArgs e)
    {
        if (GetGroupFromSender(sender) is not { } group)
        {
            return;
        }

        var result = await PromptForGroupAsync(
            "Edit tag group",
            "Save",
            group.Name,
            group.Description ?? string.Empty,
            group.DefaultColor,
            group.DefaultTextColor);
        if (result is null)
        {
            return;
        }

        await ViewModel.RenameGroupAsync(
            group.Id,
            result.Name,
            result.Description,
            result.DefaultColor,
            result.DefaultTextColor);
    }

    private async void DeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        if (GetGroupFromSender(sender) is not { } group)
        {
            return;
        }

        var shouldDelete = await ConfirmAsync(
            "Delete tag group",
            $"Remove \"{group.Name}\" and all tags in this group?",
            "Delete");
        if (!shouldDelete)
        {
            return;
        }

        await ViewModel.DeleteGroupAsync(group.Id);
    }

    private async void EditTag_Click(object sender, RoutedEventArgs e)
    {
        if (GetTagFromSender(sender) is not { } tag)
        {
            return;
        }

        await EditTagAsync(tag);
    }

    private async void DeleteTag_Click(object sender, RoutedEventArgs e)
    {
        if (GetTagFromSender(sender) is not { } tag)
        {
            return;
        }

        await DeleteTagAsync(tag);
    }

    private void TagChip_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TagItemViewModel tag } tagChip)
        {
            return;
        }

        var flyout = new MenuFlyout();

        var editItem = new MenuFlyoutItem
        {
            Text = "Edit tag",
            Tag = tag,
        };
        editItem.Click += EditTag_Click;

        var deleteItem = new MenuFlyoutItem
        {
            Text = "Delete tag",
            Tag = tag,
        };
        deleteItem.Click += DeleteTag_Click;

        flyout.Items.Add(editItem);
        flyout.Items.Add(deleteItem);
        flyout.ShowAt(tagChip);

        e.Handled = true;
    }

    private async Task EditTagAsync(TagItemViewModel tag)
    {
        var result = await PromptForTagAsync(
            "Edit tag",
            "Save",
            initialGroupId: tag.GroupId,
            initialName: tag.Name,
            initialColor: tag.Color,
            initialTextColor: tag.TextColor,
            allowGroupChange: true);
        if (result is null)
        {
            return;
        }

        await ViewModel.RenameTagAsync(
            tag.Id,
            result.GroupId,
            result.Name,
            result.Color,
            result.TextColor);
    }

    private async Task DeleteTagAsync(TagItemViewModel tag)
    {
        var shouldDelete = await ConfirmAsync(
            "Delete tag",
            $"Remove \"{tag.Name}\" from the tag library?",
            "Delete");
        if (!shouldDelete)
        {
            return;
        }

        await ViewModel.DeleteTagAsync(tag.Id);
    }

    private async Task<GroupDialogResult?> PromptForGroupAsync(
        string title,
        string primaryButtonText,
        string initialName,
        string initialDescription,
        string initialDefaultColor,
        string initialDefaultTextColor)
    {
        var nameBox = new TextBox
        {
            Header = "Group name",
            PlaceholderText = "Enter a group name",
            Text = initialName,
        };

        var descriptionBox = new TextBox
        {
            Header = "Description",
            PlaceholderText = "Optional description",
            Text = initialDescription,
            AcceptsReturn = true,
            Height = 76,
            TextWrapping = TextWrapping.Wrap,
        };

        var colorBox = CreateColorComboBox("Default tag color", initialDefaultColor);
        var textColorBox = CreateColorComboBox("Default text color", initialDefaultTextColor);

        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                nameBox,
                descriptionBox,
                colorBox,
                textColorBox,
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

        void UpdatePrimaryButtonState()
        {
            dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(nameBox.Text);
        }

        nameBox.TextChanged += (_, _) => UpdatePrimaryButtonState();
        dialog.Opened += (_, _) =>
        {
            UpdatePrimaryButtonState();
            nameBox.Focus(FocusState.Programmatic);
            nameBox.SelectAll();
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        return new GroupDialogResult(
            nameBox.Text.Trim(),
            descriptionBox.Text.Trim(),
            GetSelectedColor(colorBox, "#2196f3"),
            GetSelectedColor(textColorBox, "#ffffff"));
    }

    private async Task<TagDialogResult?> PromptForTagAsync(
        string title,
        string primaryButtonText,
        string? initialGroupId,
        string initialName,
        string initialColor,
        string initialTextColor,
        bool allowGroupChange)
    {
        var groups = ViewModel.GetGroupsSnapshot();
        if (groups.Count == 0)
        {
            return null;
        }

        var nameBox = new TextBox
        {
            Header = "Tag name",
            PlaceholderText = "Enter a tag name",
            Text = initialName,
        };

        var groupBox = CreateGroupComboBox(initialGroupId);
        groupBox.IsEnabled = allowGroupChange;

        var colorBox = CreateColorComboBox("Tag color", initialColor);
        var textColorBox = CreateColorComboBox("Text color", initialTextColor);

        groupBox.SelectionChanged += (_, _) =>
        {
            if (initialName.Length > 0 || GetSelectedGroup(groupBox) is not { } selectedGroup)
            {
                return;
            }

            SelectColor(colorBox, selectedGroup.DefaultColor);
            SelectColor(textColorBox, selectedGroup.DefaultTextColor);
        };

        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                nameBox,
                groupBox,
                colorBox,
                textColorBox,
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

        void UpdatePrimaryButtonState()
        {
            dialog.IsPrimaryButtonEnabled =
                !string.IsNullOrWhiteSpace(nameBox.Text) &&
                GetSelectedGroup(groupBox) is not null;
        }

        nameBox.TextChanged += (_, _) => UpdatePrimaryButtonState();
        groupBox.SelectionChanged += (_, _) => UpdatePrimaryButtonState();
        dialog.Opened += (_, _) =>
        {
            UpdatePrimaryButtonState();
            nameBox.Focus(FocusState.Programmatic);
            nameBox.SelectAll();
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary ||
            GetSelectedGroup(groupBox) is not { } group)
        {
            return null;
        }

        return new TagDialogResult(
            group.Id,
            nameBox.Text.Trim(),
            GetSelectedColor(colorBox, group.DefaultColor),
            GetSelectedColor(textColorBox, group.DefaultTextColor));
    }

    private async Task<bool> ConfirmAsync(
        string title,
        string content,
        string primaryButtonText)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        var result = await dialog.ShowAsync();

        return result == ContentDialogResult.Primary;
    }

    private ComboBox CreateGroupComboBox(string? selectedGroupId)
    {
        var comboBox = new ComboBox
        {
            Header = "Group",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        foreach (var group in ViewModel.GetGroupsSnapshot())
        {
            var choice = new GroupChoice(
                group.Id,
                group.Name,
                group.DefaultColor,
                group.DefaultTextColor ?? "#ffffff");
            var item = new ComboBoxItem
            {
                Content = group.Name,
                Tag = choice,
            };
            comboBox.Items.Add(item);

            if (group.Id == selectedGroupId)
            {
                comboBox.SelectedItem = item;
            }
        }

        comboBox.SelectedItem ??= comboBox.Items.FirstOrDefault();

        return comboBox;
    }

    private static ComboBox CreateColorComboBox(string header, string selectedColor)
    {
        var comboBox = new ComboBox
        {
            Header = header,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        foreach (var color in PresetColors)
        {
            comboBox.Items.Add(new ComboBoxItem
            {
                Content = $"{color.Name} ({color.Hex})",
                Tag = color.Hex,
            });
        }

        SelectColor(comboBox, selectedColor);

        return comboBox;
    }

    private static void SelectColor(ComboBox comboBox, string color)
    {
        var normalized = string.IsNullOrWhiteSpace(color) ? "#2196f3" : color.Trim();
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is string itemColor &&
                string.Equals(itemColor, normalized, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        var customItem = new ComboBoxItem
        {
            Content = $"Custom ({normalized})",
            Tag = normalized,
        };
        comboBox.Items.Add(customItem);
        comboBox.SelectedItem = customItem;
    }

    private static string GetSelectedColor(ComboBox comboBox, string fallback)
    {
        return comboBox.SelectedItem is ComboBoxItem { Tag: string color } &&
            !string.IsNullOrWhiteSpace(color)
            ? color
            : fallback;
    }

    private static GroupChoice? GetSelectedGroup(ComboBox comboBox)
    {
        return comboBox.SelectedItem is ComboBoxItem { Tag: GroupChoice group }
            ? group
            : null;
    }

    private static bool IsInsideTagChip(object originalSource)
    {
        var current = originalSource as DependencyObject;
        while (current is not null)
        {
            if (current is FrameworkElement { DataContext: TagItemViewModel })
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static TagGroupItemViewModel? GetGroupFromSender(object sender)
    {
        return sender switch
        {
            Button { CommandParameter: TagGroupItemViewModel group } => group,
            FrameworkElement { Tag: TagGroupItemViewModel group } => group,
            _ => null,
        };
    }

    private static TagItemViewModel? GetTagFromSender(object sender)
    {
        return sender switch
        {
            Button { CommandParameter: TagItemViewModel tag } => tag,
            MenuFlyoutItem { Tag: TagItemViewModel tag } => tag,
            FrameworkElement { DataContext: TagItemViewModel tag } => tag,
            _ => null,
        };
    }

    private sealed record ColorChoice(string Name, string Hex);

    private sealed record GroupChoice(
        string Id,
        string Name,
        string DefaultColor,
        string DefaultTextColor);

    private sealed record GroupDialogResult(
        string Name,
        string? Description,
        string DefaultColor,
        string DefaultTextColor);

    private sealed record TagDialogResult(
        string GroupId,
        string Name,
        string Color,
        string TextColor);
}
