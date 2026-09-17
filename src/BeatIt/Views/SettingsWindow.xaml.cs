using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using BeatIt.Models;
using BeatIt.Services;
using Microsoft.Win32;

namespace BeatIt.Views;

/// <summary>캐릭터·테마와 타격 감각을 고르는 설정 창. 값을 만지는 즉시 위젯에 반영된다.</summary>
public partial class SettingsWindow : Window
{
    private readonly ISettingsPreview _preview;
    private readonly ObservableCollection<CharacterInfo> _characters;
    private readonly ObservableCollection<ThemeInfo> _themes;
    private readonly string? _defaultCharacter = CharacterLibrary.DefaultPath;
    private readonly string? _defaultTheme = ThemeLibrary.DefaultPath;

    private bool _ready;

    public SettingsWindow(AppSettings settings, ISettingsPreview preview)
    {
        InitializeComponent();

        Result = settings;
        _preview = preview;
        _characters = [.. CharacterLibrary.Scan()];
        _themes = [.. ThemeLibrary.Scan()];
        CharacterCombo.ItemsSource = _characters;
        ThemeCombo.ItemsSource = _themes;

        WidthSlider.Value = settings.WidgetWidth;
        ComboSlider.Value = settings.ComboTimeoutMs;
        IdleMinSlider.Value = settings.IdleMinMs;
        IdleMaxSlider.Value = settings.IdleMaxMs;
        ComboSizeSlider.Value = settings.ComboSize;
        ComboXSlider.Value = settings.ComboOffsetX;
        ComboYSlider.Value = settings.ComboOffsetY;
        VolumeSlider.Value = settings.SoundVolume * 100;
        MuteCheck.IsChecked = settings.SoundMuted;
        IdleSoundMinSlider.Value = settings.IdleSoundMinMs;
        IdleSoundMaxSlider.Value = settings.IdleSoundMaxMs;
        EffectsCheck.IsChecked = settings.EffectsEnabled;
        WanderCheck.IsChecked = settings.Wander;
        TopmostCheck.IsChecked = settings.Topmost;
        LockCheck.IsChecked = settings.PositionLocked;

        SelectCharacter(settings.CharacterPath);
        SelectTheme(settings.ThemePath);

        // 여기까지는 값을 채워 넣는 단계라 미리보기를 쏘지 않는다.
        _ready = true;
    }

    /// <summary>확인을 눌렀을 때 적용할 설정.</summary>
    public AppSettings Result { get; }

    private void OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => Push();

    private void OnValueToggled(object sender, RoutedEventArgs e) => Push();

    private void OnCharacterChanged(object sender, SelectionChangedEventArgs e)
    {
        CharacterSummary.Text = (CharacterCombo.SelectedItem as CharacterInfo)?.Summary
            ?? "쓸 수 있는 캐릭터가 없다. 기본 샌드백으로 대신한다.";
        Push();
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        ThemeSummary.Text = (ThemeCombo.SelectedItem as ThemeInfo)?.Summary
            ?? "쓸 수 있는 테마가 없다. 이펙트 없이 기본 글꼴로 그린다.";
        Push();
    }

