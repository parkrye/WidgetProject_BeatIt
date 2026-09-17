using System.Windows;

namespace BeatIt.Core;

/// <summary>벽에 부딪힌 한 번. 어느 쪽이 얼마나 세게 박았는지.</summary>
/// <param name="Side">캐릭터의 어느 쪽이 벽에 닿았는지. 그쪽 beat 그림이 뜬다.</param>
/// <param name="Strength">0 이면 안 부딪혔다. 1 이 보통 한 대 맞은 정도.</param>
public readonly record struct Bump(FacingDirection Side, double Strength)
{
    public static Bump None => new(FacingDirection.Default, 0);

    public bool Happened => Strength > 0;
}

/// <summary>
/// 끌다 놓으면 놓은 속도로 날아가고, 영역 경계에 통 하고 튕긴다.
/// 중력은 없다. 있으면 언제나 영역 바닥에 가라앉아서, 위젯을 원하는 자리에 둘 수가 없다.
/// 마찰로 느려지다 <see cref="StopSpeed"/> 밑으로 떨어지면 공중이든 어디든 그 자리에 선다.
/// </summary>
public sealed class ThrowMotion
{
    /// <summary>
    /// 이 속도를 정통으로 벽에 꽂으면 한 대 맞은 만큼 꾸겨진다(px/s).
    /// 살살 굴러가 닿은 것과 던져 박은 것이 같은 세기로 튀면 던진 맛이 안 산다.
    /// </summary>
    private const double FullBumpSpeed = 900;

    /// <summary>튕겨도 이만큼은 벽에서 떼어놓는다. 경계에 딱 붙은 채로 매 프레임 다시 부딪히지 않게.</summary>
    private const double WallClearance = 0.5;

    private Vector _velocity;

    /// <summary>끄면 놓아도 안 날아가고, 날던 것도 그 자리에 선다.</summary>
    public bool Enabled { get; set; }

    /// <summary>놓은 속도를 얼마나 부풀려 던질지.</summary>
    public double SpeedScale { get; set; } = 1;

    /// <summary>아무리 세게 뿌려도 이 속도를 넘지 않는다(px/s).</summary>
    public double MaxSpeed { get; set; } = 2600;

    /// <summary>벽에 튕길 때 남는 속도의 비율(0~1).</summary>
    public double Bounce { get; set; } = 0.6;

    /// <summary>나는 동안 초당 줄어드는 속도의 비율.</summary>
    public double Friction { get; set; } = 1.6;

    /// <summary>이 속도 밑이면 다 왔다고 보고 멈춘다(px/s).</summary>
    public double StopSpeed { get; set; } = 40;

    public bool IsFlying { get; private set; }

    /// <summary>놓은 속도로 던진다. 꺼져 있거나 너무 느리면 아무 일도 안 난다.</summary>
    public void Launch(Vector pixelsPerSecond)
    {
        if (!Enabled)
        {
            return;
        }

        Vector velocity = pixelsPerSecond * SpeedScale;
        double speed = velocity.Length;
        if (speed <= StopSpeed)
        {
            return;
        }

        // 마우스를 확 튕기면 한 프레임에 화면을 가로지르는 속도가 나온다. 뚜껑을 덮어둔다.
        _velocity = speed > MaxSpeed ? velocity / speed * MaxSpeed : velocity;
        IsFlying = true;
    }

    public void Stop()
    {
        _velocity = default;
        IsFlying = false;
    }

    /// <summary>
    /// 벽이 아닌 것에 부딪혔다. <paramref name="away"/> 는 부딪힌 자리에서 멀어지는 방향이고,
    /// 길이는 안 본다. 그 방향을 법선으로 삼아 튕겨낸다.
    /// 이미 멀어지는 중이면 아무 일도 안 난다. 닿은 채로 지나가는 동안 매 프레임 튕기지 않게.
    /// </summary>
    public Bump BounceOff(Vector away, FacingDirection side)
    {
        if (!IsFlying)
        {
            return Bump.None;
        }

        // 정확히 한가운데를 찔렸으면 멀어질 방향이 없다. 온 길로 되돌려보낸다.
        Vector normal = away.LengthSquared > 0 ? away : -_velocity;
        normal /= normal.Length;

        // 법선을 파고드는 속도. 이만큼을 두 배로 되돌려주면 반사가 된다.
        double into = -(_velocity * normal);
        if (into <= 0)
        {
            return Bump.None;
        }

        _velocity = (_velocity + (normal * into * 2)) * Math.Clamp(Bounce, 0, 1);
        if (_velocity.Length < StopSpeed)
        {
            Stop();
        }

        return new Bump(side, Math.Clamp(into / FullBumpSpeed, 0.15, 2.4));
    }

