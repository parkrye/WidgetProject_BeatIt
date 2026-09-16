namespace BeatIt.Models;

/// <summary>디스크에 저장되는 위젯 설정.</summary>
public sealed class AppSettings
{
    /// <summary>쓸 캐릭터 폴더. null 이면 기본 제공 캐릭터를 쓴다.</summary>
    public string? CharacterPath { get; set; }

    /// <summary>위젯의 가로 길이(px). 세로는 이미지 비율로 결정된다.</summary>
    public double WidgetWidth { get; set; } = 220;

    /// <summary>이 시간 안에 다시 때리면 콤보가 이어진다(ms).</summary>
    public int ComboTimeoutMs { get; set; } = 1200;

    /// <summary>대기 중 idle 이미지를 갈아 끼우는 간격의 최소/최대(ms). 그 사이에서 매번 다시 뽑는다.</summary>
    public int IdleMinMs { get; set; } = 2000;

    public int IdleMaxMs { get; set; } = 5000;

    public bool Topmost { get; set; } = true;

    public bool PositionLocked { get; set; }

    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public AppSettings Clone() => new()
    {
        CharacterPath = CharacterPath,
        WidgetWidth = WidgetWidth,
        ComboTimeoutMs = ComboTimeoutMs,
        IdleMinMs = IdleMinMs,
        IdleMaxMs = IdleMaxMs,
        Topmost = Topmost,
        PositionLocked = PositionLocked,
        WindowLeft = WindowLeft,
        WindowTop = WindowTop,
    };
}
