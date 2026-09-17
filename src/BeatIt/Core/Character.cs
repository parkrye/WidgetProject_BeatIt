using System.IO;

namespace BeatIt.Core;

/// <summary>상태별 이미지와 소리 묶음 하나. 폴더에서 읽어 들인다.</summary>
public sealed class Character : IDisposable
{
    private Character(
        string name,
        IReadOnlyList<Sprite> idle,
        DirectionalSprites move,
        DirectionalSprites beat,
        CharacterAudio audio)
    {
        Name = name;
        Idle = idle;
        Move = move;
        Beat = beat;
        Audio = audio;

        // move/beat 가 비어서 idle 을 대신 쓰는 경우가 있어 같은 Sprite 가 여러 목록에 들어간다.
        // 구독과 정리는 중복 없이 한 번씩만 해야 한다.
        All =
        [
            .. idle
                .Concat(move.Own)
                .Concat(beat.Own)
                .Distinct(ReferenceEqualityComparer.Instance)
                .Cast<Sprite>()
        ];
    }

    public string Name { get; }

    public IReadOnlyList<Sprite> Idle { get; }

    public DirectionalSprites Move { get; }

    public DirectionalSprites Beat { get; }

    public CharacterAudio Audio { get; private set; }

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

        CharacterAudio audio = new(
            SoundBank.Load(paths.Sounds.Idle),
            SoundBank.Load(paths.Sounds.Move),
            SoundBank.Load(paths.Sounds.Beat));

        string name = new DirectoryInfo(folder).Name;
        return new Character(name, idle, LoadDirectional(paths.Move, idle), LoadDirectional(paths.Beat, idle), audio);
    }

    /// <summary>캐릭터를 못 찾았을 때 대신 맞아줄 기본 샌드백.</summary>
    public static Character Placeholder()
    {
        List<Sprite> only = [Sprite.FromImage(PlaceholderSprite.Create())];
        return new Character(
            "기본 샌드백",
            only,
            DirectionalSprites.Fallback(only),
            DirectionalSprites.Fallback(only),
            CharacterAudio.Silent);
    }

    /// <summary>
    /// 쥐고 있던 소리 파일을 놓는다. 그림은 그대로라 위젯은 계속 떠 있고, 소리만 조용해진다.
    /// 캐릭터 폴더를 고치는 동안 파일이 잠겨 있지 않게 하려는 것이고, 고치고 나면 통째로 다시 읽는다.
    /// </summary>
    public void ReleaseAudio()
    {
        if (ReferenceEquals(Audio, CharacterAudio.Silent))
        {
            return;
        }

        CharacterAudio released = Audio;
        Audio = CharacterAudio.Silent;
        released.Dispose();
    }

    public void Dispose()
    {
        foreach (Sprite sprite in All)
        {
            sprite.Dispose();
        }

        ReleaseAudio();
    }

    private static DirectionalSprites LoadDirectional(DirectionalPaths paths, IReadOnlyList<Sprite> fallback)
    {
        Dictionary<FacingDirection, IReadOnlyList<Sprite>> loaded = [];
        foreach ((FacingDirection direction, IReadOnlyList<string> files) in paths.ByDirection)
        {
            List<Sprite> sprites = LoadAll(files);
            if (sprites.Count > 0)
            {
                loaded[direction] = sprites;
            }
        }

        return new DirectionalSprites(loaded, fallback);
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
