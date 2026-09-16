using System.IO;

namespace BeatIt.Core;

/// <summary>캐릭터 폴더 하나에서 찾아낸 상태별 이미지 경로.</summary>
public sealed record CharacterPaths(
    IReadOnlyList<string> Idle,
    IReadOnlyList<string> Move,
    IReadOnlyList<string> Beat);

/// <summary>
/// 캐릭터 = idle / move / beat 하위 폴더를 가진 폴더 하나.
/// 없는 상태는 idle 로 대신하고, 하위 폴더가 아예 없으면 폴더에 바로 있는 이미지를 idle 로 본다.
/// </summary>
public static class CharacterFolder
{
    public static readonly string[] Extensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"];

    private const string IdleFolder = "idle";
    private const string MoveFolder = "move";
    private const string BeatFolder = "beat";

    /// <summary>쓸 이미지가 하나도 없으면 null.</summary>
    public static CharacterPaths? Read(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return null;
        }

        IReadOnlyList<string> idle = ImagesIn(folder, IdleFolder);
        if (idle.Count == 0)
        {
            idle = ImagesDirectlyIn(folder);
        }

        if (idle.Count == 0)
        {
            return null;
        }

        IReadOnlyList<string> move = ImagesIn(folder, MoveFolder);
        IReadOnlyList<string> beat = ImagesIn(folder, BeatFolder);

        return new CharacterPaths(idle, move.Count > 0 ? move : idle, beat.Count > 0 ? beat : idle);
    }

    private static IReadOnlyList<string> ImagesIn(string root, string name)
    {
        string? matched = Directory
            .EnumerateDirectories(root)
            .FirstOrDefault(path => Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase));

        return matched is null ? [] : ImagesDirectlyIn(matched);
    }

    private static IReadOnlyList<string> ImagesDirectlyIn(string folder) =>
    [
        .. Directory
            .EnumerateFiles(folder)
            .Where(path => Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
    ];
}
