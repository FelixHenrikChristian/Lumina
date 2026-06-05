using Microsoft.UI.Xaml.Controls;

using Lumina.App.Services;
using Lumina.Core.Models;
using Lumina.Core.Services;

namespace Lumina.App.Views;

public sealed partial class SettingsView : Page
{
    private readonly IDisplaySettingsStore _displaySettingsStore = new JsonDisplaySettingsStore();
    private DisplaySettings _displaySettings = new();
    private bool _isLoadingSettings;

    public SettingsView()
    {
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _isLoadingSettings = true;
        try
        {
            _displaySettings = await _displaySettingsStore.LoadAsync();
            SelectLanguage(_displaySettings.Language);
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private async void LanguageComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isLoadingSettings ||
            LanguageComboBox.SelectedItem is not ComboBoxItem { Tag: string selectedLanguage })
        {
            return;
        }

        var language = DisplayLanguage.Normalize(selectedLanguage);
        if (language == _displaySettings.Language)
        {
            return;
        }

        _displaySettings = _displaySettings with
        {
            Language = language,
        };

        await _displaySettingsStore.SaveAsync(_displaySettings);
        LocalizationService.ApplyLanguage(language);
    }

    private void SelectLanguage(string? language)
    {
        var normalizedLanguage = DisplayLanguage.Normalize(language);
        LanguageComboBox.SelectedItem = normalizedLanguage switch
        {
            DisplayLanguage.English => EnglishLanguageItem,
            DisplayLanguage.SimplifiedChinese => SimplifiedChineseLanguageItem,
            _ => SystemLanguageItem,
        };
    }
}