    /// <summary>
    /// 이번 프레임에 창을 얼마나 옮길지 돌려준다. <paramref name="travel"/> 은 창 왼쪽 위가 갈 수 있는 범위다.
    /// 벽에 닿았으면 <paramref name="bump"/> 에 어느 쪽을 얼마나 세게 박았는지 담긴다.
    /// </summary>
    public Vector Update(double deltaSeconds, Point topLeft, Rect travel, out Bump bump)
    {
        bump = Bump.None;
        if (!IsFlying || !Enabled)
        {
            Stop();
            return default;
        }

        // 초당 비율로 깎는다. 프레임이 길어져도 짧아져도 같은 거리에서 선다.
        _velocity *= Math.Exp(-Math.Max(0, Friction) * deltaSeconds);
        if (_velocity.Length < StopSpeed)
        {
            Stop();
            return default;
        }

        Point target = topLeft + (_velocity * deltaSeconds);
        bump = Deflect(ref target, travel);
        return target - topLeft;
    }

    /// <summary>
    /// 영역 밖으로 나간 만큼 벽에 붙여 세우고 그 축의 속도를 뒤집는다.
    /// 한 프레임에 두 벽을 같이 박을 수 있어서 축마다 따로 본다. 더 세게 박은 쪽을 알린다.
    /// </summary>
    private Bump Deflect(ref Point target, Rect travel)
    {
        double right = travel.X + travel.Width;
        double bottom = travel.Y + travel.Height;
        double vx = _velocity.X;
        double vy = _velocity.Y;
        Bump bump = Bump.None;

        if (target.X < travel.X)
        {
            target.X = travel.X + WallClearance;
            bump = Stronger(bump, Reflect(ref vx, -1, FacingDirection.Left));
        }
        else if (target.X > right)
        {
            target.X = right - WallClearance;
            bump = Stronger(bump, Reflect(ref vx, 1, FacingDirection.Right));
        }

        if (target.Y < travel.Y)
        {
            target.Y = travel.Y + WallClearance;
            bump = Stronger(bump, Reflect(ref vy, -1, FacingDirection.Up));
        }
        else if (target.Y > bottom)
        {
            target.Y = bottom - WallClearance;
            bump = Stronger(bump, Reflect(ref vy, 1, FacingDirection.Down));
        }

        if (!bump.Happened)
        {
            return bump;
        }

        _velocity = new Vector(vx, vy);

        // 다 튕기고 나니 기어가는 속도만 남았으면 거기서 선다. 벽에 붙어 잘게 떠는 걸 막는다.
        if (_velocity.Length < StopSpeed)
        {
            Stop();
        }

        return bump;
    }

    /// <summary>
    /// <paramref name="into"/> 는 그 벽으로 들어가는 방향의 부호다. 벽 밖에 나가 있어도
    /// 이미 멀어지는 중이면 도로 세우기만 하고 박은 걸로 안 친다. 콤보가 오르면 창이 커지면서
    /// 이동 영역이 그만큼 좁아지는데, 그것까지 박은 걸로 치면 벽에 붙어 콤보가 저 혼자 쌓인다.
    /// </summary>
    private Bump Reflect(ref double component, double into, FacingDirection side)
    {
        if (component * into <= 0)
        {
            return Bump.None;
        }

        double before = Math.Abs(component);
        component = -component * Math.Clamp(Bounce, 0, 1);
        return new Bump(side, Math.Clamp(before / FullBumpSpeed, 0.15, 2.4));
    }

    private static Bump Stronger(Bump left, Bump right) => right.Strength > left.Strength ? right : left;
}
