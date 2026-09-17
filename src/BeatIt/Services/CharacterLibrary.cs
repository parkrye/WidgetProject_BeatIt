using System.IO;
using System.Text;
using BeatIt.Core;

namespace BeatIt.Services;

/// <summary>캐릭터 폴더 하나의 요약. 설정 창 목록에 쓴다.</summary>
public sealed record CharacterInfo(
    string Name,
    string Path,
    int IdleCount,
    int MoveCount,
    IReadOnlyList<FacingDirection> MoveDirections,
    int BeatCount,
    IReadOnlyList<FacingDirection> BeatDirections,
    int SoundCount)
{
    private static readonly (FacingDirection Direction, string Mark)[] Marks =
    [
        (FacingDirection.Left, "←"),
        (FacingDirection.Right, "→"),
        (FacingDirection.Up, "↑"),
        (FacingDirection.Down, "↓"),
    ];

    /// <summary>move/beat 폴더가 없어 idle 로 대신하는 경우 "없음"으로 적는다.</summary>
    public string Summary
    {
        get
        {
            StringBuilder text = new();
            text.Append("idle ").Append(IdleCount).Append("장 / move ");
            text.Append(Describe(MoveCount, MoveDirections));
            text.Append(" / beat ").Append(Describe(BeatCount, BeatDirections));
            if (SoundCount > 0)
            {
                text.Append(" / 소리 ").Append(SoundCount).Append('개');
            }

            return text.ToString();
        }
    }

    public override string ToString() => Name;

    private static string Describe(int count, IReadOnlyList<FacingDirection> directions)
    {
        if (count == 0)
        {
            return "없음(idle 사용)";
        }

        StringBuilder text = new();
        text.Append(count).Append('장');

        StringBuilder marks = new();
        foreach ((FacingDirection direction, string mark) in Marks)
        {
            if (directions.Contains(direction))
            {
                marks.Append(mark);
            }
        }

        if (marks.Length > 0)
        {
            text.Append(' ').Append('(').Append(marks).Append(')');
        }

        return text.ToString();
    }
}

/// <summary>기본 제공 캐릭터와 사용자가 넣어둔 캐릭터를 찾아준다.</summary>
public static class CharacterLibrary
{
    /// <summary>캐릭터가 모여 사는 곳. 기본 캐릭터도 첫 실행 때 여기로 풀린다.</summary>
    public static string UserRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BeatIt",
        "characters");

    public static string? DefaultPath => Scan().FirstOrDefault()?.Path;

    /// <summary>캐릭터 폴더를 훑어 쓸 수 있는 것만 돌려준다.</summary>
    public static IReadOnlyList<CharacterInfo> Scan()
    {
        if (!Directory.Exists(UserRoot))
        {
            return [];
        }

        List<CharacterInfo> found = [];
        foreach (string folder in Directory.EnumerateDirectories(UserRoot).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            CharacterInfo? info = Describe(folder);
            if (info is not null)
            {
                found.Add(info);
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

        return new CharacterInfo(
            new DirectoryInfo(folder).Name,
            Path.GetFullPath(folder),
            paths.Idle.Count,
            paths.Move.Count,
            paths.Move.Directions,
            paths.Beat.Count,
            paths.Beat.Directions,
            paths.Sounds.Count);
    }

    public static void EnsureUserRoot() => Directory.CreateDirectory(UserRoot);
}
