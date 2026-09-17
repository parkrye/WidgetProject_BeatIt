using System.Windows.Media;

namespace BeatIt.Core;

/// <summary>맞은 순간 꾸겨졌다가 스프링으로 튀어 돌아온다. 콤보가 높을수록 세게 꾸겨진다.</summary>
public sealed class HitAnimator
{
    private const double MaxSquash = 0.62;
    private const double MaxStretch = -0.45;
    private const double MaxTiltDegrees = 26;

    private readonly Random _random = new();
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly RotateTransform _rotate = new(0);
    private readonly TransformGroup _transform;

    private Spring _squash = new(stiffness: 430, damping: 13);
    private Spring _tilt = new(stiffness: 330, damping: 11);

    public HitAnimator()
    {
        _transform = new TransformGroup();
        _transform.Children.Add(_scale);
        _transform.Children.Add(_rotate);
    }

    public Transform Transform => _transform;

    /// <summary>한 대 맞을 때 꾸겨지는 세기. 스프링에 밀어넣는 임펄스다.</summary>
    public double Power { get; set; } = 9.5;

    /// <summary>한 대 맞을 때 휘청이는 세기. 어느 쪽으로 기울지는 매번 랜덤이다.</summary>
    public double Tilt { get; set; } = 190;

    /// <summary>콤보 한 단계마다 세지는 비율. 0 이면 몇 콤보든 같은 세기로 맞는다.</summary>
    public double ComboGain { get; set; } = 0.07;

    /// <summary>현재 꾸겨진 정도. 0이면 원래 모양.</summary>
    public double Intensity => Math.Abs(_squash.Value);

    public void Hit(int combo)
    {
        double scale = Math.Min(1.0 + ((combo - 1) * ComboGain), 2.4);
        _squash.Kick(Power * scale);
        _tilt.Kick((_random.NextDouble() * 2 - 1) * Tilt * scale);
    }

    /// <summary>
    /// 때린 게 아니라 벽에 부딪힌 것. 콤보를 안 타므로 세기를 직접 준다.
    /// 살짝 스친 것과 세게 꽂힌 것이 같은 소리를 내면 던진 맛이 안 산다.
    /// </summary>
    public void Bump(double strength)
    {
        double scale = Math.Clamp(strength, 0, 2.4);
        _squash.Kick(Power * scale);
        _tilt.Kick((_random.NextDouble() * 2 - 1) * Tilt * scale);
    }

    public void Update(double deltaSeconds)
    {
        _squash.Update(deltaSeconds);
        _tilt.Update(deltaSeconds);

        double squash = Math.Clamp(_squash.Value, MaxStretch, MaxSquash);
        _scale.ScaleY = 1 - squash * 0.55;
        _scale.ScaleX = 1 + squash * 0.42;
        _rotate.Angle = Math.Clamp(_tilt.Value, -MaxTiltDegrees, MaxTiltDegrees);
    }
}
