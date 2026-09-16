using System.IO;
using BeatIt.Core;

namespace BeatIt.Services;

/// <summary>캐릭터 폴더 하나의 요약. 설정 창 목록에 쓴다.</summary>
public sealed record CharacterInfo(string Name, string Path, int IdleCount, int MoveCount, int BeatCount)
{
    /// <summary>move/beat 폴더가 없어 idle 로 대신하는 경우 개수를 0으로 채워 보낸다.</summary>
    public string Summary =>
        $"idle {IdleCount}장 / move {Describe(MoveCount)} / beat {Describe(BeatCount)}";

    public override string ToString() => Name;

    private static string Describe(int count) => count > 0 ? $"{count}장" : "없음(idle 사용)";
}

/// <summary>기본 제공 캐릭터와 사용자가 넣어둔 캐릭터를 찾아준다.</summary>
public static class CharacterLibrary
{
    /// <summary>실행 파일 옆에 따라오는 기본 캐릭터들.</summary>
    public static string BundledRoot { get; } =
        Path.Combine(AppContext.BaseDirectory, "assets", "characters");

    /// <summary>사용자가 캐릭터 폴더를 복사해 넣는 곳.</summary>
    public static string UserRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BeatIt",
        "characters");

    public static string? DefaultPath => Scan().FirstOrDefault()?.Path;

    /// <summary>기본 폴더와 사용자 폴더를 훑어 쓸 수 있는 캐릭터만 돌려준다.</summary>
    public static IReadOnlyList<CharacterInfo> Scan()
    {
        List<CharacterInfo> found = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (string root in (string[])[BundledRoot, UserRoot])
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (string folder in Directory.EnumerateDirectories(root).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                CharacterInfo? info = Describe(folder);
                if (info is not null && seen.Add(info.Path))
                {
                    found.Add(info);
                }
            }
        }

        return found;
    }

    /// <summary>쓸 이미지가 없는 폴더면 null.</summary>
    public static CharacterInfo? Describe(string folder)
    {
        CharacterPaths? paths = CharacterFolder.Read(folder);
        if (paths is null)
        {
            return null;
        }

        int move = ReferenceEquals(paths.Move, paths.Idle) ? 0 : paths.Move.Count;
        int beat = ReferenceEquals(paths.Beat, paths.Idle) ? 0 : paths.Beat.Count;
        return new CharacterInfo(new DirectoryInfo(folder).Name, Path.GetFullPath(folder), paths.Idle.Count, move, beat);
    }

    public static void EnsureUserRoot() => Directory.CreateDirectory(UserRoot);
}
