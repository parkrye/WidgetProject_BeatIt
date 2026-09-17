using System.IO;
using BeatIt.Core;

namespace BeatIt.Services;

/// <summary>고치기를 시도한 결과. 실패하면 <see cref="Problem"/> 에 사람이 읽을 이유가 담긴다.</summary>
public sealed record EditResult(string? Path, string? Problem)
{
    public bool Ok => Problem is null;

    public static EditResult Done(string path) => new(path, null);

    public static EditResult Failed(string problem) => new(null, problem);
}

/// <summary>
/// 캐릭터 폴더를 앱 안에서 만들고 고친다.
/// 손대는 곳은 전부 <see cref="CharacterLibrary.UserRoot"/> 바로 아래이고, 그 밖은 건드리지 않는다.
/// 기본 캐릭터는 버전마다 앱이 다시 풀어놓는 것이라 잠가두고, 고치려면 복사본을 뜨게 한다.
/// </summary>
public static class CharacterEditor
{
    private static readonly char[] Forbidden = System.IO.Path.GetInvalidFileNameChars();

    /// <summary>exe 가 넣어준 기본 캐릭터인가. 이건 고치지도 지우지도 못한다.</summary>
    public static bool IsBundled(string folder) =>
        IsDirectlyInRoot(folder) && BundledAssets.BundledCharacters.Contains(NameOf(folder));

    /// <summary>캐릭터 폴더 밖에 사는 캐릭터인가. 직접 고른 폴더가 여기 해당한다.</summary>
    public static bool IsOutside(string folder) => !IsDirectlyInRoot(folder);

    /// <summary>빈 캐릭터 하나를 만든다. idle 폴더까지 같이 판다.</summary>
    public static EditResult Create(string? name)
    {
        EditResult target = ResolveNewName(name);
        if (!target.Ok)
        {
            return target;
        }

        try
        {
            Directory.CreateDirectory(CharacterSlots.Idle.In(target.Path!));
            return target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return EditResult.Failed($"폴더를 못 만들었다.\n{ex.Message}");
        }
    }

    /// <summary>있는 캐릭터를 통째로 베낀다. 기본 캐릭터를 고치고 싶을 때 쓴다.</summary>
    public static EditResult Duplicate(string source, string? name)
    {
        if (!Directory.Exists(source))
        {
            return EditResult.Failed("베낄 캐릭터 폴더가 없다.");
        }

        EditResult target = ResolveNewName(name);
        if (!target.Ok)
        {
            return target;
        }

        // 캐릭터 폴더 자신이나 그 위를 고르면 목적지가 원본 안에 들어간다.
        // 그대로 두면 자기 자신을 끝없이 베끼다가 경로 길이에서 터진다.
        if (Contains(source, target.Path!))
        {
            return EditResult.Failed("그 폴더 안으로는 못 가져온다.\n캐릭터 폴더 자신이나 그 위를 고른 것 같다.");
        }

        try
        {
            CopyTree(source, target.Path!);
            return target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return EditResult.Failed($"베끼지 못했다.\n{ex.Message}");
        }
    }

    public static EditResult Rename(string folder, string? name)
    {
        EditResult guard = EnsureEditable(folder);
        if (!guard.Ok)
        {
            return guard;
        }

        string trimmed = (name ?? string.Empty).Trim();
        if (string.Equals(trimmed, NameOf(folder), StringComparison.Ordinal))
        {
            return EditResult.Done(folder);
        }

        EditResult target = ResolveNewName(trimmed);
        if (!target.Ok)
        {
            return target;
        }

        try
        {
            Directory.Move(folder, target.Path!);
            return target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return EditResult.Failed($"이름을 못 바꿨다.\n{ex.Message}");
        }
    }

