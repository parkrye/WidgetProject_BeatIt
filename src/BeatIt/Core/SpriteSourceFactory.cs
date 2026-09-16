using BeatIt.Models;

namespace BeatIt.Core;

/// <summary>설정을 보고 알맞은 <see cref="ISpriteSource"/> 를 만든다. 쓸 이미지가 없으면 기본 샌드백으로 떨어진다.</summary>
public static class SpriteSourceFactory
{
    public static ISpriteSource Create(AppSettings settings) =>
        settings.Mode == SpriteMode.Sequence ? CreateSequence(settings) : CreateSingle(settings);

    private static ISpriteSource CreateSingle(AppSettings settings)
    {
        Sprite? sprite = settings.SinglePath is null ? null : Sprite.Load(settings.SinglePath);
        return new SingleSpriteSource(sprite ?? Fallback());
    }

    private static ISpriteSource CreateSequence(AppSettings settings)
    {
        List<Sprite> sprites = [];
        foreach (string path in settings.SequencePaths)
        {
            Sprite? sprite = Sprite.Load(path);
            if (sprite is not null)
            {
                sprites.Add(sprite);
            }
        }

        if (sprites.Count == 0)
        {
            return new SingleSpriteSource(Fallback());
        }

        return new SequenceSpriteSource(sprites);
    }

    private static Sprite Fallback() => Sprite.FromImage(PlaceholderSprite.Create());
}
