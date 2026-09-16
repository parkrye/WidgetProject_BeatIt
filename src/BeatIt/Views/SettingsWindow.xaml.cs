using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using BeatIt.Models;
using BeatIt.Services;
using Microsoft.Win32;

namespace BeatIt.Views;

/// <summary>캐릭터와 타격 감각을 고르는 설정 창.</summary>
public partial class SettingsWindow : Window
{
    private readonly ObservableCollection<CharacterInfo> _characters;
    private readonly string? _defaultPath = CharacterLibrary.DefaultPath;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();

        Result = settings;
        _characters = [.. CharacterLibrary.Scan()];
        CharacterCombo.ItemsSource = _characters;

        WidthSlider.Value = settings.WidgetWidth;
        ComboSlider.Value = settings.ComboTimeoutMs;
        IdleMinSlider.Value = settings.IdleMinMs;
        IdleMaxSlider.Value = settings.IdleMaxMs;
        TopmostCheck.IsChecked = settings.Topmost;

        SelectInitialCharacter(settings.CharacterPath);
    }

    /// <summary>확인을 눌렀을 때 적용할 설정.</summary>
    public AppSettings Result { get; }

    private void OnCharacterChanged(object sender, SelectionChangedEventArgs e) =>
        SummaryText.Text = (CharacterCombo.SelectedItem as CharacterInfo)?.Summary ?? "쓸 수 있는 캐릭터가 없다. 기본 샌드백으로 대신한다.";

    private void OnAddCharacter(object sender, RoutedEventArgs e)
    {
        OpenFolderDialog dialog = new() { Title = "캐릭터 폴더 고르기", Multiselect = false };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        CharacterInfo? info = CharacterLibrary.Describe(dialog.FolderName);
        if (info is null)
        {
            MessageBox.Show(
                this,
                "그 폴더에서 쓸 이미지를 못 찾았다.\nidle / move / beat 하위 폴더에 이미지를 넣거나, 폴더에 이미지를 바로 넣어야 한다.",
                "BeatIt",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        CharacterInfo? existing = _characters.FirstOrDefault(c => PathEquals(c.Path, info.Path));
        if (existing is null)
        {
            _characters.Add(info);
            existing = info;
        }

        CharacterCombo.SelectedItem = existing;
    }

    private void OnOpenUserFolder(object sender, RoutedEventArgs e)
    {
        CharacterLibrary.EnsureUserRoot();
        Process.Start(new ProcessStartInfo(CharacterLibrary.UserRoot) { UseShellExecute = true });
    }

    private void OnIdleMinChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IdleMaxSlider is not null && IdleMaxSlider.Value < e.NewValue)
        {
            IdleMaxSlider.Value = e.NewValue;
        }
    }

    private void OnIdleMaxChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IdleMinSlider is not null && IdleMinSlider.Value > e.NewValue)
        {
            IdleMinSlider.Value = e.NewValue;
        }
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        string? path = (CharacterCombo.SelectedItem as CharacterInfo)?.Path;

        // 기본 캐릭터는 경로를 비워 저장한다. 앱 폴더가 바뀌어도 따라온다.
        Result.CharacterPath = path is not null && PathEquals(path, _defaultPath) ? null : path;
        Result.WidgetWidth = WidthSlider.Value;
        Result.ComboTimeoutMs = (int)ComboSlider.Value;
        Result.IdleMinMs = (int)IdleMinSlider.Value;
        Result.IdleMaxMs = (int)Math.Max(IdleMaxSlider.Value, IdleMinSlider.Value);
        Result.Topmost = TopmostCheck.IsChecked == true;
        DialogResult = true;
    }

    private void SelectInitialCharacter(string? path)
    {
        string? target = path ?? _defaultPath;
        CharacterCombo.SelectedItem = _characters.FirstOrDefault(c => PathEquals(c.Path, target));

        if (CharacterCombo.SelectedItem is not null || target is null)
        {
            OnCharacterChanged(this, null!);
            return;
        }

        // 목록에 없는 폴더를 쓰고 있었다면(직접 고른 폴더) 그대로 목록에 얹어준다.
        CharacterInfo? info = CharacterLibrary.Describe(target);
        if (info is not null)
        {
            _characters.Add(info);
            CharacterCombo.SelectedItem = info;
        }

        OnCharacterChanged(this, null!);
    }

    private static bool PathEquals(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
    }
}
