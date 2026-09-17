using System.Windows;
using System.Windows.Media;
using BeatIt.Services;

namespace BeatIt.Core;

/// <summary>돌아다닐 범위를 어디로 잡을지.</summary>
public enum WanderAreaKind
{
    /// <summary>모니터를 다 합친 가상 화면 전체.</summary>
    FullScreen,

    /// <summary>위젯이 지금 올라가 있는 모니터. 다른 모니터로 옮기면 그쪽으로 따라간다.</summary>
    CurrentMonitor,

    /// <summary>그 모니터의 작업 영역. 작업표시줄을 뺀 자리다.</summary>
    WorkArea,

    /// <summary>사용자가 화면에 직접 그린 사각형.</summary>
    Custom,
}

/// <summary>설정에 적힌 이동 영역을 실제 사각형으로 푼다.</summary>
public static class WanderArea
{
    /// <summary>모니터를 다 합친 사각형.</summary>
    public static Rect VirtualScreen => new(
        SystemParameters.VirtualScreenLeft,
        SystemParameters.VirtualScreenTop,
        SystemParameters.VirtualScreenWidth,
        SystemParameters.VirtualScreenHeight);

    /// <summary>
    /// 돌아다닐 사각형. 화면 밖으로 나간 만큼은 잘라내고, 남는 게 없으면 화면 전체로 돌아간다.
    /// 모니터를 빼거나 해상도를 바꿔도 위젯이 안 보이는 데로 걸어가 버리지 않게 하려는 것.
    /// </summary>
    public static Rect Resolve(WanderAreaKind kind, Rect custom, Visual visual, Point widgetCenter)
    {
        Rect screen = VirtualScreen;
        Rect clipped = Rect.Intersect(Wanted(kind, custom, visual, widgetCenter, screen), screen);

        return clipped.IsEmpty || clipped.Width < 1 || clipped.Height < 1 ? screen : clipped;
    }

    /// <summary>
    /// 창의 왼쪽 위가 돌아다닐 수 있는 범위.
    /// 창은 늘어나고 흔들려도 안 잘리게 여백을 크게 갖고 있어서, 창째로 영역 안에 넣어야 콤보와 출렁임이 안 잘린다.
    /// </summary>
    public static Rect Travel(Rect area, Size windowSize) => new(
        area.X,
        area.Y,
        Math.Max(1, area.Width - windowSize.Width),
        Math.Max(1, area.Height - windowSize.Height));

    private static Rect Wanted(WanderAreaKind kind, Rect custom, Visual visual, Point widgetCenter, Rect screen)
    {
        if (kind == WanderAreaKind.Custom)
        {
            return custom;
        }

        if (kind == WanderAreaKind.FullScreen)
        {
            return screen;
        }

        MonitorArea? monitor = MonitorBounds.Containing(visual, widgetCenter);
        if (monitor is null)
        {
            // 모니터를 못 물어봤으면 주 모니터 기준으로라도 답한다. 돌아다니기가 멈추는 것보다 낫다.
            return kind == WanderAreaKind.WorkArea ? SystemParameters.WorkArea : screen;
        }

        return kind == WanderAreaKind.WorkArea ? monitor.Work : monitor.Full;
    }
}
