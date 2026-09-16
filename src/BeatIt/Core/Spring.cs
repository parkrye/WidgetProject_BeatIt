namespace BeatIt.Core;

/// <summary>
/// 감쇠 스프링 하나. 타격을 "임펄스"로 밀어넣기 때문에 연타하면 흔들림이 누적된다.
/// Storyboard 로는 연타할 때마다 애니메이션이 끊기고 처음부터 다시 시작해서 이 방식을 썼다.
/// </summary>
public struct Spring(double stiffness, double damping)
{
    private const double MaxStep = 1.0 / 240.0;

    public double Value { get; private set; }

    public double Velocity { get; private set; }

    public readonly bool IsResting => Math.Abs(Value) < 0.0005 && Math.Abs(Velocity) < 0.0005;

    public void Kick(double impulse) => Velocity += impulse;

    /// <summary>속도가 아니라 위치를 직접 밀어낸다. 끌고 갈 때 몸통이 뒤처지는 양에 쓴다.</summary>
    public void Displace(double amount) => Value += amount;

    public void Reset()
    {
        Value = 0;
        Velocity = 0;
    }

    /// <summary>프레임이 길어져도 터지지 않도록 잘게 쪼개서 적분한다.</summary>
    public void Update(double deltaSeconds)
    {
        double remaining = Math.Min(deltaSeconds, 0.1);
        while (remaining > 0)
        {
            double step = Math.Min(MaxStep, remaining);
            Velocity += (-Value * stiffness - Velocity * damping) * step;
            Value += Velocity * step;
            remaining -= step;
        }

        if (IsResting)
        {
            Reset();
        }
    }
}