    public static EditResult Delete(string folder)
    {
        EditResult guard = EnsureEditable(folder);
        if (!guard.Ok)
        {
            return guard;
        }

        try
        {
            Directory.Delete(folder, recursive: true);
            return EditResult.Done(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return EditResult.Failed($"지우지 못했다.\n{ex.Message}");
        }
    }

    /// <summary>고른 파일을 그 칸으로 베껴 넣는다. 참조가 아니라 복사라 원본을 지워도 캐릭터가 안 깨진다.</summary>
    public static EditResult AddFiles(string folder, CharacterSlot slot, IReadOnlyList<string> sources)
    {
        EditResult guard = EnsureEditable(folder);
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
                File.Copy(source, FreeNameIn(destination, System.IO.Path.GetFileName(source)));
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
        EditResult guard = EnsureEditable(folder);
        if (!guard.Ok)
        {
            return guard;
        }

        string destination = slot.In(folder);
        if (!Contains(destination, existing))
        {
            return EditResult.Failed("그 파일은 이 칸에 없다.");
        }

        try
        {
            string target = FreeNameIn(destination, System.IO.Path.GetFileName(source));
            File.Copy(source, target);

            try
            {
                File.Delete(existing);
            }
            catch
            {
                // 옛 파일을 못 지웠으면 새 파일도 도로 치운다.
                // 안 그러면 "바꾸지 못했다" 고 알리면서 실제로는 두 장이 남는다.
                Discard(target);
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
        EditResult guard = EnsureEditable(folder);
        if (!guard.Ok)
        {
            return guard;
        }

        // 하나가 막혔다고 나머지까지 안 빼면, 어디까지 됐는지 알 수 없어진다.
        List<string> stuck = [];
        foreach (string path in paths)
        {
            if (!Discard(path))
            {
                stuck.Add(System.IO.Path.GetFileName(path));
            }
        }

        if (stuck.Count == 0)
        {
            return EditResult.Done(folder);
        }

        return EditResult.Failed($"{stuck.Count}개를 못 뺐다.\n{string.Join(", ", stuck)}{InUseHint(slot)}");
    }

    /// <summary>
    /// 캐릭터 폴더 바로 아래 폴더 전부. <see cref="CharacterLibrary.Scan"/> 과 달리
    /// 아직 그림이 없어 캐릭터로 안 쳐주는 폴더도 준다. 그래야 방금 만든 걸 채워 넣을 수 있다.
    /// </summary>
    public static IReadOnlyList<string> Folders()
    {
        if (!Directory.Exists(CharacterLibrary.UserRoot))
        {
            return [];
        }

        return
        [
            .. Directory
                .EnumerateDirectories(CharacterLibrary.UserRoot)
                .OrderBy(System.IO.Path.GetFileName, StringComparer.OrdinalIgnoreCase)
        ];
    }

    /// <summary>그 칸에 들어 있는 파일. 쓸 수 있는 확장자만 이름순으로 돌려준다.</summary>
    public static IReadOnlyList<string> Contents(string folder, CharacterSlot slot)
    {
        string path = slot.In(folder);
        if (!Directory.Exists(path))
        {
            return [];
        }

        string[] extensions = slot.IsSound ? CharacterFolder.SoundExtensions : CharacterFolder.Extensions;
        return
        [
            .. Directory
                .EnumerateFiles(path)
                .Where(file => extensions.Contains(System.IO.Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                .OrderBy(System.IO.Path.GetFileName, StringComparer.OrdinalIgnoreCase)
        ];
    }

    /// <summary>파일 고르기 대화상자에 걸 필터.</summary>
    public static string FilterFor(CharacterSlot slot)
    {
        string[] extensions = slot.IsSound ? CharacterFolder.SoundExtensions : CharacterFolder.Extensions;
        string patterns = string.Join(";", extensions.Select(extension => "*" + extension));
        return $"{(slot.IsSound ? "소리" : "그림")} ({patterns})|{patterns}";
    }

    /// <summary>
    /// 소리는 첫 소리가 늦지 않게 파일마다 미리 열어둔다(<see cref="SoundBank"/>).
    /// 그래서 지금 쓰고 있는 캐릭터의 소리 파일은 잠겨 있다.
    /// </summary>
    private static string InUseHint(CharacterSlot slot) => slot.IsSound
        ? "\n\n지금 쓰고 있는 캐릭터의 소리는 미리 열어둔 상태라 잠겨 있다. 다른 캐릭터로 바꾼 뒤에 빼야 한다."
        : string.Empty;

    /// <summary>지우기를 시도한다. 잠겨 있으면 false. 여러 개를 뺄 때 하나에 걸려 멈추지 않으려고 쓴다.</summary>
    private static bool Discard(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary><paramref name="inner"/> 가 <paramref name="outer"/> 안에 있거나 같은 곳인가.</summary>
    private static bool Contains(string outer, string inner)
    {
        try
        {
            string parent = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(outer));
            string child = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(inner));

            return string.Equals(parent, child, StringComparison.OrdinalIgnoreCase)
                || child.StartsWith(parent + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            return false;
        }
    }

    private static EditResult EnsureEditable(string folder)
    {
        if (IsBundled(folder))
        {
            return EditResult.Failed("기본 캐릭터는 못 고친다.\n복사본을 떠서 고쳐야 한다.");
        }

        if (IsOutside(folder))
        {
            return EditResult.Failed("캐릭터 폴더 밖에 있는 캐릭터는 여기서 못 고친다.\n폴더를 직접 열어서 고쳐야 한다.");
        }

        return EditResult.Done(folder);
    }

    private static EditResult ResolveNewName(string? name)
    {
        string trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return EditResult.Failed("이름이 비어 있다.");
        }

        if (trimmed.IndexOfAny(Forbidden) >= 0)
        {
            return EditResult.Failed("폴더 이름으로 못 쓰는 글자가 들어 있다.");
        }

        string path = System.IO.Path.Combine(CharacterLibrary.UserRoot, trimmed);
        if (Directory.Exists(path))
        {
            return EditResult.Failed($"[{trimmed}] 은 이미 있다.");
        }

        return EditResult.Done(path);
    }

    /// <summary>같은 이름이 이미 있으면 뒤에 번호를 붙인다. 덮어써서 있던 그림을 날리지 않는다.</summary>
    private static string FreeNameIn(string folder, string fileName)
    {
        string stem = System.IO.Path.GetFileNameWithoutExtension(fileName);
        string extension = System.IO.Path.GetExtension(fileName);
        string candidate = System.IO.Path.Combine(folder, fileName);

        for (int number = 2; File.Exists(candidate); number++)
        {
            candidate = System.IO.Path.Combine(folder, $"{stem} ({number}){extension}");
        }

        return candidate;
    }

    private static void CopyTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, System.IO.Path.Combine(destination, System.IO.Path.GetFileName(file)));
        }

        foreach (string folder in Directory.EnumerateDirectories(source))
        {
            CopyTree(folder, System.IO.Path.Combine(destination, System.IO.Path.GetFileName(folder)));
        }
    }

    private static string NameOf(string folder) => new DirectoryInfo(folder).Name;

    /// <summary>캐릭터 폴더 바로 아래에 있는가. 한 겹 더 들어간 경로나 바깥 경로는 건드리지 않는다.</summary>
    private static bool IsDirectlyInRoot(string folder)
    {
        try
        {
            string? parent = Directory.GetParent(System.IO.Path.GetFullPath(folder))?.FullName;
            return parent is not null && string.Equals(
                System.IO.Path.TrimEndingDirectorySeparator(parent),
                System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(CharacterLibrary.UserRoot)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            return false;
        }
    }
}
