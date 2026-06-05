using Microsoft.UI.Xaml;

using Lumina.App.Services;

namespace Lumina.App;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public App()
    {
        LocalizationService.InitializeFromSettings();
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
