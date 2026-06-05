using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Lumina.App.Services;

public static class Localization
{
    public static readonly DependencyProperty TextKeyProperty = DependencyProperty.RegisterAttached(
        "TextKey",
        typeof(string),
        typeof(Localization),
        new PropertyMetadata(null, OnLocalizationKeyChanged));

    public static readonly DependencyProperty ContentKeyProperty = DependencyProperty.RegisterAttached(
        "ContentKey",
        typeof(string),
        typeof(Localization),
        new PropertyMetadata(null, OnLocalizationKeyChanged));

    public static readonly DependencyProperty HeaderKeyProperty = DependencyProperty.RegisterAttached(
        "HeaderKey",
        typeof(string),
        typeof(Localization),
        new PropertyMetadata(null, OnLocalizationKeyChanged));

    public static readonly DependencyProperty PlaceholderTextKeyProperty = DependencyProperty.RegisterAttached(
        "PlaceholderTextKey",
        typeof(string),
        typeof(Localization),
        new PropertyMetadata(null, OnLocalizationKeyChanged));

    public static readonly DependencyProperty TitleKeyProperty = DependencyProperty.RegisterAttached(
        "TitleKey",
        typeof(string),
        typeof(Localization),
        new PropertyMetadata(null, OnLocalizationKeyChanged));

    public static readonly DependencyProperty ToolTipKeyProperty = DependencyProperty.RegisterAttached(
        "ToolTipKey",
        typeof(string),
        typeof(Localization),
        new PropertyMetadata(null, OnLocalizationKeyChanged));

    public static readonly DependencyProperty AutomationNameKeyProperty = DependencyProperty.RegisterAttached(
        "AutomationNameKey",
        typeof(string),
        typeof(Localization),
        new PropertyMetadata(null, OnLocalizationKeyChanged));

    private static readonly List<WeakReference<DependencyObject>> Targets = [];

    static Localization()
    {
        LocalizationService.LanguageChanged += (_, _) => RefreshTrackedTargets();
    }

    public static string? GetTextKey(DependencyObject target)
    {
        return (string?)target.GetValue(TextKeyProperty);
    }

    public static void SetTextKey(DependencyObject target, string? value)
    {
        target.SetValue(TextKeyProperty, value);
    }

    public static string? GetContentKey(DependencyObject target)
    {
        return (string?)target.GetValue(ContentKeyProperty);
    }

    public static void SetContentKey(DependencyObject target, string? value)
    {
        target.SetValue(ContentKeyProperty, value);
    }

    public static string? GetHeaderKey(DependencyObject target)
    {
        return (string?)target.GetValue(HeaderKeyProperty);
    }

    public static void SetHeaderKey(DependencyObject target, string? value)
    {
        target.SetValue(HeaderKeyProperty, value);
    }

    public static string? GetPlaceholderTextKey(DependencyObject target)
    {
        return (string?)target.GetValue(PlaceholderTextKeyProperty);
    }

    public static void SetPlaceholderTextKey(DependencyObject target, string? value)
    {
        target.SetValue(PlaceholderTextKeyProperty, value);
    }

    public static string? GetTitleKey(DependencyObject target)
    {
        return (string?)target.GetValue(TitleKeyProperty);
    }

    public static void SetTitleKey(DependencyObject target, string? value)
    {
        target.SetValue(TitleKeyProperty, value);
    }

    public static string? GetToolTipKey(DependencyObject target)
    {
        return (string?)target.GetValue(ToolTipKeyProperty);
    }

    public static void SetToolTipKey(DependencyObject target, string? value)
    {
        target.SetValue(ToolTipKeyProperty, value);
    }

    public static string? GetAutomationNameKey(DependencyObject target)
    {
        return (string?)target.GetValue(AutomationNameKeyProperty);
    }

    public static void SetAutomationNameKey(DependencyObject target, string? value)
    {
        target.SetValue(AutomationNameKeyProperty, value);
    }

    private static void OnLocalizationKeyChanged(
        DependencyObject target,
        DependencyPropertyChangedEventArgs args)
    {
        Track(target);
        UpdateTarget(target);
    }

    private static void Track(DependencyObject target)
    {
        for (var index = Targets.Count - 1; index >= 0; index--)
        {
            if (!Targets[index].TryGetTarget(out var existing))
            {
                Targets.RemoveAt(index);
                continue;
            }

            if (ReferenceEquals(existing, target))
            {
                return;
            }
        }

        Targets.Add(new WeakReference<DependencyObject>(target));
    }

    private static void RefreshTrackedTargets()
    {
        for (var index = Targets.Count - 1; index >= 0; index--)
        {
            if (Targets[index].TryGetTarget(out var target))
            {
                UpdateTarget(target);
            }
            else
            {
                Targets.RemoveAt(index);
            }
        }
    }

    private static void UpdateTarget(DependencyObject target)
    {
        ApplyText(target, GetTextKey(target));
        ApplyContent(target, GetContentKey(target));
        ApplyHeader(target, GetHeaderKey(target));
        ApplyPlaceholderText(target, GetPlaceholderTextKey(target));
        ApplyTitle(target, GetTitleKey(target));
        ApplyToolTip(target, GetToolTipKey(target));
        ApplyAutomationName(target, GetAutomationNameKey(target));
    }

    private static void ApplyText(DependencyObject target, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var text = LocalizationService.Get(key);
        switch (target)
        {
            case TextBlock textBlock:
                textBlock.Text = text;
                break;
            case MenuFlyoutSubItem menuFlyoutSubItem:
                menuFlyoutSubItem.Text = text;
                break;
            case MenuFlyoutItem menuFlyoutItem:
                menuFlyoutItem.Text = text;
                break;
            case SelectorBarItem selectorBarItem:
                selectorBarItem.Text = text;
                break;
            case AppBarButton appBarButton:
                appBarButton.Label = text;
                break;
        }
    }

    private static void ApplyContent(DependencyObject target, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (target is ContentControl contentControl)
        {
            contentControl.Content = LocalizationService.Get(key);
        }
    }

    private static void ApplyHeader(DependencyObject target, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var text = LocalizationService.Get(key);
        switch (target)
        {
            case TextBox textBox:
                textBox.Header = text;
                break;
            case ComboBox comboBox:
                comboBox.Header = text;
                break;
            case ToggleSwitch toggleSwitch:
                toggleSwitch.Header = text;
                break;
        }
    }

    private static void ApplyPlaceholderText(DependencyObject target, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var text = LocalizationService.Get(key);
        switch (target)
        {
            case TextBox textBox:
                textBox.PlaceholderText = text;
                break;
            case AutoSuggestBox autoSuggestBox:
                autoSuggestBox.PlaceholderText = text;
                break;
        }
    }

    private static void ApplyTitle(DependencyObject target, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (target is InfoBar infoBar)
        {
            infoBar.Title = LocalizationService.Get(key);
        }
    }

    private static void ApplyToolTip(DependencyObject target, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        ToolTipService.SetToolTip(target, LocalizationService.Get(key));
    }

    private static void ApplyAutomationName(DependencyObject target, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        AutomationProperties.SetName(target, LocalizationService.Get(key));
    }
}
