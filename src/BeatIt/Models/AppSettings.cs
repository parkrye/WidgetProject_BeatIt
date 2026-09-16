namespace BeatIt.Models;

/// <summary>타격 이미지를 어떻게 다룰지 결정하는 모드.</summary>
public enum SpriteMode
{
    /// <summary>이미지 한 장을 꾸겨가며 사용한다.</summary>
    Single,

    /// <summary>타격마다 다음 이미지로 교체한다. 장수 제한 없음.</summary>
    Sequence,
}

/// <summary>디스크에 저장되는 위젯 설정.</summary>
public sealed class AppSettings
{
    public SpriteMode Mode { get; set; } = SpriteMode.Single;

    /// <summary>Single 모드에서 사용할 이미지 경로.</summary>
    public string? SinglePath { get; set; }

    /// <summary>Sequence 모드에서 순서대로 순환할 이미지 경로 목록.</summary>
    public List<string> SequencePaths { get; set; } = [];

    /// <summary>위젯의 가로 길이(px). 세로는 이미지 비율로 결정된다.</summary>
    public double WidgetWidth { get; set; } = 220;

    /// <summary>이 시간 안에 다시 때리면 콤보가 이어진다(ms).</summary>
    public int ComboTimeoutMs { get; set; } = 1200;

    public bool Topmost { get; set; } = true;

    public bool PositionLocked { get; set; }

    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public AppSettings Clone() => new()
    {
        Mode = Mode,
        SinglePath = SinglePath,
        SequencePaths = [.. SequencePaths],
        WidgetWidth = WidgetWidth,
        ComboTimeoutMs = ComboTimeoutMs,
        Topmost = Topmost,
        PositionLocked = PositionLocked,
        WindowLeft = WindowLeft,
        WindowTop = WindowTop,
    };
}
