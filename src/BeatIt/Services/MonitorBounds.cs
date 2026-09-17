using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace BeatIt.Services;

/// <summary>모니터 하나의 전체 영역과 작업 영역. 둘 다 WPF 좌표(DIP)로 바꿔서 담는다.</summary>
public sealed record MonitorArea(Rect Full, Rect Work);

/// <summary>
/// 어떤 지점이 올라가 있는 모니터를 묻는다.
/// WPF 는 주 모니터의 작업 영역(<see cref="SystemParameters.WorkArea"/>)만 알려줘서, 나머지는 Win32 에 물어봐야 한다.
/// WinForms 의 Screen 을 쓰면 간단하지만 그것 때문에 배포 exe 가 크게 불어난다.
/// </summary>
internal static class MonitorBounds
{
    private const uint MonitorDefaultToNearest = 2;

    /// <summary>
    /// <paramref name="point"/> 가 올라가 있는 모니터. 못 찾으면 null.
    /// <paramref name="visual"/> 은 DPI 배율을 얻는 데 쓴다. 창이 아직 화면에 안 올라갔으면 알 수 없어서 null 이 나온다.
    /// </summary>
    public static MonitorArea? Containing(Visual visual, Point point)
    {
        CompositionTarget? target = PresentationSource.FromVisual(visual)?.CompositionTarget;
        if (target is null)
        {
            return null;
        }

        // Win32 는 물리 픽셀로 말하고 WPF 는 DIP 로 말한다. 오갈 때마다 배율을 태워야 한다.
        Point device = target.TransformToDevice.Transform(point);
        nint monitor = MonitorFromPoint(new NativePoint((int)device.X, (int)device.Y), MonitorDefaultToNearest);
        if (monitor == 0)
        {
            return null;
        }

        NativeMonitorInfo info = new() { Size = Marshal.SizeOf<NativeMonitorInfo>() };
        if (!GetMonitorInfoW(monitor, ref info))
        {
            return null;
        }

        Matrix fromDevice = target.TransformFromDevice;
        return new MonitorArea(ToRect(info.Monitor, fromDevice), ToRect(info.Work, fromDevice));
    }

    private static Rect ToRect(NativeRect rect, Matrix fromDevice)
    {
        Point topLeft = fromDevice.Transform(new Point(rect.Left, rect.Top));
        Point bottomRight = fromDevice.Transform(new Point(rect.Right, rect.Bottom));
        return new Rect(topLeft, bottomRight);
    }

    // LibraryImport 로 쓰면 프로젝트 전체에 AllowUnsafeBlocks 를 켜야 한다. 두 번 부르자고 켤 일은 아니다.
    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfoW(nint monitor, ref NativeMonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public int Flags;
    }
}
