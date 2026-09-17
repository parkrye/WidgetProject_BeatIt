using System.IO;
using BeatIt.Core;

namespace BeatIt.Services;

/// <summary>
/// 콤보 숫자와 라벨이 들어가는 자리 하나. <see cref="Path"/> 가 null 이면 아직 빈 자리다.
/// 빈 자리도 목록에 보여야 어디가 모자라 글꼴로 떨어지는지 알 수 있다.
/// </summary>
public sealed record ThemePlace(string Name, string? Path);

/// <summary>
/// 테마 폴더를 앱 안에서 만들고 고친다. 캐릭터와 마찬가지로 <see cref="FolderLibraryEditor"/> 위에 얹혀 있고,
/// 여기서는 테마에만 있는 것 — 이름이 정해진 콤보 자리를 다루는 일을 맡는다.
/// </summary>
public static class ThemeEditor
{
    private static readonly FolderLibraryEditor Folder = new(
        ThemeLibrary.Root,
        () => BundledAssets.BundledThemes,
        "테마");

    /// <summary>exe 가 넣어준 기본 테마인가. 이건 고치지도 지우지도 못한다.</summary>
    public static bool IsBundled(string folder) => Folder.IsBundled(folder);

    /// <summary>테마 폴더 밖에 사는 테마인가.</summary>
    public static bool IsOutside(string folder) => Folder.IsOutside(folder);

    /// <summary>빈 테마 하나를 만든다. effects 폴더까지 같이 판다.</summary>
    public static EditResult Create(string? name) => Folder.Create(name, "effects");

    public static EditResult Duplicate(string source, string? name) => Folder.Duplicate(source, name);

    public static EditResult Rename(string folder, string? name) => Folder.Rename(folder, name);

    public static EditResult Delete(string folder) => Folder.Delete(folder);

    /// <summary>테마 폴더 바로 아래 폴더 전부. 아직 비어서 목록에 안 잡히는 것도 준다.</summary>
    public static IReadOnlyList<string> Folders() => Folder.Folders();

    /// <summary>
    /// 그 칸에 들어 있는 파일. <see cref="ThemeSlotKind.Fixed"/> 칸은 자리 이름에 맞는 것만 골라내는데,
    /// 그렇게 하지 않으면 label 과 0~9 가 같은 combo 폴더를 쓰기 때문에 서로의 파일까지 보여준다.
    /// </summary>
    public static IReadOnlyList<string> Contents(string folder, ThemeSlot slot)
    {
        IReadOnlyList<string> files = FolderLibraryEditor.FilesIn(slot.In(folder), slot.Extensions);
        if (slot.Kind != ThemeSlotKind.Fixed)
        {
            return files;
        }

        return [.. files.Where(file => slot.Places.Contains(Path.GetFileNameWithoutExtension(file), StringComparer.OrdinalIgnoreCase))];
    }

    /// <summary>자리마다 지금 무엇이 들어 있는지. 빈 자리는 <see cref="ThemePlace.Path"/> 가 null 이다.</summary>
    public static IReadOnlyList<ThemePlace> Places(string folder, ThemeSlot slot)
    {
        IReadOnlyList<string> files = Contents(folder, slot);
        return
        [
            .. slot.Places.Select(place => new ThemePlace(
                place,
                files.FirstOrDefault(file => string.Equals(
                    Path.GetFileNameWithoutExtension(file),
                    place,
                    StringComparison.OrdinalIgnoreCase))))
        ];
    }

    /// <summary>이름 제한이 없는 칸에 파일을 베껴 넣는다. 이펙트와 글꼴이 여기 해당한다.</summary>
    public static EditResult AddFiles(string folder, ThemeSlot slot, IReadOnlyList<string> sources)
    {
        EditResult guard = EnsureFree(folder, slot);
        if (!guard.Ok)
        {
            return guard;
        }

        string destination = slot.In(folder);
        try
        {
            Directory.CreateDirectory(destination);
            foreach (string source in sources)
            {
                File.Copy(source, FolderLibraryEditor.FreeNameIn(destination, Path.GetFileName(source)));
            }

            return EditResult.Done(destination);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return EditResult.Failed($"넣지 못했다.\n{ex.Message}{FontHint(slot)}");
        }
    }

