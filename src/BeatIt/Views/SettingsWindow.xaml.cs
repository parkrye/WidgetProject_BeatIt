using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using BeatIt.Core;
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

    /// <summary>목록 맨 앞이 곧 기본값이다. 같은 걸 물어보자고 폴더를 또 훑지 않는다.</summary>
    private string? _defaultCharacter;
    private readonly string? _defaultTheme;

    private readonly AreaChoice[] _areaChoices =
    [
        new(WanderAreaKind.FullScreen, "화면 전체"),
        new(WanderAreaKind.CurrentMonitor, "현재 모니터"),
        new(WanderAreaKind.WorkArea, "작업 영역 (작업표시줄 제외)"),
        new(WanderAreaKind.Custom, "직접 지정..."),
    ];

    private AreaRect? _customArea;
    private AreaChoice _lastAreaChoice;
    private bool _ready;

    public SettingsWindow(AppSettings settings, ISettingsPreview preview)
    {
        InitializeComponent();

        Result = settings;
        _preview = preview;
        _characters = [.. CharacterLibrary.Scan()];
        _themes = [.. ThemeLibrary.Scan()];
        _defaultCharacter = _characters.FirstOrDefault()?.Path;
        _defaultTheme = _themes.FirstOrDefault()?.Path;
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
        _customArea = settings.CustomWanderArea?.Clone();
        WanderAreaCombo.ItemsSource = _areaChoices;
        WanderAreaCombo.SelectedItem = _areaChoices.First(choice => choice.Kind == settings.WanderArea);
        _lastAreaChoice = (AreaChoice)WanderAreaCombo.SelectedItem;
        TopmostCheck.IsChecked = settings.Topmost;
        LockCheck.IsChecked = settings.PositionLocked;

        SelectCharacter(settings.CharacterPath);
        SelectTheme(settings.ThemePath);
        RefreshWanderArea();

        // 여기까지는 값을 채워 넣는 단계라 미리보기를 쏘지 않는다.
        _ready = true;
    }

    /// <summary>확인을 눌렀을 때 적용할 설정.</summary>
    public AppSettings Result { get; }

    private void OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => Push();

    private void OnValueToggled(object sender, RoutedEventArgs e) => Push();

    private void OnWanderToggled(object sender, RoutedEventArgs e)
    {
        RefreshWanderArea();
        Push();
    }

    /// <summary>
    /// 직접 지정을 고르는 순간 바로 그리게 한다. 그리다 말면 고르기 전으로 되돌린다.
    /// 이미 그려둔 게 있으면 그대로 쓰고, 다시 그리려면 옆 버튼을 누른다.
    /// </summary>
    private void OnWanderAreaChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WanderAreaCombo.SelectedItem is not AreaChoice choice)
        {
            return;
        }

        AreaChoice previous = _lastAreaChoice;
        _lastAreaChoice = choice;
        RefreshWanderArea();
        Push();

        if (!_ready || choice.Kind != WanderAreaKind.Custom || _customArea is not null)
        {
            return;
        }

        // 여기는 드롭다운이 아직 안 닫힌 자리다. 이대로 창을 숨기면 목록 팝업이 주인을 잃고
        // 화면 구석에 박힌 채 마우스까지 물고 늘어진다. 콤보가 뒷정리를 끝낸 뒤에 연다.
        Dispatcher.InvokeAsync(
            () =>
            {
                // 고르자마자 확인이나 취소를 눌러 창이 먼저 닫혔으면 여기서 그만둔다.
                // 닫힌 창을 오버레이의 주인으로 삼으면 그 자리에서 터진다.
                if (!IsLoaded)
                {
                    return;
                }

                if (TryPickArea())
                {
                    RefreshWanderArea();
                    Push();
                    return;
                }

                WanderAreaCombo.SelectedItem = previous;
            },
            DispatcherPriority.Background);
    }

    private void OnPickArea(object sender, RoutedEventArgs e)
    {
        if (!TryPickArea())
        {
            return;
        }

        RefreshWanderArea();
        Push();
    }

    /// <summary>화면을 덮는 오버레이를 띄워 영역을 그리게 한다. 설정 창이 가리면 안 되니 잠깐 숨긴다.</summary>
    private bool TryPickArea()
    {
        // 열려 있는 목록과 마우스 캡처를 먼저 걷어낸다. 창을 숨기면 소유 팝업도 같이 숨었다가
        // 다시 보일 때 되살아나는데, WPF 는 이미 닫은 걸로 알고 있어서 닫지도 옮기지도 못한다.
        WanderAreaCombo.IsDropDownOpen = false;
        Mouse.Capture(null);

        Visibility = Visibility.Hidden;
        try
        {
            Rect? initial = _customArea is { } area
                ? new Rect(area.Left, area.Top, area.Width, area.Height)
                : null;

            AreaPickerWindow picker = new(initial) { Owner = this };
            if (picker.ShowDialog() != true)
            {
                return false;
            }

            _customArea = new AreaRect
            {
                Left = picker.Area.X,
                Top = picker.Area.Y,
                Width = picker.Area.Width,
                Height = picker.Area.Height,
            };

            return true;
        }
        finally
        {
            Visibility = Visibility.Visible;
            Activate();
        }
    }

    private void RefreshWanderArea()
    {
        bool wandering = WanderCheck.IsChecked == true;
        WanderAreaRow.IsEnabled = wandering;
        WanderAreaSummary.IsEnabled = wandering;

        WanderAreaKind kind = (WanderAreaCombo.SelectedItem as AreaChoice)?.Kind ?? WanderAreaKind.FullScreen;
        PickAreaButton.Visibility = kind == WanderAreaKind.Custom ? Visibility.Visible : Visibility.Collapsed;
        WanderAreaSummary.Text = DescribeArea(kind);
    }

    private string DescribeArea(WanderAreaKind kind) => kind switch
    {
        WanderAreaKind.CurrentMonitor => "위젯이 올라가 있는 모니터 안에서만 돈다. 다른 모니터로 끌고 가면 거기서 돈다.",
        WanderAreaKind.WorkArea => "그 모니터의 작업표시줄을 뺀 자리에서만 돈다.",
        WanderAreaKind.Custom when _customArea is { } area =>
            $"그려둔 영역 {area.Width:F0} x {area.Height:F0} @ ({area.Left:F0}, {area.Top:F0})",
        WanderAreaKind.Custom => "아직 안 그렸다. 화면 전체로 돈다.",
        _ => "모니터를 다 합친 화면 전체에서 돈다.",
    };

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

    /// <summary>
    /// 캐릭터를 만들고 고치는 창을 연다. 닫고 나면 목록이 달라져 있을 수 있어 다시 읽는다.
    /// 파일을 직접 손대는 일이라 "만지면 즉시 반영" 하는 이 창과 성질이 달라 따로 뺐다.
    /// </summary>
    private void OnManageCharacters(object sender, RoutedEventArgs e)
    {
        CharacterLibrary.EnsureUserRoot();

        // 위젯이 쥐고 있는 소리 파일을 먼저 놓게 한다. 미리 열어둔 채로는 지금 쓰는 캐릭터의
        // 소리를 빼지도 갈아 끼우지도 못한다. 닫고 나서 통째로 다시 읽으니 조용한 건 그동안뿐이다.
        _preview.ReleaseAudio();

        string? chosen = (CharacterCombo.SelectedItem as CharacterInfo)?.Path;
        CharacterEditorWindow editor = new(chosen) { Owner = this };
        editor.ShowDialog();

        // 편집기에서 무엇을 보고 있었는지가 아니라, 쓰던 캐릭터가 어떻게 됐는지를 따라간다.
        ReloadCharacters(editor.InUse);

        // 폴더 안이 달라졌으니 경로가 그대로여도 다시 읽어야 방금 넣은 그림과 소리가 나온다.
        _preview.ReloadAssets();
    }

    /// <summary>편집기가 만들고 지운 걸 목록에 반영한다. 쓰던 캐릭터가 사라졌으면 기본 캐릭터로 떨어진다.</summary>
    private void ReloadCharacters(string? select)
    {
        _characters.Clear();
        foreach (CharacterInfo info in CharacterLibrary.Scan())
        {
            _characters.Add(info);
        }

        // 기본값은 "이름순 첫 캐릭터" 다. 편집기에서 앞 순서가 생겼으면 여기서 같이 바뀐다.
        _defaultCharacter = _characters.FirstOrDefault()?.Path;
        SelectCharacter(select);
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
        Result.WanderArea = (WanderAreaCombo.SelectedItem as AreaChoice)?.Kind ?? WanderAreaKind.FullScreen;
        Result.CustomWanderArea = _customArea?.Clone();
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

        // 쓰던 캐릭터가 사라졌으면 빈 칸으로 두지 말고 남은 것 중 첫 번째를 쥐여준다.
        CharacterCombo.SelectedItem ??= _characters.FirstOrDefault();

        // 고른 게 그대로면 SelectionChanged 가 안 울린다. 요약과 미리보기는 직접 챙긴다.
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

    /// <summary>이동 영역 콤보에 담기는 항목.</summary>
    private sealed record AreaChoice(WanderAreaKind Kind, string Label)
    {
        public override string ToString() => Label;
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
