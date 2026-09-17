using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using BeatIt.Core;
using BeatIt.Services;
using Microsoft.Win32;

namespace BeatIt.Views;

/// <summary>
/// 캐릭터 프로필을 만들고 고치는 창. 왼쪽에서 캐릭터를, 가운데에서 상태 칸을 고르고
/// 오른쪽에서 그 칸에 들어갈 그림과 소리를 넣고 뺀다.
/// 기본 캐릭터는 버전마다 앱이 다시 풀어놓는 것이라 잠가두고, 복사본을 떠서 고치게 한다.
/// </summary>
public partial class CharacterEditorWindow : Window
{
    private readonly ObservableCollection<Profile> _profiles = [];
    private readonly ObservableCollection<SlotRow> _slots = [];
    private readonly ObservableCollection<SlotFile> _files = [];

    /// <param name="select">처음에 골라둘 캐릭터 폴더. 설정 창에서 쓰고 있던 것을 넘겨준다.</param>
    public CharacterEditorWindow(string? select = null)
    {
        InitializeComponent();

        CharacterList.ItemsSource = _profiles;
        FileList.ItemsSource = _files;

        // 칸은 대기 / 움직일 때 / 맞을 때 / 소리로 묶어서 보여준다.
        CollectionViewSource grouped = new() { Source = _slots };
        grouped.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SlotRow.Group)));
        SlotList.ItemsSource = grouped.View;

        foreach (CharacterSlot slot in CharacterSlots.All)
        {
            _slots.Add(new SlotRow(slot));
        }

        ReloadProfiles(select);
        SlotList.SelectedIndex = 0;
    }

    /// <summary>창을 닫을 때 골라둔 캐릭터 폴더. 설정 창이 이걸로 목록을 맞춘다.</summary>
    public string? SelectedPath => Selected?.Path;

    private Profile? Selected => CharacterList.SelectedItem as Profile;

    private CharacterSlot? Slot => (SlotList.SelectedItem as SlotRow)?.Slot;

    private void OnCharacterChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshButtons();
        RefreshFiles();
    }

    private void OnSlotChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        SlotHint.Text = Slot?.Hint ?? string.Empty;
        RefreshFiles();
    }

    private void OnFileSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        RefreshButtons();

    private void OnCreate(object sender, RoutedEventArgs e)
    {
        string? name = NameDialog.Ask(this, "새 캐릭터", "새 캐릭터 이름을 적는다. 이 이름으로 폴더가 생긴다.");
        if (name is null)
        {
            return;
        }

        Apply(CharacterEditor.Create(name), "만들었다");
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
            $"[{profile.Name}] 을 베껴 만들 캐릭터 이름을 적는다.",
            profile.Name + " 복사본");

        if (name is null)
        {
            return;
        }

        Apply(CharacterEditor.Duplicate(profile.Path, name), "베꼈다");
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

        Apply(CharacterEditor.Rename(profile.Path, name), "이름을 바꿨다");
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } profile)
        {
            return;
        }

        MessageBoxResult answer = MessageBox.Show(
            this,
            $"[{profile.Name}] 폴더를 통째로 지운다. 안에 넣어둔 그림과 소리도 같이 사라지고 되돌릴 수 없다.\n\n지울까?",
            "BeatIt",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.OK)
        {
            return;
        }

        EditResult result = CharacterEditor.Delete(profile.Path);
        if (!result.Ok)
        {
            Complain(result.Problem!);
            return;
        }

        ReloadProfiles(null);
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
            Filter = CharacterEditor.FilterFor(slot) + "|모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        EditResult result = CharacterEditor.AddFiles(profile.Path, slot, dialog.FileNames);
        Finish(result, $"{dialog.FileNames.Length}개를 넣었다.");
    }

    private void OnReplaceFile(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } profile || Slot is not { } slot || FileList.SelectedItem is not SlotFile chosen)
        {
            return;
        }

        OpenFileDialog dialog = new()
        {
            Title = $"[{chosen.Name}] 을 대신할 파일 고르기",
            Multiselect = false,
            Filter = CharacterEditor.FilterFor(slot) + "|모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        Finish(CharacterEditor.Replace(profile.Path, slot, chosen.Path, dialog.FileName), "바꿨다.");
    }

    private void OnRemoveFiles(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } profile || Slot is not { } slot || FileList.SelectedItems.Count == 0)
        {
            return;
        }

        string[] paths = [.. FileList.SelectedItems.OfType<SlotFile>().Select(file => file.Path)];
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

        Finish(CharacterEditor.Remove(profile.Path, slot, paths), $"{paths.Length}개를 뺐다.");
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        OpenFolderDialog dialog = new() { Title = "가져올 캐릭터 폴더 고르기", Multiselect = false };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        string source = dialog.FolderName;
        string? name = NameDialog.Ask(
            this,
            "폴더에서 가져오기",
            "가져온 캐릭터를 뭐라고 부를지 적는다. 원본 폴더는 안 건드린다.",
            new DirectoryInfo(source).Name);

        if (name is null)
        {
            return;
        }

        Apply(CharacterEditor.Duplicate(source, name), "가져왔다");
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        CharacterLibrary.EnsureUserRoot();
        Process.Start(new ProcessStartInfo(CharacterLibrary.UserRoot) { UseShellExecute = true });
    }

    /// <summary>캐릭터를 만들거나 이름을 바꾼 결과를 목록에 반영한다.</summary>
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
        if (!result.Ok)
        {
            Complain(result.Problem!);
            return;
        }

        RefreshFiles();
        RefreshCounts();
        Status.Text = done;
    }

    private void ReloadProfiles(string? select)
    {
        string? keep = select ?? Selected?.Path;

        _profiles.Clear();
        foreach (string folder in CharacterEditor.Folders())
        {
            _profiles.Add(new Profile(folder));
        }

        CharacterList.SelectedItem = _profiles.FirstOrDefault(profile => SamePath(profile.Path, keep))
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
            foreach (string path in CharacterEditor.Contents(profile.Path, slot))
            {
                _files.Add(new SlotFile(path, slot.IsSound));
            }
        }
        else
        {
            SlotHeader.Text = string.Empty;
        }

        RefreshCounts();
        RefreshButtons();
    }

    /// <summary>칸 목록 오른쪽에 들어 있는 개수를 적어준다. 어디가 비었는지 한눈에 보여야 한다.</summary>
    private void RefreshCounts()
    {
        foreach (SlotRow row in _slots)
        {
            row.Count = Selected is { } profile ? CharacterEditor.Contents(profile.Path, row.Slot).Count : 0;
        }
    }

    private void RefreshButtons()
    {
        bool chosen = Selected is not null;
        bool editable = Selected is { Editable: true };

        DuplicateButton.IsEnabled = chosen;
        RenameButton.IsEnabled = editable;
        DeleteButton.IsEnabled = editable;
        AddButton.IsEnabled = editable && Slot is not null;
        ReplaceButton.IsEnabled = editable && FileList.SelectedItems.Count == 1;
        RemoveButton.IsEnabled = editable && FileList.SelectedItems.Count > 0;

        if (Selected is { Editable: false })
        {
            Status.Text = "기본 캐릭터는 앱이 버전마다 다시 풀어놓는 것이라 못 고친다. 복사해서 만들면 마음껏 고칠 수 있다.";
        }
        else if (Selected is { HasIdle: false })
        {
            Status.Text = "idle 칸이 비어 있다. 그림을 한 장이라도 넣어야 쓸 수 있는 캐릭터가 된다.";
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

    /// <summary>왼쪽 목록에 뜨는 캐릭터 한 줄.</summary>
    private sealed class Profile(string path)
    {
        public string Path { get; } = path;

        public string Name { get; } = new DirectoryInfo(path).Name;

        public bool Editable { get; } = !CharacterEditor.IsBundled(path) && !CharacterEditor.IsOutside(path);

        /// <summary>idle 이 비면 캐릭터 목록에 안 잡힌다. 그 사실을 이름 옆에 적어준다.</summary>
        public bool HasIdle => CharacterEditor.Contents(Path, CharacterSlots.Idle).Count > 0;

        public string Display => Editable
            ? HasIdle ? Name : $"{Name}  (idle 비어 있음)"
            : $"{Name}  (기본)";

        // 목록 항목을 읽어주는 도구는 ToString 을 본다. 안 겹쳐두면 타입 이름이 읽힌다.
        public override string ToString() => Display;
    }

    /// <summary>가운데 목록에 뜨는 칸 한 줄. 들어 있는 개수가 바뀌면 그 자리에서 고쳐 쓴다.</summary>
    private sealed class SlotRow(CharacterSlot slot) : INotifyPropertyChanged
    {
        private int _count;

        public event PropertyChangedEventHandler? PropertyChanged;

        public CharacterSlot Slot { get; } = slot;

        public string Group => Slot.Group;

        public int Count
        {
            get => _count;
            set
            {
                if (_count == value)
                {
                    return;
                }

                _count = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
            }
        }
    }

    /// <summary>오른쪽에 뜨는 파일 한 장.</summary>
    private sealed class SlotFile
    {
        public SlotFile(string path, bool isSound)
        {
            Path = path;
            Name = System.IO.Path.GetFileName(path);

            // 소리는 미리 볼 게 없으니 음표만 세워둔다. GIF 는 첫 프레임으로 족하다.
            Thumbnail = isSound ? null : Sprite.Load(path)?.Current;
            BadgeVisibility = Thumbnail is null ? Visibility.Visible : Visibility.Collapsed;
        }

        public string Path { get; }

        public string Name { get; }

        public ImageSource? Thumbnail { get; }

        public Visibility BadgeVisibility { get; }
    }
}