    /// <summary>이름 제한이 없는 칸에서 파일 하나를 갈아 끼운다.</summary>
    public static EditResult Replace(string folder, ThemeSlot slot, string existing, string source)
    {
        EditResult guard = EnsureFree(folder, slot);
        if (!guard.Ok)
        {
            return guard;
        }

        string destination = slot.In(folder);
        if (!FolderLibraryEditor.Contains(destination, existing))
        {
            return EditResult.Failed("그 파일은 이 칸에 없다.");
        }

        try
        {
            string target = FolderLibraryEditor.FreeNameIn(destination, Path.GetFileName(source));
            File.Copy(source, target);

            try
            {
                File.Delete(existing);
            }
            catch
            {
                // 옛 파일을 못 지웠으면 새 파일도 도로 치운다. 안 그러면 실패를 알리면서 두 장이 남는다.
                FolderLibraryEditor.Discard(target);
                throw;
            }

            return EditResult.Done(target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return EditResult.Failed($"바꾸지 못했다.\n{ex.Message}{FontHint(slot)}");
        }
    }

    /// <summary>
    /// 이름이 정해진 자리에 파일을 넣는다. 자리 이름으로 베끼되 확장자는 원본을 따른다.
    /// 그 자리에 이미 다른 확장자로 들어 있던 건 같이 치운다. 안 그러면 <c>3.png</c> 와 <c>3.gif</c> 가
    /// 함께 남아 어느 쪽이 그려질지 알 수 없어진다.
    /// </summary>
    public static EditResult PutAt(string folder, ThemeSlot slot, string place, string source)
    {
        EditResult guard = Folder.EnsureEditable(folder);
        if (!guard.Ok)
        {
            return guard;
        }

        if (!slot.Places.Contains(place, StringComparer.OrdinalIgnoreCase))
        {
            return EditResult.Failed("그런 자리는 없다.");
        }

        string destination = slot.In(folder);
        string target = Path.Combine(destination, place + Path.GetExtension(source).ToLowerInvariant());

        try
        {
            Directory.CreateDirectory(destination);

            // 먼저 치우고 넣는다. 같은 확장자면 File.Copy 가 거절하기 때문이다.
            List<string> stuck = [];
            foreach (string old in Contents(folder, slot))
            {
                if (!string.Equals(Path.GetFileNameWithoutExtension(old), place, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!FolderLibraryEditor.Discard(old))
                {
                    stuck.Add(Path.GetFileName(old));
                }
            }

            if (stuck.Count > 0)
            {
                return EditResult.Failed($"그 자리에 있던 걸 못 치웠다.\n{string.Join(", ", stuck)}");
            }

            File.Copy(source, target);
            return EditResult.Done(target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return EditResult.Failed($"넣지 못했다.\n{ex.Message}");
        }
    }

    public static EditResult Remove(string folder, ThemeSlot slot, IReadOnlyList<string> paths)
    {
        EditResult guard = Folder.EnsureEditable(folder);
        if (!guard.Ok)
        {
            return guard;
        }

        // 하나가 막혔다고 나머지까지 안 빼면, 어디까지 됐는지 알 수 없어진다.
        List<string> stuck = [];
        foreach (string path in paths)
        {
            if (!FolderLibraryEditor.Discard(path))
            {
                stuck.Add(Path.GetFileName(path));
            }
        }

        if (stuck.Count == 0)
        {
            return EditResult.Done(folder);
        }

        return EditResult.Failed($"{stuck.Count}개를 못 뺐다.\n{string.Join(", ", stuck)}{FontHint(slot)}");
    }

    /// <summary>파일 고르기 대화상자에 걸 필터.</summary>
    public static string FilterFor(ThemeSlot slot) =>
        FolderLibraryEditor.Filter(slot.Kind == ThemeSlotKind.Font ? "글꼴" : "그림", slot.Extensions);

    /// <summary>이름을 자유롭게 붙이는 칸인지 확인한다. 자리가 정해진 칸은 <see cref="PutAt"/> 로 가야 한다.</summary>
    private static EditResult EnsureFree(string folder, ThemeSlot slot)
    {
        EditResult guard = Folder.EnsureEditable(folder);
        if (!guard.Ok)
        {
            return guard;
        }

        return slot.Kind == ThemeSlotKind.Fixed
            ? EditResult.Failed("이 칸은 자리가 정해져 있다.\n자리를 고르고 [이 자리에 넣기] 를 눌러야 한다.")
            : guard;
    }

    /// <summary>
    /// 글꼴은 <c>Fonts.GetFontFamilies</c> 가 폴더째 훑으면서 쥐고 있을 수 있다.
    /// 테마를 바꾸면 놓으므로, 막혔을 때 뭘 해야 하는지만 알려준다.
    /// </summary>
    private static string FontHint(ThemeSlot slot) => slot.Kind == ThemeSlotKind.Font
        ? "\n\n지금 쓰고 있는 테마의 글꼴은 그려두느라 잠겨 있을 수 있다. 다른 테마로 바꾼 뒤에 해야 한다."
        : string.Empty;
}
