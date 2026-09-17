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

        // 기본 캐릭터와 테마는 exe 안에 들어 있다. 설정을 읽기 전에 먼저 풀어놔야 목록에 잡힌다.
        BundledAssets.EnsureExtracted();

        SettingsService settingsService = new();
        AppSettings settings = await settingsService.LoadAsync();

        // 기본 캐릭터 이름이 바뀌었으면 골라둔 경로도 같이 따라가야 한다.
        settings.CharacterPath = BundledAssets.Rename(settings.CharacterPath);

        MainWindow window = new(settingsService, settings);
        MainWindow = window;
        window.Show();
    }
}
