using System.Windows.Media;

namespace BeatIt.Core;

/// <summary>
/// 콤보 수 하나가 얼마나 크고 무슨 색으로 그려질지.
/// 그리는 쪽(<c>ComboDisplay</c>)과 자리를 잡는 쪽(창)이 같은 답을 봐야 해서 한 군데에 둔다.
/// 창은 콤보가 커진 만큼 위로 자리를 비워줘야 하는데, 그 높이를 따로 계산하면 언젠가 어긋난다.
///
/// 크기는 <see cref="Milestone"/> 마다 계단으로 뛴다. 한 대마다 조금씩 커지면 49 와 50 이
/// 구별되지 않아서, 50 이 사건처럼 느껴지라고 끊어 올린다.
/// </summary>
public sealed class ComboStyle
{
    /// <summary>색 스펙트럼을 다 쓰는 데 걸리는 계단 수. 기본 간격 50 이면 1000 콤보다.</summary>
    public const int SpectrumTiers = 20;

    /// <summary>스펙트럼을 다 쓴 뒤에도 계속 자라긴 하되, 이 비율로 느리게 자란다.</summary>
    private const double SlowFactor = 0.2;

    /// <summary>무지개의 채도. 1 로 놓으면 형광색이 번갈아 터져서 오래 보고 있기 힘들다.</summary>
    private const double RainbowSaturation = 0.55;

    /// <summary>흰색에서 시작해 붉게, 끝으로 갈수록 분홍으로. 이 사이를 계단 수만큼 나눠 쓴다.</summary>
    private static readonly Color[] Spectrum =
    [
        Color.FromRgb(0xFF, 0xFF, 0xFF),
        Color.FromRgb(0xFF, 0xE0, 0x66),
        Color.FromRgb(0xFF, 0xA5, 0x2B),
        Color.FromRgb(0xFF, 0x5C, 0x2B),
        Color.FromRgb(0xFF, 0x2B, 0x5C),
    ];

    /// <summary>콤보 숫자 밑에 붙는 라벨의 크기 비율.</summary>
    public const double LabelRatio = 0.3;

    /// <summary>계단이 오르는 자리에서 튀어오르는 배율. 평소보다 확 커야 사건처럼 보인다.</summary>
    public const double MilestonePop = 1.9;

    private const double BasePop = 1.35;

    /// <summary>계단 중간에 때릴 때 나올 수 있는 가장 큰 팝 배율.</summary>
    public const double MaxRegularPop = BasePop + 0.30;

    /// <summary>계단을 안 탄 상태의 숫자 크기.</summary>
    public double BaseSize { get; set; } = 52;

    /// <summary>크기와 색이 한 계단 오르는 간격.</summary>
    public int Milestone { get; set; } = 50;

    /// <summary>계단 하나마다 커지는 비율.</summary>
    public double Growth { get; set; } = 0.10;

    /// <summary>아무리 자라도 넘지 않는 배율.</summary>
    public double MaxScale { get; set; } = 4.0;

    /// <summary>몇 번째 계단인지. 간격이 0 이면 계단을 안 탄다.</summary>
    public int TierOf(int combo) => Milestone <= 0 ? 0 : Math.Max(0, combo) / Milestone;

    /// <summary>이 콤보에서 계단이 막 올라갔는지. 여기서 한 번 크게 연출한다.</summary>
    public bool IsMilestone(int combo) => Milestone > 0 && combo >= Milestone && combo % Milestone == 0;

    /// <summary>스펙트럼을 다 써서 무지개로 넘어갔는지.</summary>
    public bool IsRainbow(int combo) => TierOf(combo) >= SpectrumTiers;

    public double SizeFor(int combo) => BaseSize * ScaleFor(combo);

    /// <summary>이번 타격에서 튀어오를 배율.</summary>
    public double PopFor(int combo, bool milestone) =>
        milestone ? MilestonePop : Math.Min(BasePop + (Math.Min(combo, 30) * 0.01), MaxRegularPop);

    /// <summary>
    /// 이 계단을 띄우려면 캐릭터 머리 위에 얼마나 비워둬야 하는지.
    /// 튀어오르는 몫까지 넣어야 꼭대기가 창 밖으로 잘리지 않는다. 계단 1부터는 그 자리가
    /// 곧 마일스톤이라 큰 팝이 나오고, 계단 0 에서는 그럴 일이 없어 덜 잡아도 된다.
    /// </summary>
    public double ReserveFor(int tier) =>
        HeightFor(Math.Max(0, tier) * Math.Max(1, Milestone)) * (tier >= 1 ? MilestonePop : MaxRegularPop);

    /// <summary>숫자와 라벨을 합친 높이. 창은 이만큼을 캐릭터 머리 위에 비워둬야 한다.</summary>
    public double HeightFor(int combo) => SizeFor(combo) * (1 + LabelRatio);

    /// <summary>
    /// 이 콤보의 색. 스펙트럼을 다 쓰기 전까지는 계단마다 조금씩 붉어지고,
    /// 다 쓴 뒤에는 무지개가 도니 이 값은 그 첫 색이 된다.
    /// </summary>
    public Color ColorFor(int combo)
    {
        double progress = Math.Clamp(TierOf(combo) / (double)SpectrumTiers, 0, 1);
        double scaled = progress * (Spectrum.Length - 1);
        int index = Math.Min((int)scaled, Spectrum.Length - 2);
        return Lerp(Spectrum[index], Spectrum[index + 1], scaled - index);
    }

    /// <summary>무지개 한 바퀴 중 <paramref name="progress"/> (0~1) 지점의 색.</summary>
    public static Color Rainbow(double progress) => FromHue(progress * 360, RainbowSaturation, 1);

    private double ScaleFor(int combo)
    {
        int tier = TierOf(combo);
        double scale = tier <= SpectrumTiers
            ? 1 + (tier * Growth)
            : 1 + (SpectrumTiers * Growth) + ((tier - SpectrumTiers) * Growth * SlowFactor);

        return Math.Clamp(scale, 1, Math.Max(1, MaxScale));
    }

    private static Color Lerp(Color from, Color to, double amount)
    {
        double t = Math.Clamp(amount, 0, 1);
        return Color.FromRgb(
            (byte)(from.R + ((to.R - from.R) * t)),
            (byte)(from.G + ((to.G - from.G) * t)),
            (byte)(from.B + ((to.B - from.B) * t)));
    }

    private static Color FromHue(double degrees, double saturation, double value)
    {
        double hue = ((degrees % 360) + 360) % 360 / 60;
        double chroma = value * saturation;
        double second = chroma * (1 - Math.Abs((hue % 2) - 1));
        double floor = value - chroma;

        (double r, double g, double b) = (int)hue switch
        {
            0 => (chroma, second, 0.0),
            1 => (second, chroma, 0.0),
            2 => (0.0, chroma, second),
            3 => (0.0, second, chroma),
            4 => (second, 0.0, chroma),
            _ => (chroma, 0.0, second),
        };

        return Color.FromRgb(
            (byte)Math.Round((r + floor) * 255),
            (byte)Math.Round((g + floor) * 255),
            (byte)Math.Round((b + floor) * 255));
    }
}
