using System.IO;
using BeatIt.Core;

namespace BeatIt.Services;

/// <summary>
/// 캐릭터 폴더를 앱 안에서 만들고 고친다.
/// 폴더를 만들고 베끼고 지우는 몫은 <see cref="FolderLibraryEditor"/> 가 쥐고 있고,
/// 여기서는 캐릭터에만 있는 것 — 상태별 칸에 그림과 소리를 넣고 빼는 일을 맡는다.
/// </summary>
public static class CharacterEditor
{
    private static readonly FolderLibraryEditor Folder = new(
        CharacterLibrary.UserRoot,
        () => BundledAssets.BundledCharacters,
        "캐릭터");

    /// <summary>exe 가 넣어준 기본 캐릭터인가. 이건 고치지도 지우지도 못한다.</summary>
    public static bool IsBundled(string folder) => Folder.IsBundled(folder);

    /// <summary>캐릭터 폴더 밖에 사는 캐릭터인가. 직접 고른 폴더가 여기 해당한다.</summary>
    public static bool IsOutside(string folder) => Folder.IsOutside(folder);

    /// <summary>빈 캐릭터 하나를 만든다. idle 폴더까지 같이 판다.</summary>
    public static EditResult Create(string? name) => Folder.Create(name, CharacterSlots.Idle.RelativePath);

    /// <summary>있는 캐릭터를 통째로 베낀다. 기본 캐릭터를 고치고 싶을 때 쓴다.</summary>
    public static EditResult Duplicate(string source, string? name) => Folder.Duplicate(source, name);

    public static EditResult Rename(string folder, string? name) => Folder.Rename(folder, name);

    public static EditResult Delete(string folder) => Folder.Delete(folder);

    /// <summary>
    /// 캐릭터 폴더 바로 아래 폴더 전부. <see cref="CharacterLibrary.Scan"/> 과 달리
    /// 아직 그림이 없어 캐릭터로 안 쳐주는 폴더도 준다. 그래야 방금 만든 걸 채워 넣을 수 있다.
    /// </summary>
    public static IReadOnlyList<string> Folders() => Folder.Folders();

    /// <summary>고른 파일을 그 칸으로 베껴 넣는다. 참조가 아니라 복사라 원본을 지워도 캐릭터가 안 깨진다.</summary>
    public static EditResult AddFiles(string folder, CharacterSlot slot, IReadOnlyList<string> sources)
    {
        EditResult guard = Folder.EnsureEditable(folder);
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
            return EditResult.Failed($"넣지 못했다.\n{ex.Message}");
        }
    }

    /// <summary>있던 파일을 다른 파일로 갈아 끼운다. 확장자가 바뀌어도 되게 새로 넣고 옛 파일을 지운다.</summary>
    public static EditResult Replace(string folder, CharacterSlot slot, string existing, string source)
    {
        EditResult guard = Folder.EnsureEditable(folder);
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
                // 옛 파일을 못 지웠으면 새 파일도 도로 치운다.
                // 안 그러면 "바꾸지 못했다" 고 알리면서 실제로는 두 장이 남는다.
                FolderLibraryEditor.Discard(target);
                throw;
            }

            return EditResult.Done(target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return EditResult.Failed($"바꾸지 못했다.\n{ex.Message}{InUseHint(slot)}");
        }
    }

    public static EditResult Remove(string folder, CharacterSlot slot, IReadOnlyList<string> paths)
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

        return EditResult.Failed($"{stuck.Count}개를 못 뺐다.\n{string.Join(", ", stuck)}{InUseHint(slot)}");
    }

    /// <summary>그 칸에 들어 있는 파일. 쓸 수 있는 확장자만 이름순으로 돌려준다.</summary>
    public static IReadOnlyList<string> Contents(string folder, CharacterSlot slot) =>
        FolderLibraryEditor.FilesIn(slot.In(folder), Extensions(slot));

    /// <summary>파일 고르기 대화상자에 걸 필터.</summary>
    public static string FilterFor(CharacterSlot slot) =>
        FolderLibraryEditor.Filter(slot.IsSound ? "소리" : "그림", Extensions(slot));

    private static string[] Extensions(CharacterSlot slot) =>
        slot.IsSound ? CharacterFolder.SoundExtensions : CharacterFolder.Extensions;

    /// <summary>
    /// 소리는 첫 소리가 늦지 않게 파일마다 미리 열어둔다(<see cref="SoundBank"/>).
    /// 이 창을 여는 동안은 위젯이 그걸 놓아주므로 보통은 여기까지 안 온다.
    /// 그래도 막혔다면 앱 밖의 다른 프로그램이 물고 있는 것이다.
    /// </summary>
    private static string InUseHint(CharacterSlot slot) => slot.IsSound
        ? "\n\n다른 프로그램이 그 소리 파일을 열어둔 것 같다. 닫고 다시 해봐야 한다."
        : string.Empty;
}