    private void OnIdleMinChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IdleMaxSlider is not null && IdleMaxSlider.Value < e.NewValue)
        {
            IdleMaxSlider.Value = e.NewValue;
        }

        Push();
    }

    private void OnIdleMaxChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IdleMinSlider is not null && IdleMinSlider.Value > e.NewValue)
        {
            IdleMinSlider.Value = e.NewValue;
        }

        Push();
    }

    private void OnIdleSoundMinChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IdleSoundMaxSlider is not null && IdleSoundMaxSlider.Value < e.NewValue)
        {
            IdleSoundMaxSlider.Value = e.NewValue;
        }

        Push();
    }

    private void OnIdleSoundMaxChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IdleSoundMinSlider is not null && IdleSoundMinSlider.Value > e.NewValue)
        {
            IdleSoundMinSlider.Value = e.NewValue;
        }

        Push();
    }

    private void OnAddCharacter(object sender, RoutedEventArgs e)
    {
        string? folder = AskFolder("캐릭터 폴더 고르기");
        if (folder is null)
        {
            return;
        }

        CharacterInfo? info = CharacterLibrary.Describe(folder);
        if (info is null)
        {
            Complain("그 폴더에서 쓸 이미지를 못 찾았다.\nidle / move / beat 하위 폴더에 이미지를 넣거나, 폴더에 이미지를 바로 넣어야 한다.");
            return;
        }

        CharacterInfo existing = _characters.FirstOrDefault(c => SamePath(c.Path, info.Path)) ?? Add(_characters, info);
        CharacterCombo.SelectedItem = existing;
    }

    private void OnAddTheme(object sender, RoutedEventArgs e)
    {
        string? folder = AskFolder("테마 폴더 고르기");
        if (folder is null)
        {
            return;
        }

        ThemeInfo? info = ThemeLibrary.Describe(folder);
        if (info is null)
        {
            Complain("그 폴더를 테마로 읽을 수 없다.\neffects / combo 하위 폴더를 두고 그 안에 이미지를 넣어야 한다.");
            return;
        }

        ThemeInfo existing = _themes.FirstOrDefault(t => SamePath(t.Path, info.Path)) ?? Add(_themes, info);
        ThemeCombo.SelectedItem = existing;
    }

    private void OnOpenCharacterFolder(object sender, RoutedEventArgs e)
    {
        CharacterLibrary.EnsureUserRoot();
        Open(CharacterLibrary.UserRoot);
    }

    private void OnOpenThemeFolder(object sender, RoutedEventArgs e)
    {
        ThemeLibrary.EnsureRoot();
        Open(ThemeLibrary.Root);
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        Collect();
        DialogResult = true;
    }

    /// <summary>지금 화면의 값을 위젯에 바로 비춰본다. 복사본을 넘겨야 위젯이 무엇이 바뀌었는지 알 수 있다.</summary>
    private void Push()
    {
        if (!_ready)
        {
            return;
        }

        _preview.Preview(Collect().Clone());
    }

    private AppSettings Collect()
    {
        string? character = (CharacterCombo.SelectedItem as CharacterInfo)?.Path;
        string? theme = (ThemeCombo.SelectedItem as ThemeInfo)?.Path;

        // 기본 캐릭터와 기본 테마는 경로를 비워 저장한다. 앱 폴더가 바뀌어도 따라온다.
        Result.CharacterPath = character is not null && SamePath(character, _defaultCharacter) ? null : character;
        Result.ThemePath = theme is not null && SamePath(theme, _defaultTheme) ? null : theme;
        Result.WidgetWidth = WidthSlider.Value;
        Result.ComboTimeoutMs = (int)ComboSlider.Value;
        Result.IdleMinMs = (int)IdleMinSlider.Value;
        Result.IdleMaxMs = (int)Math.Max(IdleMaxSlider.Value, IdleMinSlider.Value);
        Result.ComboSize = ComboSizeSlider.Value;
        Result.ComboOffsetX = ComboXSlider.Value;
        Result.ComboOffsetY = ComboYSlider.Value;
        Result.SoundVolume = VolumeSlider.Value / 100;
        Result.SoundMuted = MuteCheck.IsChecked == true;
        Result.IdleSoundMinMs = (int)IdleSoundMinSlider.Value;
        Result.IdleSoundMaxMs = (int)Math.Max(IdleSoundMaxSlider.Value, IdleSoundMinSlider.Value);
        Result.EffectsEnabled = EffectsCheck.IsChecked == true;
        Result.Wander = WanderCheck.IsChecked == true;
        Result.Topmost = TopmostCheck.IsChecked == true;
        Result.PositionLocked = LockCheck.IsChecked == true;
        return Result;
    }

    private void SelectCharacter(string? path)
    {
        string? target = path ?? _defaultCharacter;
        CharacterCombo.SelectedItem = _characters.FirstOrDefault(c => SamePath(c.Path, target));
        if (CharacterCombo.SelectedItem is null && target is not null && CharacterLibrary.Describe(target) is { } info)
        {
            // 목록에 없는 폴더를 쓰고 있었다면(직접 고른 폴더) 그대로 목록에 얹어준다.
            CharacterCombo.SelectedItem = Add(_characters, info);
        }

        OnCharacterChanged(this, null!);
    }

    private void SelectTheme(string? path)
    {
        string? target = path ?? _defaultTheme;
        ThemeCombo.SelectedItem = _themes.FirstOrDefault(t => SamePath(t.Path, target));
        if (ThemeCombo.SelectedItem is null && target is not null && ThemeLibrary.Describe(target) is { } info)
        {
            ThemeCombo.SelectedItem = Add(_themes, info);
        }

        OnThemeChanged(this, null!);
    }

    private static T Add<T>(ObservableCollection<T> list, T item)
    {
        list.Add(item);
        return item;
    }

    private string? AskFolder(string title)
    {
        OpenFolderDialog dialog = new() { Title = title, Multiselect = false };
        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }

    private void Complain(string message) =>
        MessageBox.Show(this, message, "BeatIt", MessageBoxButton.OK, MessageBoxImage.Warning);

    private static void Open(string folder) =>
        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });

    private static bool SamePath(string? left, string? right)
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
