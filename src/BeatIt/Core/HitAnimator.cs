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

    /// <summary>현재 꾸겨진 정도. 0이면 원래 모양.</summary>
    public double Intensity => Math.Abs(_squash.Value);

    public void Hit(int combo)
    {
        double power = Math.Min(1.0 + (combo - 1) * 0.07, 2.4);
        _squash.Kick(9.5 * power);
        _tilt.Kick((_random.NextDouble() * 2 - 1) * 190 * power);
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
