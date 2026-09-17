using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using BeatIt.Core;
using BeatIt.Services;
using Microsoft.Win32;

namespace BeatIt.Views;

/// <summary>
/// 테마를 만들고 고치는 창. 캐릭터 관리 창과 같은 3단이다.
/// 왼쪽에서 테마를, 가운데에서 칸을 고르고 오른쪽에서 그 칸에 들어갈 그림과 글꼴을 넣고 뺀다.
/// 캐릭터와 다른 점은 콤보다. 숫자와 라벨은 파일 이름이 정해져 있어서, 파일을 쌓는 게 아니라
/// **자리를 골라 채운다.** 열 자리가 다 차야 숫자 그림으로 그려지기 때문에 빈 자리도 같이 보여준다.
/// </summary>
public partial class ThemeEditorWindow : Window
{
    private readonly ObservableCollection<Profile> _profiles = [];
    private readonly ObservableCollection<SlotRow> _slots = [];
    private readonly ObservableCollection<SlotFile> _files = [];

    /// <param name="select">처음에 골라둘 테마 폴더. 설정 창에서 쓰고 있던 것을 넘겨준다.</param>
    public ThemeEditorWindow(string? select = null)
    {
        InitializeComponent();

        ThemeList.ItemsSource = _profiles;
        FileList.ItemsSource = _files;

        CollectionViewSource grouped = new() { Source = _slots };
        grouped.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SlotRow.Group)));
        SlotList.ItemsSource = grouped.View;

        foreach (ThemeSlot slot in ThemeSlots.All)
        {
            _slots.Add(new SlotRow(slot));
        }

        InUse = select;
        ReloadProfiles(select);
        SlotList.SelectedIndex = 0;
    }

    /// <summary>
    /// 설정 창이 쓰고 있던 테마. 이름을 바꾸면 따라가고, 지우면 비운다.
    /// 왼쪽 목록의 커서와는 다르다. 구경만 하고 닫았는데 위젯 테마가 바뀌면 안 된다.
    /// </summary>
    public string? InUse { get; private set; }

    private Profile? Selected => ThemeList.SelectedItem as Profile;

    private ThemeSlot? Slot => (SlotList.SelectedItem as SlotRow)?.Slot;

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshButtons();
        RefreshFiles();
    }

    private void OnSlotChanged(object sender, SelectionChangedEventArgs e)
    {
        SlotHint.Text = Slot?.Hint ?? string.Empty;
        RefreshFiles();
    }

    private void OnFileSelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshButtons();

    private void OnCreate(object sender, RoutedEventArgs e)
    {
        string? name = NameDialog.Ask(this, "새 테마", "새 테마 이름을 적는다. 이 이름으로 폴더가 생긴다.");
        if (name is null)
        {
            return;
        }

        Apply(ThemeEditor.Create(name), "만들었다");
    }

    private void OnDuplicate(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } profile)
        {
            return;
        }

        string? name = NameDialog.Ask(
            this,
            "복사해서 만들기",
            $"[{profile.Name}] 을 베껴 만들 테마 이름을 적는다.",
            profile.Name + " 복사본");

        if (name is null)
        {
            return;
        }

        Apply(ThemeEditor.Duplicate(profile.Path, name), "베꼈다");
    }

    private void OnRename(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } profile)
        {
            return;
        }

        string? name = NameDialog.Ask(this, "이름 바꾸기", "새 이름을 적는다.", profile.Name);
        if (name is null)
        {
            return;
        }

        bool wasInUse = SamePath(profile.Path, InUse);
        EditResult result = ThemeEditor.Rename(profile.Path, name);
        if (result.Ok && wasInUse)
        {
            InUse = result.Path;
        }

        Apply(result, "이름을 바꿨다");
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } profile)
        {
            return;
        }

        MessageBoxResult answer = MessageBox.Show(
            this,
            $"[{profile.Name}] 폴더를 통째로 지운다. 안에 넣어둔 이펙트와 콤보 그림도 같이 사라지고 되돌릴 수 없다.\n\n지울까?",
            "BeatIt",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.OK)
        {
            return;
        }

        EditResult result = ThemeEditor.Delete(profile.Path);

        // 반쯤 지워졌을 수도 있으니 실패해도 목록을 다시 읽는다.
        if (SamePath(profile.Path, InUse) && !Directory.Exists(profile.Path))
        {
            InUse = null;
        }

        ReloadProfiles(null);

        if (!result.Ok)
        {
            Complain(result.Problem!);
            return;
        }

        Status.Text = $"[{profile.Name}] 을 지웠다.";
    }

    private void OnAddFiles(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } profile || Slot is not { } slot)
        {
            return;
        }

        OpenFileDialog dialog = new()
        {
            Title = $"{slot.Label} 에 넣을 파일 고르기",
            Multiselect = true,
            Filter = ThemeEditor.FilterFor(slot) + "|모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        Finish(ThemeEditor.AddFiles(profile.Path, slot, dialog.FileNames), $"{dialog.FileNames.Length}개를 넣었다.");
    }

    /// <summary>이름이 정해진 자리를 채운다. 파일 이름이 뭐든 그 자리 이름으로 들어간다.</summary>
    private void OnPutAtPlace(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } profile || Slot is not { } slot || FileList.SelectedItem is not SlotFile { Place: { } place })
        {
            return;
        }

        OpenFileDialog dialog = new()
        {
            Title = $"[{place}] 자리에 넣을 그림 고르기",
            Multiselect = false,
            Filter = ThemeEditor.FilterFor(slot) + "|모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        Finish(ThemeEditor.PutAt(profile.Path, slot, place, dialog.FileName), $"[{place}] 자리를 채웠다.");
    }

    private void OnReplaceFile(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } profile || Slot is not { } slot || FileList.SelectedItem is not SlotFile { Path: { } existing } chosen)
        {
            return;
        }

        OpenFileDialog dialog = new()
        {
            Title = $"[{chosen.Name}] 을 대신할 파일 고르기",
            Multiselect = false,
            Filter = ThemeEditor.FilterFor(slot) + "|모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        Finish(ThemeEditor.Replace(profile.Path, slot, existing, dialog.FileName), "바꿨다.");
    }

    private void OnRemoveFiles(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } profile || Slot is not { } slot)
        {
            return;
        }

        string[] paths = [.. FileList.SelectedItems.OfType<SlotFile>().Select(file => file.Path).OfType<string>()];
        if (paths.Length == 0)
        {
            return;
        }

        MessageBoxResult answer = MessageBox.Show(
            this,
            $"고른 {paths.Length}개를 지운다. 되돌릴 수 없다.\n\n뺄까?",
            "BeatIt",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.OK)
        {
            return;
        }

        Finish(ThemeEditor.Remove(profile.Path, slot, paths), $"{paths.Length}개를 뺐다.");
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        OpenFolderDialog dialog = new() { Title = "가져올 테마 폴더 고르기", Multiselect = false };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        string source = dialog.FolderName;
        string? name = NameDialog.Ask(
            this,
            "폴더에서 가져오기",
            "가져온 테마를 뭐라고 부를지 적는다. 원본 폴더는 안 건드린다.",
            new DirectoryInfo(source).Name);

        if (name is null)
        {
            return;
        }

        Apply(ThemeEditor.Duplicate(source, name), "가져왔다");
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        ThemeLibrary.EnsureRoot();
        Process.Start(new ProcessStartInfo(ThemeLibrary.Root) { UseShellExecute = true });
    }

    /// <summary>테마를 만들거나 이름을 바꾼 결과를 목록에 반영한다.</summary>
    private void Apply(EditResult result, string done)
    {
        if (!result.Ok)
        {
            Complain(result.Problem!);
            return;
        }

        ReloadProfiles(result.Path);
        Status.Text = $"[{new DirectoryInfo(result.Path!).Name}] 을 {done}.";
    }

    /// <summary>파일을 넣거나 뺀 결과를 목록에 반영한다.</summary>
    private void Finish(EditResult result, string done)
    {
        // 여러 개 중 일부만 빠졌을 수도 있다. 실패했다고 옛 목록을 그대로 두면 안 맞는다.
        RefreshFiles();

        if (!result.Ok)
        {
            Complain(result.Problem!);
            return;
        }

        Status.Text = done;
    }

    private void ReloadProfiles(string? select)
    {
        string? keep = select ?? Selected?.Path;

        _profiles.Clear();
        foreach (string folder in ThemeEditor.Folders())
        {
            _profiles.Add(new Profile(folder));
        }

        ThemeList.SelectedItem = _profiles.FirstOrDefault(profile => SamePath(profile.Path, keep))
            ?? _profiles.FirstOrDefault();

        RefreshButtons();
        RefreshFiles();
    }

    private void RefreshFiles()
    {
        _files.Clear();
        if (Selected is { } profile && Slot is { } slot)
        {
            SlotHeader.Text = $"{profile.Name} · {slot.Group} · {slot.Label}";
            foreach (SlotFile file in Read(profile.Path, slot))
            {
                _files.Add(file);
            }
        }
        else
        {
            SlotHeader.Text = string.Empty;
        }

        RefreshCounts();
        RefreshButtons();
    }

    /// <summary>자리가 정해진 칸은 빈 자리까지 같이 준다. 어디가 비어 글꼴로 떨어지는지 보여야 한다.</summary>
    private static IEnumerable<SlotFile> Read(string folder, ThemeSlot slot)
    {
        if (slot.Kind != ThemeSlotKind.Fixed)
        {
            return ThemeEditor.Contents(folder, slot).Select(path => new SlotFile(path, slot));
        }

        return ThemeEditor.Places(folder, slot).Select(place => new SlotFile(place));
    }

    /// <summary>칸 목록 오른쪽에 든 개수를 적어준다. 자리가 정해진 칸은 "3/10" 처럼 몇 자리가 찼는지 적는다.</summary>
    private void RefreshCounts()
    {
        foreach (SlotRow row in _slots)
        {
            row.Tally = Selected is not { } profile
                ? string.Empty
                : Describe(profile.Path, row.Slot);
        }
    }

    private static string Describe(string folder, ThemeSlot slot)
    {
        int filled = ThemeEditor.Contents(folder, slot).Count;
        return slot.Kind == ThemeSlotKind.Fixed ? $"{filled}/{slot.Places.Count}" : filled.ToString();
    }

    private void RefreshButtons()
    {
        bool chosen = Selected is not null;
        bool editable = Selected is { Editable: true };
        bool fixedSlot = Slot?.Kind == ThemeSlotKind.Fixed;
        List<SlotFile> picked = [.. FileList.SelectedItems.OfType<SlotFile>()];
        int removable = picked.Count(file => file.Path is not null);

        DuplicateButton.IsEnabled = chosen;
        RenameButton.IsEnabled = editable;
        DeleteButton.IsEnabled = editable;

        // 자리가 정해진 칸에는 쌓을 수 없다. 자리를 고르고 채우는 버튼으로 갈아 끼운다.
        AddButton.Visibility = fixedSlot ? Visibility.Collapsed : Visibility.Visible;
        ReplaceButton.Visibility = fixedSlot ? Visibility.Collapsed : Visibility.Visible;
        PutButton.Visibility = fixedSlot ? Visibility.Visible : Visibility.Collapsed;

        AddButton.IsEnabled = editable && Slot is not null;
        ReplaceButton.IsEnabled = editable && picked.Count == 1 && removable == 1;
        PutButton.IsEnabled = editable && picked.Count == 1;
        RemoveButton.IsEnabled = editable && removable > 0;

        if (Selected is { Editable: false })
        {
            Status.Text = "기본 테마는 앱이 버전마다 다시 풀어놓는 것이라 못 고친다. 복사해서 만들면 마음껏 고칠 수 있다.";
        }
        else if (Selected is { HasEffects: false })
        {
            Status.Text = "이펙트 칸이 비어 있다. 때려도 아무것도 안 튄다.";
        }
    }

    private void Complain(string message) =>
        MessageBox.Show(this, message, "BeatIt", MessageBoxButton.OK, MessageBoxImage.Warning);

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

    /// <summary>왼쪽 목록에 뜨는 테마 한 줄.</summary>
    private sealed class Profile(string path)
    {
        public string Path { get; } = path;

        public string Name { get; } = new DirectoryInfo(path).Name;

        public bool Editable { get; } = !ThemeEditor.IsBundled(path) && !ThemeEditor.IsOutside(path);

        public bool HasEffects => ThemeEditor.Contents(Path, ThemeSlots.All[0]).Count > 0;

        public string Display => Editable ? Name : $"{Name}  (기본)";

        // 목록 항목을 읽어주는 도구는 ToString 을 본다. 안 겹쳐두면 타입 이름이 읽힌다.
        public override string ToString() => Display;
    }

    /// <summary>가운데 목록에 뜨는 칸 한 줄. 든 개수가 바뀌면 그 자리에서 고쳐 쓴다.</summary>
    private sealed class SlotRow(ThemeSlot slot) : INotifyPropertyChanged
    {
        private string _tally = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ThemeSlot Slot { get; } = slot;

        public string Group => Slot.Group;

        public string Tally
        {
            get => _tally;
            set
            {
                if (_tally == value)
                {
                    return;
                }

                _tally = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Tally)));
            }
        }
    }

    /// <summary>오른쪽에 뜨는 한 장. 자리가 정해진 칸에서는 아직 비어 있는 자리일 수도 있다.</summary>
    private sealed class SlotFile
    {
        /// <summary>이름이 자유로운 칸의 파일 하나.</summary>
        public SlotFile(string path, ThemeSlot slot)
        {
            Path = path;
            Name = System.IO.Path.GetFileName(path);
            Hint = path;

            // 글꼴은 미리 볼 게 없으니 글자만 세워둔다. GIF 는 첫 프레임으로 족하다.
            Thumbnail = slot.Kind == ThemeSlotKind.Font ? null : Sprite.Load(path)?.Current;
            Badge = Thumbnail is null ? "가" : string.Empty;
        }

        /// <summary>자리가 정해진 칸의 자리 하나. 비어 있으면 자리 이름만 회색으로 뜬다.</summary>
        public SlotFile(ThemePlace place)
        {
            Place = place.Name;
            Path = place.Path;
            Name = place.Path is null ? place.Name : System.IO.Path.GetFileName(place.Path);
            Hint = place.Path ?? $"[{place.Name}] 자리가 비어 있다. 골라서 [이 자리에 넣기] 를 누르면 채워진다.";

            Thumbnail = place.Path is null ? null : Sprite.Load(place.Path)?.Current;
            Badge = Thumbnail is null ? place.Name : string.Empty;
        }

        /// <summary>정해진 자리의 이름. 이름이 자유로운 칸에서는 null.</summary>
        public string? Place { get; }

        /// <summary>실제 파일. 아직 안 채운 자리면 null.</summary>
        public string? Path { get; }

        public string Name { get; }

        public string Hint { get; }

        public ImageSource? Thumbnail { get; }

        public string Badge { get; }

        public Visibility BadgeVisibility => Thumbnail is null ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>빈 자리는 테두리를 둘러 여기가 채울 곳이라는 걸 보여준다.</summary>
        public Thickness BorderThickness => Path is null ? new Thickness(1) : new Thickness(0);
    }
}
