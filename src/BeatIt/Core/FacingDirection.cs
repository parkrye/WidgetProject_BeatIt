using System.Windows;

namespace BeatIt.Core;

/// <summary>방향별 그림을 고를 때 쓰는 네 방향과, 방향을 안 따지는 기본값.</summary>
public enum FacingDirection
{
    /// <summary>방향을 안 따진다. 가운데를 때렸거나 방향 폴더가 없을 때.</summary>
    Default,

    Left,

    Right,

    Up,

    Down,
}

/// <summary>클릭한 자리와 진행 벡터를 방향으로 바꾼다.</summary>
public static class Facing
{
    /// <summary>모든 방향. 목록에 찍을 때처럼 순서가 필요한 곳에 쓴다.</summary>
    public static readonly FacingDirection[] All =
    [
        FacingDirection.Default,
        FacingDirection.Left,
        FacingDirection.Right,
        FacingDirection.Up,
        FacingDirection.Down,
    ];

    /// <summary>중심에서 이 반지름 안을 때리면 가운데로 본다. 가로세로를 각각 펴서 재기 때문에 실제로는 타원이다.</summary>
    private const double CenterRadius = 0.25;

    /// <summary>세로로 넘어가려면 가로보다 이만큼 뚜렷해야 한다. 비스듬히 걸을 때 위아래로 깜빡이는 걸 막는다.</summary>
    private const double VerticalBias = 1.4;

    /// <summary>이보다 짧게 움직였으면 방향을 다시 따지지 않는다.</summary>
    private const double MinMotion = 0.01;

    /// <summary>
    /// 스프라이트 안에서 때린 자리를 방향으로 바꾼다.
    /// <paramref name="point"/> 는 스프라이트 왼쪽 위 기준이고 <paramref name="size"/> 는 그 스프라이트 크기다.
    /// 가운데 타원 안이면 <see cref="FacingDirection.Default"/>, 밖이면 45도씩 나눈 부채꼴 넷 중 하나.
    /// </summary>
    public static FacingDirection FromHit(Point point, Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            return FacingDirection.Default;
        }

        // 축마다 [-1, 1] 로 펴면 가운데 원이 스프라이트 비율을 따라 늘어난다. 세로로 긴 캐릭터에도 영역이 맞는다.
        double x = (point.X / size.Width * 2) - 1;
        double y = (point.Y / size.Height * 2) - 1;

        if ((x * x) + (y * y) <= CenterRadius * CenterRadius)
        {
            return FacingDirection.Default;
        }

        if (Math.Abs(y) > Math.Abs(x))
        {
            return y < 0 ? FacingDirection.Up : FacingDirection.Down;
        }

        return x < 0 ? FacingDirection.Left : FacingDirection.Right;
    }

    /// <summary>
    /// 움직인 벡터를 방향으로 바꾼다. 거의 안 움직였으면 <paramref name="previous"/> 를 그대로 둔다.
    /// 프레임마다 방향이 뒤집히면 그림이 깜빡이기 때문에, 세로로 넘어갈 때만 한 번 더 따진다.
    /// </summary>
    public static FacingDirection FromMotion(Vector motion, FacingDirection previous)
    {
        if (motion.Length < MinMotion)
        {
            return previous;
        }

        if (Math.Abs(motion.Y) > Math.Abs(motion.X) * VerticalBias)
        {
            return motion.Y < 0 ? FacingDirection.Up : FacingDirection.Down;
        }

        return motion.X < 0 ? FacingDirection.Left : FacingDirection.Right;
    }
}
