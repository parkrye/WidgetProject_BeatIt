namespace BeatIt.Models;

/// <summary>디스크에 저장되는 위젯 설정.</summary>
public sealed class AppSettings
{
    /// <summary>쓸 캐릭터 폴더. null 이면 기본 캐릭터를 쓴다.</summary>
    public string? CharacterPath { get; set; }

    /// <summary>타격 이펙트와 콤보 그림이 들어 있는 테마 폴더. null 이면 기본 테마를 쓴다.</summary>
    public string? ThemePath { get; set; }

    /// <summary>위젯의 가로 길이(px). 세로는 이미지 비율로 결정된다.</summary>
    public double WidgetWidth { get; set; } = 220;

    /// <summary>이 시간 안에 다시 때리면 콤보가 이어진다(ms).</summary>
    public int ComboTimeoutMs { get; set; } = 1200;

    /// <summary>대기 중 idle 이미지를 갈아 끼우는 간격의 최소/최대(ms). 그 사이에서 매번 다시 뽑는다.</summary>
    public int IdleMinMs { get; set; } = 2000;

    public int IdleMaxMs { get; set; } = 5000;

    /// <summary>콤보 표시를 기본 위치(위젯 위쪽 가운데)에서 얼마나 밀지(px).</summary>
    public double ComboOffsetX { get; set; }

    public double ComboOffsetY { get; set; }

    /// <summary>콤보 숫자 크기. 숫자 이미지를 쓸 때는 그 높이가 된다.</summary>
    public double ComboSize { get; set; } = 52;

    /// <summary>소리 크기(0~1). 캐릭터 폴더의 sounds 에 넣어둔 소리에 적용된다.</summary>
    public double SoundVolume { get; set; } = 0.6;

    /// <summary>켜면 볼륨과 무관하게 아무 소리도 안 난다.</summary>
    public bool SoundMuted { get; set; }

    /// <summary>대기 중 소리를 내는 간격의 최소/최대(ms). idle 이미지 교체와 따로 돈다.</summary>
    public int IdleSoundMinMs { get; set; } = 10000;

    public int IdleSoundMaxMs { get; set; } = 30000;

    /// <summary>끄면 때려도 이펙트가 안 뜬다.</summary>
    public bool EffectsEnabled { get; set; } = true;

    /// <summary>켜면 위젯이 혼자 화면을 돌아다닌다.</summary>
    public bool Wander { get; set; }

    public bool Topmost { get; set; } = true;

    public bool PositionLocked { get; set; }

    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public AppSettings Clone() => new()
    {
        CharacterPath = CharacterPath,
        ThemePath = ThemePath,
        WidgetWidth = WidgetWidth,
        ComboTimeoutMs = ComboTimeoutMs,
        IdleMinMs = IdleMinMs,
        IdleMaxMs = IdleMaxMs,
        ComboOffsetX = ComboOffsetX,
        ComboOffsetY = ComboOffsetY,
        ComboSize = ComboSize,
        SoundVolume = SoundVolume,
        SoundMuted = SoundMuted,
        IdleSoundMinMs = IdleSoundMinMs,
        IdleSoundMaxMs = IdleSoundMaxMs,
        EffectsEnabled = EffectsEnabled,
        Wander = Wander,
        Topmost = Topmost,
        PositionLocked = PositionLocked,
        WindowLeft = WindowLeft,
        WindowTop = WindowTop,
    };
}
