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

        // 기본 캐릭터는 exe 안에 들어 있다. 설정을 읽기 전에 먼저 풀어놔야 목록에 잡힌다.
        CharacterAssets.EnsureExtracted();

        SettingsService settingsService = new();
        AppSettings settings = await settingsService.LoadAsync();

        MainWindow window = new(settingsService, settings);
        MainWindow = window;
        window.Show();
    }
}
