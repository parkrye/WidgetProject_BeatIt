using BeatIt.Models;
using BeatIt.Services;

namespace BeatIt.Core;

/// <summary>설정에 적힌 캐릭터를 읽어 <see cref="ISpriteSource"/> 로 만든다. 못 읽으면 기본 캐릭터, 그것도 없으면 샌드백.</summary>
public static class SpriteSourceFactory
{
    public static ISpriteSource Create(AppSettings settings)
    {
        Character character = TryLoad(settings.CharacterPath)
            ?? TryLoad(CharacterLibrary.DefaultPath)
            ?? Character.Placeholder();

        CharacterSpriteSource source = new(character, settings.IdleMinMs / 1000.0, settings.IdleMaxMs / 1000.0);
        source.SetIdleSoundInterval(settings.IdleSoundMinMs / 1000.0, settings.IdleSoundMaxMs / 1000.0);
        source.SetVolume(settings.SoundVolume, settings.SoundMuted);
        return source;
    }

    private static Character? TryLoad(string? folder) => folder is null ? null : Character.Load(folder);
}
