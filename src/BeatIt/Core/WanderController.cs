using System.Windows;

namespace BeatIt.Core;

/// <summary>켜두면 위젯이 혼자 화면을 돌아다닌다. 목적지를 하나 고르고 가다가, 도착하면 좀 쉬고 다시 고른다.</summary>
public sealed class WanderController
{
    private const double ArriveDistance = 1.5;

    private readonly Random _random = new();

    private Point _origin;
    private Point _target;
    private double _speed;
    private double _restRemaining;
    private double _suspendRemaining;
    private bool _hasTarget;
    private bool _enabled;

    /// <summary>꺼질 때 바로 잊는다. 껐다 켜면 예전 목적지가 아니라 새 목적지를 고른다.</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            Reset();
        }
    }

    public bool IsMoving { get; private set; }

    /// <summary>목적지를 새로 고를 때마다 이 사이에서 걷는 속도를 뽑는다(px/s).</summary>
    public double SpeedMin { get; private set; } = 70;

    public double SpeedMax { get; private set; } = 165;

    /// <summary>목적지에 닿은 뒤 이 사이에서 쉬는 시간을 뽑는다(초).</summary>
    public double RestMin { get; private set; } = 1.5;

    public double RestMax { get; private set; } = 5.5;

    /// <summary>
    /// 걷는 속도와 쉬는 시간의 범위를 정한다. 뒤집힌 값이 들어와도 서로 넘지 않게 눌러둔다.
    /// 설정 창에서 최소를 최대보다 크게 끌어도 그 프레임에 걸음이 멈추면 안 된다.
    /// </summary>
    public void SetPace(double speedMin, double speedMax, double restMin, double restMax)
    {
        SpeedMin = Math.Max(1, speedMin);
        SpeedMax = Math.Max(SpeedMin, speedMax);
        RestMin = Math.Max(0, restMin);
        RestMax = Math.Max(RestMin, restMax);
    }

    /// <summary>맞았거나 끌려가는 중에는 잠깐 멈춘다.</summary>
    public void Suspend(double seconds)
    {
        _suspendRemaining = Math.Max(_suspendRemaining, seconds);
        IsMoving = false;
    }

    /// <summary>이번 프레임에 창을 얼마나 옮길지 돌려준다.</summary>
    public Vector Update(double deltaSeconds, Point current, Rect bounds)
    {
        if (!Enabled)
        {
            Reset();
            return default;
        }

        if (_suspendRemaining > 0)
        {
            _suspendRemaining -= deltaSeconds;
            _hasTarget = false;
            return default;
        }

        if (!_hasTarget)
        {
            StartTrip(current, bounds);
            return default;
        }

        if (_restRemaining > 0)
        {
            _restRemaining -= deltaSeconds;
            IsMoving = false;
            if (_restRemaining <= 0)
            {
                _hasTarget = false;
            }

            return default;
        }

        return Step(deltaSeconds, current);
    }

    private Vector Step(double deltaSeconds, Point current)
    {
        Vector remaining = _target - current;
        double distance = remaining.Length;
        if (distance <= ArriveDistance)
        {
            IsMoving = false;
            _restRemaining = RestMin + _random.NextDouble() * (RestMax - RestMin);
            return remaining;
        }

        IsMoving = true;

        // 출발과 도착에서 속도를 줄여 뚜벅뚜벅 걷는 느낌을 준다.
        double travelled = (_origin - current).Length;
        double total = Math.Max(1, travelled + distance);
        double progress = Math.Clamp(travelled / total, 0, 1);
        double ease = 0.35 + 0.65 * Math.Sin(progress * Math.PI);
        double stride = Math.Min(_speed * ease * deltaSeconds, distance);

        return remaining / distance * stride;
    }

    private void StartTrip(Point current, Rect bounds)
    {
        _origin = current;
        _target = new Point(
            bounds.X + _random.NextDouble() * Math.Max(1, bounds.Width),
            bounds.Y + _random.NextDouble() * Math.Max(1, bounds.Height));
        _speed = SpeedMin + _random.NextDouble() * (SpeedMax - SpeedMin);
        _restRemaining = 0;
        _hasTarget = true;
        IsMoving = true;
    }

    private void Reset()
    {
        _hasTarget = false;
        _restRemaining = 0;
        _suspendRemaining = 0;
        IsMoving = false;
    }
}
