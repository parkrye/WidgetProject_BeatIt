using System.Windows;
using System.Windows.Media;

namespace BeatIt.Core;

/// <summary>
/// 끌고 갈 때 붙잡은 지점은 커서에 붙어 있고 몸통이 뒤처지면서 쭉 늘어난다.
/// 놓으면 스프링으로 출렁이며 제자리를 찾는다.
/// </summary>
public sealed class DragStretchAnimator
{
    private const double MaxLag = 120;
    private const double StretchPerPixel = 0.0055;
    private const double TrailFactor = 0.45;

    /// <summary>스프링을 약하게 만들어도 출렁임이 같은 꼴로 남도록 감쇠를 세기에 맞춰 따라 올린다.</summary>
    private const double DampingRatio = 0.975;

    private readonly RotateTransform _alignBack = new(0);
    private readonly ScaleTransform _stretch = new(1, 1);
    private readonly RotateTransform _alignForward = new(0);
    private readonly TranslateTransform _trail = new(0, 0);
    private readonly TransformGroup _transform;

    private Spring _lagX = NewSpring(95);
    private Spring _lagY = NewSpring(95);
    private double _stiffness = 95;

    public DragStretchAnimator()
    {
        // 늘어나는 축을 x축에 맞춰 늘린 뒤 다시 되돌린다. 그래야 끌리는 방향으로만 늘어난다.
        _transform = new TransformGroup();
        _transform.Children.Add(_alignBack);
        _transform.Children.Add(_stretch);
        _transform.Children.Add(_alignForward);
        _transform.Children.Add(_trail);
    }

    public Transform Transform => _transform;

    /// <summary>끌 때 몸통이 뒤처지는 양. 0 이면 통째로 따라온다.</summary>
    public double Lag { get; set; } = 0.85;

    /// <summary>늘어나는 한계(0~1). 0 이면 안 늘어난다.</summary>
    public double MaxStretch { get; set; } = 0.55;

    /// <summary>
    /// 놓았을 때 제자리로 돌아오는 스프링의 세기. 값이 그대로면 아무것도 안 한다.
    /// 스프링은 struct 라 갈아 끼우면 지금 출렁이던 게 끊기기 때문이다.
    /// </summary>
    public void SetStiffness(double stiffness)
    {
        double next = Math.Max(1, stiffness);
        if (Math.Abs(next - _stiffness) < 0.001)
        {
            return;
        }

        _stiffness = next;
        _lagX = NewSpring(next);
        _lagY = NewSpring(next);
    }

    private static Spring NewSpring(double stiffness) =>
        new(stiffness, DampingRatio * Math.Sqrt(stiffness));

    /// <summary>붙잡은 지점(0~1 정규화). 이 점을 기준으로 늘어난다.</summary>
    public Point Anchor { get; private set; } = new(0.5, 0.5);

    public void Grab(Point normalizedAnchor) =>
        Anchor = new Point(Math.Clamp(normalizedAnchor.X, 0, 1), Math.Clamp(normalizedAnchor.Y, 0, 1));

    /// <summary>창이 <paramref name="delta"/> 만큼 움직였다. 몸통은 그만큼 뒤로 밀린다.</summary>
    public void Pull(Vector delta)
    {
        _lagX.Displace(Math.Clamp(-delta.X * Lag, -MaxLag, MaxLag));
        _lagY.Displace(Math.Clamp(-delta.Y * Lag, -MaxLag, MaxLag));
    }

    public void Update(double deltaSeconds)
    {
        _lagX.Update(deltaSeconds);
        _lagY.Update(deltaSeconds);

        double x = Math.Clamp(_lagX.Value, -MaxLag, MaxLag);
        double y = Math.Clamp(_lagY.Value, -MaxLag, MaxLag);
        double magnitude = Math.Sqrt(x * x + y * y);
        if (magnitude < 0.01)
        {
            ResetTransform();
            return;
        }

        double angle = Math.Atan2(y, x) * 180 / Math.PI;
        double stretch = Math.Min(magnitude * StretchPerPixel, MaxStretch);

        _alignBack.Angle = -angle;
        _alignForward.Angle = angle;
        _stretch.ScaleX = 1 + stretch;
        _stretch.ScaleY = 1 - stretch * 0.5;
        _trail.X = x * TrailFactor;
        _trail.Y = y * TrailFactor;
    }

    private void ResetTransform()
    {
        _alignBack.Angle = 0;
        _alignForward.Angle = 0;
        _stretch.ScaleX = 1;
        _stretch.ScaleY = 1;
        _trail.X = 0;
        _trail.Y = 0;
    }
}
