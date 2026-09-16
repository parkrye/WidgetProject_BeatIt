using System.IO;

namespace BeatIt.Core;

/// <summary>상태별 이미지 묶음 하나. 폴더에서 읽어 들인다.</summary>
public sealed class Character : IDisposable
{
    private Character(
        string name,
        IReadOnlyList<Sprite> idle,
        IReadOnlyList<Sprite> move,
        IReadOnlyList<Sprite> beat)
    {
        Name = name;
        Idle = idle;
        Move = move;
        Beat = beat;

        // move/beat 가 비어서 idle 을 대신 쓰는 경우가 있어 같은 Sprite 가 여러 목록에 들어간다.
        // 구독과 정리는 중복 없이 한 번씩만 해야 한다.
        All = [.. idle.Concat(move).Concat(beat).Distinct(ReferenceEqualityComparer.Instance).Cast<Sprite>()];
    }

    public string Name { get; }

    public IReadOnlyList<Sprite> Idle { get; }

    public IReadOnlyList<Sprite> Move { get; }

    public IReadOnlyList<Sprite> Beat { get; }

    /// <summary>중복 없는 전체 스프라이트. 구독/해제와 정리에 쓴다.</summary>
    public IReadOnlyList<Sprite> All { get; }

    /// <summary>읽을 수 있는 이미지가 하나도 없으면 null.</summary>
    public static Character? Load(string folder)
    {
        CharacterPaths? paths = CharacterFolder.Read(folder);
        if (paths is null)
        {
            return null;
        }

        List<Sprite> idle = LoadAll(paths.Idle);
        if (idle.Count == 0)
        {
            return null;
        }

        List<Sprite> move = ReferenceEquals(paths.Move, paths.Idle) ? idle : LoadAll(paths.Move);
        List<Sprite> beat = ReferenceEquals(paths.Beat, paths.Idle) ? idle : LoadAll(paths.Beat);

        string name = new DirectoryInfo(folder).Name;
        return new Character(name, idle, move.Count > 0 ? move : idle, beat.Count > 0 ? beat : idle);
    }

    /// <summary>캐릭터를 못 찾았을 때 대신 맞아줄 기본 샌드백.</summary>
    public static Character Placeholder()
    {
        List<Sprite> only = [Sprite.FromImage(PlaceholderSprite.Create())];
        return new Character("기본 샌드백", only, only, only);
    }

    public void Dispose()
    {
        foreach (Sprite sprite in All)
        {
            sprite.Dispose();
        }
    }

    private static List<Sprite> LoadAll(IReadOnlyList<string> paths)
    {
        List<Sprite> sprites = [];
        foreach (string path in paths)
        {
            Sprite? sprite = Sprite.Load(path);
            if (sprite is not null)
            {
                sprites.Add(sprite);
            }
        }

        return sprites;
    }
}
