using System.IO;

namespace BeatIt.Core;

/// <summary>방향 폴더에서 찾아낸 그림 경로. 방향 폴더를 안 쓴 캐릭터는 전부 default 로 들어온다.</summary>
public sealed record DirectionalPaths(IReadOnlyDictionary<FacingDirection, IReadOnlyList<string>> ByDirection)
{
    public static DirectionalPaths Empty { get; } = new(new Dictionary<FacingDirection, IReadOnlyList<string>>());

    public bool IsEmpty => ByDirection.Count == 0;

    public int Count => ByDirection.Values.Sum(paths => paths.Count);

    /// <summary>들어 있는 방향. <see cref="Facing.All"/> 순서로 돌려준다.</summary>
    public IReadOnlyList<FacingDirection> Directions =>
        [.. Facing.All.Where(ByDirection.ContainsKey)];
}

/// <summary>캐릭터 폴더에서 찾아낸 행동별 소리 경로.</summary>
public sealed record SoundPaths(
    IReadOnlyList<string> Idle,
    IReadOnlyList<string> Move,
    IReadOnlyList<string> Beat)
{
    public static SoundPaths Empty { get; } = new([], [], []);

    public int Count => Idle.Count + Move.Count + Beat.Count;
}

/// <summary>캐릭터 폴더 하나에서 찾아낸 상태별 그림과 소리.</summary>
public sealed record CharacterPaths(
    IReadOnlyList<string> Idle,
    DirectionalPaths Move,
    DirectionalPaths Beat,
    SoundPaths Sounds);

/// <summary>
/// 캐릭터 = idle / move / beat / sounds 하위 폴더를 가진 폴더 하나.
/// move 와 beat 는 그 아래 default·left·right·up·down 으로 한 겹 더 나눌 수 있고,
/// 나누지 않고 그냥 이미지를 두면 default 로 본다. 예전 캐릭터가 그대로 도는 이유다.
/// 없는 건 default 로, default 도 없으면 idle 로 대신한다.
/// </summary>
public static class CharacterFolder
{
    public static readonly string[] Extensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"];

    /// <summary>
    /// 읽어볼 소리 형식. 실제로 날지는 코덱이 정한다.
    /// 못 여는 파일은 <see cref="SoundBank"/> 가 목록에서 빼기 때문에 넉넉하게 훑어도 된다.
    /// </summary>
    public static readonly string[] SoundExtensions =
        [".wav", ".mp3", ".wma", ".m4a", ".aac", ".mp4", ".mpa", ".ogg", ".flac", ".opus"];

    private const string IdleFolder = "idle";
    private const string MoveFolder = "move";
    private const string BeatFolder = "beat";
    private const string SoundsFolder = "sounds";

    private static readonly (string Folder, FacingDirection Direction)[] DirectionFolders =
    [
        ("default", FacingDirection.Default),
        ("left", FacingDirection.Left),
        ("right", FacingDirection.Right),
        ("up", FacingDirection.Up),
        ("down", FacingDirection.Down),
    ];

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
            idle = FilesDirectlyIn(folder, Extensions);
        }

        if (idle.Count == 0)
        {
            return null;
        }

        return new CharacterPaths(
            idle,
            ReadDirectional(folder, MoveFolder),
            ReadDirectional(folder, BeatFolder),
            ReadSounds(folder));
    }

    /// <summary>방향 폴더를 훑는다. 폴더에 그냥 놓인 이미지는 default 에 함께 담는다.</summary>
    private static DirectionalPaths ReadDirectional(string root, string name)
    {
        string? folder = SubfolderNamed(root, name);
        if (folder is null)
        {
            return DirectionalPaths.Empty;
        }

        Dictionary<FacingDirection, IReadOnlyList<string>> found = [];
        foreach ((string sub, FacingDirection direction) in DirectionFolders)
        {
            string? path = SubfolderNamed(folder, sub);
            if (path is null)
            {
                continue;
            }

            IReadOnlyList<string> images = FilesDirectlyIn(path, Extensions);
            if (images.Count > 0)
            {
                found[direction] = images;
            }
        }

        IReadOnlyList<string> loose = FilesDirectlyIn(folder, Extensions);
        if (loose.Count > 0)
        {
            found[FacingDirection.Default] = found.TryGetValue(FacingDirection.Default, out IReadOnlyList<string>? shared)
                ? [.. shared, .. loose]
                : loose;
        }

        return found.Count == 0 ? DirectionalPaths.Empty : new DirectionalPaths(found);
    }

    private static SoundPaths ReadSounds(string root)
    {
        string? folder = SubfolderNamed(root, SoundsFolder);
        if (folder is null)
        {
            return SoundPaths.Empty;
        }

        return new SoundPaths(
            SoundsIn(folder, IdleFolder),
            SoundsIn(folder, MoveFolder),
            SoundsIn(folder, BeatFolder));
    }

    private static IReadOnlyList<string> ImagesIn(string root, string name)
    {
        string? matched = SubfolderNamed(root, name);
        return matched is null ? [] : FilesDirectlyIn(matched, Extensions);
    }

    private static IReadOnlyList<string> SoundsIn(string root, string name)
    {
        string? matched = SubfolderNamed(root, name);
        return matched is null ? [] : FilesDirectlyIn(matched, SoundExtensions);
    }

    private static string? SubfolderNamed(string root, string name) =>
        Directory
            .EnumerateDirectories(root)
            .FirstOrDefault(path => Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> FilesDirectlyIn(string folder, string[] extensions) =>
    [
        .. Directory
            .EnumerateFiles(folder)
            .Where(path => extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
    ];
}
