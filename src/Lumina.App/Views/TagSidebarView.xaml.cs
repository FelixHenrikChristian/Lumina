using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using Windows.UI;
using WinRT.Interop;

using Lumina.App.Services;
using Lumina.App.ViewModels;
using Lumina.Core.Models;
using Lumina.Core.Services;

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

    private readonly JsonTagGroupTransferService _tagGroupTransferService = new();

    private bool _hasLoaded;
    private bool _isTagLibraryTransferBusy;

    public TagSidebarView()
    {
        ViewModel = new TagSidebarViewModel();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        InitializeComponent();
        DataContext = ViewModel;
        TagLibraryEvents.Imported += TagLibraryEvents_Imported;
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

    private async void TagLibraryEvents_Imported(object? sender, EventArgs e)
    {
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

    private async void ExportTagLibrary_Click(object sender, RoutedEventArgs e)
    {
        if (_isTagLibraryTransferBusy)
        {
            return;
        }

        var filePath = await PickExportPathAsync();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        await RunTagLibraryTransferAsync(
            async () =>
            {
                await _tagGroupTransferService.ExportAsync(filePath);
                await ShowMessageDialogAsync(
                    "Tag library exported",
                    $"Saved to {filePath}");
            },
            "Export failed");
    }

    private async void ImportTagLibrary_Click(object sender, RoutedEventArgs e)
    {
        if (_isTagLibraryTransferBusy)
        {
            return;
        }

        var filePath = await PickImportPathAsync();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        var shouldImport = await ConfirmTagLibraryImportAsync();
        if (!shouldImport)
        {
            return;
        }

        await RunTagLibraryTransferAsync(
            async () =>
            {
                var result = await _tagGroupTransferService.ImportAsync(filePath);
                TagLibraryEvents.RaiseImported();
                await ShowMessageDialogAsync(
                    "Tag library imported",
                    CreateImportSummary(result));
            },
            "Import failed");
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

    private async void ClearTags_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanClearTags)
        {
            return;
        }

        var shouldClear = await ConfirmAsync(
            "Clear tags",
            "Remove all tag groups and tags? Existing tags in file names will not be changed.",
            "Clear");
        if (!shouldClear)
        {
            return;
        }

        await ViewModel.ClearTagsAsync();
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

    private void TagChip_RightTapped(object sender, RightTappedRoutedEventArgs e)
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
        flyout.ShowAt(
            tagChip,
            new FlyoutShowOptions
            {
                Position = e.GetPosition(tagChip),
            });

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

        var colorBox = new ColorSelector("Default tag color", initialDefaultColor, "#2196f3");
        var textColorBox = new ColorSelector("Default text color", initialDefaultTextColor, "#ffffff");
        var preview = new TagPreview(
            nameBox,
            colorBox,
            textColorBox,
            "Tag Preview");

        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                preview.View,
                nameBox,
                descriptionBox,
                colorBox.View,
                textColorBox.View,
            },
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = CreateDialogContent(content),
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
            colorBox.SelectedColor,
            textColorBox.SelectedColor);
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

        var colorBox = new ColorSelector("Tag color", initialColor, initialColor);
        var textColorBox = new ColorSelector("Text color", initialTextColor, initialTextColor);
        var preview = new TagPreview(
            nameBox,
            colorBox,
            textColorBox,
            "Tag Preview");

        groupBox.SelectionChanged += (_, _) =>
        {
            if (initialName.Length > 0 || GetSelectedGroup(groupBox) is not { } selectedGroup)
            {
                return;
            }

            colorBox.SelectColor(selectedGroup.DefaultColor);
            textColorBox.SelectColor(selectedGroup.DefaultTextColor);
        };

        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                preview.View,
                nameBox,
                groupBox,
                colorBox.View,
                textColorBox.View,
            },
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = CreateDialogContent(content),
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
            colorBox.SelectedColor,
            textColorBox.SelectedColor);
    }

    private static ScrollViewer CreateDialogContent(FrameworkElement content)
    {
        content.Width = 456;

        return new ScrollViewer
        {
            MaxHeight = 640,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Auto,
            Content = content,
        };
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

    private async Task RunTagLibraryTransferAsync(Func<Task> transfer, string failureTitle)
    {
        _isTagLibraryTransferBusy = true;

        try
        {
            await transfer();
        }
        catch (Exception ex)
        {
            await ShowMessageDialogAsync(failureTitle, ex.Message);
        }
        finally
        {
            _isTagLibraryTransferBusy = false;
        }
    }

    private async Task<string?> PickExportPathAsync()
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = $"Lumina-tags-{DateTime.Now:yyyy-MM-dd}",
        };
        picker.FileTypeChoices.Add("JSON tag library", new List<string> { ".json" });
        InitializePicker(picker);

        var file = await picker.PickSaveFileAsync();

        return file?.Path;
    }

    private async Task<string?> PickImportPathAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add(".json");
        InitializePicker(picker);

        var file = await picker.PickSingleFileAsync();

        return file?.Path;
    }

    private static void InitializePicker(object picker)
    {
        if (App.MainWindow is null)
        {
            throw new InvalidOperationException("The main window must exist before opening a file picker.");
        }

        var windowHandle = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, windowHandle);
    }

    private Task<bool> ConfirmTagLibraryImportAsync()
    {
        return ConfirmAsync(
            "Import tag library",
            "Importing a tag library replaces the current tag groups and tags.",
            "Import");
    }

    private async Task ShowMessageDialogAsync(string title, string message)
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

    private static string CreateImportSummary(TagGroupImportResult result)
    {
        var parts = new List<string>
        {
            $"Source: {result.SourceFormat}",
            $"tags: {result.TagCount}",
        };

        parts.Add($"groups: {result.TagGroupCount}");

        return string.Join(", ", parts);
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

    private static bool TryParseHexColor(string? value, out Color color)
    {
        color = Colors.Transparent;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var hex = value.Trim();
        if (hex[0] == '#')
        {
            hex = hex[1..];
        }

        if (hex.Length is not (6 or 8) ||
            !hex.All(Uri.IsHexDigit))
        {
            return false;
        }

        var offset = hex.Length == 8 ? 2 : 0;
        var red = Convert.ToByte(hex.Substring(offset, 2), 16);
        var green = Convert.ToByte(hex.Substring(offset + 2, 2), 16);
        var blue = Convert.ToByte(hex.Substring(offset + 4, 2), 16);

        color = Color.FromArgb(byte.MaxValue, red, green, blue);

        return true;
    }

    private static string NormalizeColorText(string? color, string fallback)
    {
        return TryParseHexColor(color, out var parsed)
            ? ToHexColor(parsed)
            : fallback;
    }

    private static string ToHexColor(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static SolidColorBrush CreateColorBrush(string color, string fallback, byte? alpha = null)
    {
        if (!TryParseHexColor(color, out var parsed) &&
            !TryParseHexColor(fallback, out parsed))
        {
            return new SolidColorBrush(Colors.Transparent);
        }

        if (alpha is not null)
        {
            parsed.A = alpha.Value;
        }

        return new SolidColorBrush(parsed);
    }

    private static Border CreateSwatch(string color, double size, double cornerRadius)
    {
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(cornerRadius),
            Background = CreateColorBrush(color, "#2196F3"),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)),
            BorderThickness = new Thickness(1),
        };
    }

    private sealed class TagPreview
    {
        private readonly TextBox _nameBox;
        private readonly ColorSelector _colorSelector;
        private readonly ColorSelector _textColorSelector;
        private readonly string _fallbackName;
        private readonly Border _chip;
        private readonly TextBlock _nameText;
        private readonly Border _panel;
        private readonly Border _backgroundSwatch;
        private readonly Border _textSwatch;

        public TagPreview(
            TextBox nameBox,
            ColorSelector colorSelector,
            ColorSelector textColorSelector,
            string fallbackName)
        {
            _nameBox = nameBox;
            _colorSelector = colorSelector;
            _textColorSelector = textColorSelector;
            _fallbackName = fallbackName;

            _nameText = new TextBlock
            {
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };

            _chip = new Border
            {
                MinHeight = 38,
                MaxWidth = 320,
                Padding = new Thickness(14, 8, 14, 8),
                CornerRadius = new CornerRadius(7),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = _nameText,
            };

            _backgroundSwatch = CreateSwatch(_colorSelector.SelectedColor, 18, 4);
            _textSwatch = CreateSwatch(_textColorSelector.SelectedColor, 18, 4);

            var colorSummary = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 6,
                Children =
                {
                    _backgroundSwatch,
                    _textSwatch,
                },
            };

            var header = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
                Children =
                {
                    new TextBlock
                    {
                        Text = "Tag Preview",
                        FontSize = 13,
                        FontWeight = FontWeights.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                    colorSummary,
                },
            };
            Grid.SetColumn(colorSummary, 1);

            _panel = new Border
            {
                MinHeight = 116,
                Padding = new Thickness(16),
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Child = new Grid
                {
                    RowSpacing = 18,
                    RowDefinitions =
                    {
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                    },
                    Children =
                    {
                        header,
                        _chip,
                    },
                },
            };
            Grid.SetRow(_chip, 1);

            View = _panel;

            _nameBox.TextChanged += (_, _) => Update();
            _colorSelector.SelectedColorChanged += (_, _) => Update();
            _textColorSelector.SelectedColorChanged += (_, _) => Update();
            Update();
        }

        public FrameworkElement View { get; }

        private void Update()
        {
            var previewName = _nameBox.Text.Trim();
            _nameText.Text = previewName.Length == 0 ? _fallbackName : previewName;
            _chip.Background = CreateColorBrush(_colorSelector.SelectedColor, "#2196F3");
            _nameText.Foreground = CreateColorBrush(_textColorSelector.SelectedColor, "#FFFFFF");
            _panel.Background = CreateColorBrush(_colorSelector.SelectedColor, "#2196F3", 0x18);
            _panel.BorderBrush = CreateColorBrush(_colorSelector.SelectedColor, "#2196F3", 0x55);
            _backgroundSwatch.Background = CreateColorBrush(_colorSelector.SelectedColor, "#2196F3");
            _textSwatch.Background = CreateColorBrush(_textColorSelector.SelectedColor, "#FFFFFF");
        }
    }

    private sealed class ColorSelector
    {
        private const int PresetColumnCount = 9;

        private readonly string _fallbackColor;
        private readonly List<Button> _presetButtons = [];
        private readonly Button _pickerButton;
        private string _selectedColor;

        public ColorSelector(string header, string selectedColor, string fallbackColor)
        {
            _fallbackColor = NormalizeColorText(fallbackColor, "#2196F3");
            _selectedColor = _fallbackColor;

            var presetGrid = new Grid
            {
                ColumnSpacing = 6,
                RowSpacing = 6,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            for (var i = 0; i < PresetColumnCount; i++)
            {
                presetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }

            var rowCount = (int)Math.Ceiling((double)PresetColors.Length / PresetColumnCount);
            for (var i = 0; i < rowCount; i++)
            {
                presetGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            var colorIndex = 0;
            foreach (var color in PresetColors)
            {
                var normalized = NormalizeColorText(color.Hex, _fallbackColor);
                var colorButton = CreatePresetButton(color.Name, normalized);
                Grid.SetColumn(colorButton, colorIndex % PresetColumnCount);
                Grid.SetRow(colorButton, colorIndex / PresetColumnCount);
                presetGrid.Children.Add(colorButton);
                _presetButtons.Add(colorButton);
                colorIndex++;
            }

            _pickerButton = new Button
            {
                Width = 32,
                Height = 32,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Content = new FontIcon
                {
                    Glyph = "\uE790",
                    FontSize = 15,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                },
            };
            ToolTipService.SetToolTip(_pickerButton, "Custom color");
            _pickerButton.Click += PickerButton_Click;

            var paletteGrid = new Grid
            {
                ColumnSpacing = 10,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
                Children =
                {
                    presetGrid,
                    _pickerButton,
                },
            };
            Grid.SetColumn(_pickerButton, 1);

            View = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = header,
                        FontSize = 13,
                        FontWeight = FontWeights.SemiBold,
                    },
                    paletteGrid,
                },
            };

            SelectColor(selectedColor);
        }

        public FrameworkElement View { get; }

        public event EventHandler? SelectedColorChanged;

        public string SelectedColor => _selectedColor;

        public void SelectColor(string color)
        {
            var previous = _selectedColor;
            var normalized = NormalizeColorText(color, _fallbackColor);
            _selectedColor = normalized;
            UpdatePresetButtonStates();
            RaiseSelectedColorChanged(previous, normalized);
        }

        private Button CreatePresetButton(string name, string color)
        {
            var button = new Button
            {
                Width = 32,
                Height = 32,
                Padding = new Thickness(0),
                Tag = color,
                Content = CreateSwatch(color, 18, 4),
            };
            ToolTipService.SetToolTip(button, $"{name} {color}");
            button.Click += (_, _) => SelectColor(color);

            return button;
        }

        private void PickerButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedColor = TryParseHexColor(SelectedColor, out var parsedColor)
                ? parsedColor
                : Color.FromArgb(byte.MaxValue, 0x21, 0x96, 0xF3);

            var picker = new ColorPicker
            {
                Color = selectedColor,
                ColorSpectrumShape = ColorSpectrumShape.Box,
                IsMoreButtonVisible = true,
                IsColorSliderVisible = true,
                IsColorChannelTextInputVisible = true,
                IsHexInputVisible = true,
                IsAlphaEnabled = false,
                IsAlphaSliderVisible = false,
                IsAlphaTextInputVisible = false,
            };

            var valueText = new TextBlock
            {
                Text = ToHexColor(selectedColor),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var preview = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(selectedColor),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)),
                BorderThickness = new Thickness(1),
            };
            var doneButton = new Button
            {
                Content = "Done",
                MinWidth = 72,
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            var footer = new Grid
            {
                ColumnSpacing = 8,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
                Children =
                {
                    preview,
                    valueText,
                    doneButton,
                },
            };
            Grid.SetColumn(valueText, 1);
            Grid.SetColumn(doneButton, 2);

            var flyout = new Flyout
            {
                Placement = FlyoutPlacementMode.BottomEdgeAlignedRight,
                Content = new StackPanel
                {
                    Width = 328,
                    Spacing = 10,
                    Children =
                    {
                        picker,
                        footer,
                    },
                },
            };

            picker.ColorChanged += (_, args) =>
            {
                var hex = ToHexColor(args.NewColor);
                SelectColor(hex);
                valueText.Text = hex;
                preview.Background = new SolidColorBrush(args.NewColor);
            };
            doneButton.Click += (_, _) => flyout.Hide();

            flyout.ShowAt(_pickerButton);
        }

        private void UpdatePresetButtonStates()
        {
            var matchesPreset = false;
            foreach (var button in _presetButtons)
            {
                var isSelected = button.Tag is string color &&
                    string.Equals(color, _selectedColor, StringComparison.OrdinalIgnoreCase);

                matchesPreset |= isSelected;
                ApplySelectionVisual(button, isSelected);
            }

            ApplySelectionVisual(_pickerButton, !matchesPreset);
        }

        private static void ApplySelectionVisual(Button button, bool isSelected)
        {
            button.BorderBrush = new SolidColorBrush(isSelected
                ? Color.FromArgb(0xFF, 0x00, 0x78, 0xD4)
                : Color.FromArgb(0x00, 0, 0, 0));
            button.BorderThickness = new Thickness(isSelected ? 2 : 1);
        }

        private void RaiseSelectedColorChanged(string previousColor, string currentColor)
        {
            if (!string.Equals(previousColor, currentColor, StringComparison.OrdinalIgnoreCase))
            {
                SelectedColorChanged?.Invoke(this, EventArgs.Empty);
            }
        }
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
