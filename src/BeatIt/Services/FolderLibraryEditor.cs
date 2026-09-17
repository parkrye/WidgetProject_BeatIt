using System.IO;

namespace BeatIt.Services;

/// <summary>고치기를 시도한 결과. 실패하면 <see cref="Problem"/> 에 사람이 읽을 이유가 담긴다.</summary>
public sealed record EditResult(string? Path, string? Problem)
{
    public bool Ok => Problem is null;

    public static EditResult Done(string path) => new(path, null);

    public static EditResult Failed(string problem) => new(null, problem);
}

/// <summary>
/// 이름 붙은 폴더가 한 자리에 모여 사는 묶음(캐릭터, 테마)을 앱 안에서 만들고 고친다.
/// 손대는 곳은 전부 <paramref name="root"/> **바로 아래**이고, 그 밖을 가리키는 경로는 거절한다.
/// 기본으로 딸려오는 것은 버전마다 앱이 다시 풀어놓아서, 고쳐봐야 되살아나니 잠가둔다.
/// </summary>
/// <param name="root">그 묶음이 모여 사는 폴더.</param>
/// <param name="bundled">exe 가 넣어준 이름들. 처음 물어볼 때 zip 을 읽으므로 그때 부른다.</param>
/// <param name="what">사람에게 보여줄 이름. "캐릭터", "테마".</param>
public sealed class FolderLibraryEditor(string root, Func<IReadOnlySet<string>> bundled, string what)
{
    private static readonly char[] Forbidden = Path.GetInvalidFileNameChars();

    /// <summary>exe 가 넣어준 기본 폴더인가. 이건 고치지도 지우지도 못한다.</summary>
    public bool IsBundled(string folder) => IsDirectlyInRoot(folder) && bundled().Contains(NameOf(folder));

    /// <summary>모여 사는 자리 밖에 있는가. 직접 고른 폴더가 여기 해당한다.</summary>
    public bool IsOutside(string folder) => !IsDirectlyInRoot(folder);

    /// <summary>빈 폴더 하나를 만든다. <paramref name="seed"/> 를 주면 그 하위 폴더까지 같이 판다.</summary>
    public EditResult Create(string? name, string? seed)
    {
        EditResult target = ResolveNewName(name);
        if (!target.Ok)
        {
            return target;
        }

        try
        {
            Directory.CreateDirectory(seed is null ? target.Path! : Path.Combine(target.Path!, seed));
            return target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return EditResult.Failed($"폴더를 못 만들었다.\n{ex.Message}");
        }
    }

    /// <summary>있는 걸 통째로 베낀다. 기본으로 딸려온 걸 고치고 싶을 때 쓴다.</summary>
    public EditResult Duplicate(string source, string? name)
    {
        if (!Directory.Exists(source))
        {
            return EditResult.Failed($"베낄 {what} 폴더가 없다.");
        }

        EditResult target = ResolveNewName(name);
        if (!target.Ok)
        {
            return target;
        }

        // 모여 사는 폴더 자신이나 그 위를 고르면 목적지가 원본 안에 들어간다.
        // 그대로 두면 자기 자신을 끝없이 베끼다가 경로 길이에서 터진다.
        if (Contains(source, target.Path!))
        {
            return EditResult.Failed($"그 폴더 안으로는 못 가져온다.\n{what} 폴더 자신이나 그 위를 고른 것 같다.");
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

    public EditResult Rename(string folder, string? name)
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

    public EditResult Delete(string folder)
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

    /// <summary>
    /// 모여 사는 폴더 바로 아래 전부. 읽을 게 없어 목록에 안 잡히는 폴더도 준다.
    /// 그래야 방금 만든 빈 폴더를 채워 넣을 수 있다.
    /// </summary>
    public IReadOnlyList<string> Folders()
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        return [.. Directory.EnumerateDirectories(root).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>고칠 수 있는 자리인지. 기본으로 딸려온 것과 바깥 폴더는 막는다.</summary>
    public EditResult EnsureEditable(string folder)
    {
        if (IsBundled(folder))
        {
            return EditResult.Failed($"기본 {what}는 못 고친다.\n복사본을 떠서 고쳐야 한다.");
        }

        if (IsOutside(folder))
        {
            return EditResult.Failed($"{what} 폴더 밖에 있는 건 여기서 못 고친다.\n폴더를 직접 열어서 고쳐야 한다.");
        }

        return EditResult.Done(folder);
    }

    /// <summary>그 폴더에 들어 있는 파일. 쓸 수 있는 확장자만 이름순으로 돌려준다.</summary>
    public static IReadOnlyList<string> FilesIn(string folder, IReadOnlyList<string> extensions)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return
        [
            .. Directory
                .EnumerateFiles(folder)
                .Where(file => extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
        ];
    }

    /// <summary>같은 이름이 이미 있으면 뒤에 번호를 붙인다. 덮어써서 있던 그림을 날리지 않는다.</summary>
    public static string FreeNameIn(string folder, string fileName)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        string candidate = Path.Combine(folder, fileName);

        for (int number = 2; File.Exists(candidate); number++)
        {
            candidate = Path.Combine(folder, $"{stem} ({number}){extension}");
        }

        return candidate;
    }

    /// <summary>지우기를 시도한다. 잠겨 있으면 false. 여러 개를 뺄 때 하나에 걸려 멈추지 않으려고 쓴다.</summary>
    public static bool Discard(string path)
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
    public static bool Contains(string outer, string inner)
    {
        try
        {
            string parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outer));
            string child = Path.TrimEndingDirectorySeparator(Path.GetFullPath(inner));

            return string.Equals(parent, child, StringComparison.OrdinalIgnoreCase)
                || child.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            return false;
        }
    }

    /// <summary>파일 고르기 대화상자에 걸 필터 한 줄.</summary>
    public static string Filter(string label, IReadOnlyList<string> extensions)
    {
        string patterns = string.Join(";", extensions.Select(extension => "*" + extension));
        return $"{label} ({patterns})|{patterns}";
    }

    private EditResult ResolveNewName(string? name)
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

        string path = Path.Combine(root, trimmed);
        if (Directory.Exists(path))
        {
            return EditResult.Failed($"[{trimmed}] 은 이미 있다.");
        }

        return EditResult.Done(path);
    }

    /// <summary>모여 사는 폴더 바로 아래에 있는가. 한 겹 더 들어간 경로나 바깥 경로는 건드리지 않는다.</summary>
    private bool IsDirectlyInRoot(string folder)
    {
        try
        {
            string? parent = Directory.GetParent(Path.GetFullPath(folder))?.FullName;
            return parent is not null && string.Equals(
                Path.TrimEndingDirectorySeparator(parent),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            return false;
        }
    }

    private static void CopyTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (string folder in Directory.EnumerateDirectories(source))
        {
            CopyTree(folder, Path.Combine(destination, Path.GetFileName(folder)));
        }
    }

    private static string NameOf(string folder) => new DirectoryInfo(folder).Name;
}
