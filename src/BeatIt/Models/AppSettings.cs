using BeatIt.Core;

namespace BeatIt.Models;

/// <summary>화면 좌표로 적어둔 사각형. 설정 파일에 그대로 실린다.</summary>
public sealed class AreaRect
{
    public double Left { get; set; }

    public double Top { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    public AreaRect Clone() => new() { Left = Left, Top = Top, Width = Width, Height = Height };
}

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

    /// <summary>돌아다닐 범위. <see cref="WanderAreaKind.Custom"/> 이면 <see cref="CustomWanderArea"/> 를 쓴다.</summary>
    public WanderAreaKind WanderArea { get; set; } = WanderAreaKind.FullScreen;

    /// <summary>직접 그려둔 이동 영역. 한 번도 안 그렸으면 null 이고, 그때는 화면 전체로 본다.</summary>
    public AreaRect? CustomWanderArea { get; set; }

    /// <summary>걸어다니는 속도의 최소/최대(px/s). 목적지를 새로 고를 때마다 그 사이에서 뽑는다.</summary>
    public double WanderSpeedMin { get; set; } = 70;

    public double WanderSpeedMax { get; set; } = 165;

    /// <summary>목적지에 닿은 뒤 쉬는 시간의 최소/최대(ms).</summary>
    public int WanderRestMinMs { get; set; } = 1500;

    public int WanderRestMaxMs { get; set; } = 5500;

    /// <summary>맞은 뒤 다시 걷기까지 멈춰 있는 시간(ms).</summary>
    public int HitRestMs { get; set; } = 1600;

    /// <summary>끌다 놓은 뒤 다시 걷기까지 멈춰 있는 시간(ms).</summary>
    public int DragRestMs { get; set; } = 800;

    /// <summary>맞았을 때 꾸겨지는 세기. 스프링에 밀어넣는 임펄스다.</summary>
    public double HitPower { get; set; } = 9.5;

    /// <summary>맞았을 때 휘청이는 세기. 좌우 어느 쪽으로 기울지는 매번 랜덤이다.</summary>
    public double HitTilt { get; set; } = 190;

    /// <summary>콤보 한 단계마다 타격이 세지는 비율. 0 이면 몇 콤보든 같은 세기로 맞는다.</summary>
    public double HitComboGain { get; set; } = 0.07;

    /// <summary>맞은 그림을 붙들고 있는 시간(ms). 지나면 idle 로 돌아간다.</summary>
    public int BeatHoldMs { get; set; } = 550;

    /// <summary>끌고 갈 때 몸통이 붙잡은 지점보다 얼마나 뒤처지는지. 0 이면 통째로 따라온다.</summary>
    public double DragLag { get; set; } = 0.85;

    /// <summary>끌려갈 때 늘어나는 한계(0~1). 0 이면 안 늘어난다.</summary>
    public double DragStretch { get; set; } = 0.55;

    /// <summary>놓았을 때 제자리로 돌아오는 스프링의 세기. 클수록 빨리 잡히고 덜 출렁인다.</summary>
    public double DragSpring { get; set; } = 95;

    /// <summary>켜면 끌다 놓을 때 놓은 속도로 날아가고, 이동 영역 경계에 튕긴다.</summary>
    public bool ThrowEnabled { get; set; }

    /// <summary>놓은 속도를 얼마나 부풀려 던질지. 1 이면 커서가 가던 속도 그대로.</summary>
    public double ThrowSpeedScale { get; set; } = 1.0;

    /// <summary>아무리 세게 뿌려도 이 속도를 넘지 않는다(px/s).</summary>
    public double ThrowMaxSpeed { get; set; } = 2600;

    /// <summary>벽에 튕길 때 남는 속도의 비율(0~1). 1 이면 안 죽고 계속 튄다.</summary>
    public double ThrowBounce { get; set; } = 0.6;

    /// <summary>나는 동안 초당 줄어드는 속도의 비율. 클수록 빨리 선다.</summary>
    public double ThrowFriction { get; set; } = 1.6;

    /// <summary>이 속도 밑으로 떨어지면 다 왔다고 보고 멈춘다(px/s).</summary>
    public double ThrowStopSpeed { get; set; } = 40;

    /// <summary>콤보 크기와 색이 한 계단 오르는 간격. 기본 50 이면 50, 100, 150... 에서 바뀐다.</summary>
    public int ComboMilestone { get; set; } = 50;

    /// <summary>계단 하나마다 콤보 숫자가 커지는 비율. 1000 콤보에서 기본값이면 3배가 된다.</summary>
    public double ComboGrowth { get; set; } = 0.10;

    /// <summary>콤보 숫자가 아무리 커져도 넘지 않는 배율. 창이 화면을 다 먹지 않게 잡아둔다.</summary>
    public double ComboMaxScale { get; set; } = 4.0;

    /// <summary>콤보 숫자를 캐릭터 머리 위로 얼마나 띄울지(px).</summary>
    public double ComboGap { get; set; } = 10;

    public bool Topmost { get; set; } = true;

    public bool PositionLocked { get; set; }

    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    /// <summary>
    /// 값을 통째로 베낀다. 설정 창은 매번 복사본을 넘기고, 취소하면 열기 전 복사본으로 되돌린다.
    /// 필드를 하나씩 적어 옮기면 값을 늘릴 때마다 한 줄을 빠뜨릴 수 있는데, 그러면 그 값만
    /// 취소가 안 먹고 미리보기가 안 도는 조용한 버그가 된다. 참조로 든 건 사각형 하나뿐이라
    /// 얕은 복사 뒤 그것만 따로 떠주면 된다.
    /// </summary>
    public AppSettings Clone()
    {
        AppSettings copy = (AppSettings)MemberwiseClone();
        copy.CustomWanderArea = CustomWanderArea?.Clone();
        return copy;
    }
}
