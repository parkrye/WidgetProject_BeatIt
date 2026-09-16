using System.Windows;
using BeatIt.Models;
using BeatIt.Services;
using BeatIt.Views;

namespace BeatIt;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        SettingsService settingsService = new();
        AppSettings settings = await settingsService.LoadAsync();

        MainWindow window = new(settingsService, settings);
        MainWindow = window;
        window.Show();
    }
}
