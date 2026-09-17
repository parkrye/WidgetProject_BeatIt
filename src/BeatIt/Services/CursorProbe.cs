using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace BeatIt.Services;

/// <summary>
/// 커서가 지금 어떤 요소 위에 있는지 OS 에 직접 물어본다.
///
/// WPF 의 <c>IsMouseOver</c> 는 <b>커서가 움직여야</b> 갱신된다. 커서가 가만히 있고 창이
/// 움직이는 동안에는 Win32 가 <c>WM_MOUSEMOVE</c> 도 <c>WM_MOUSELEAVE</c> 도 주지 않아서,
/// 날아가던 위젯이 커서 밑을 벗어나도 들어와도 그 값이 그대로 굳는다.
/// <c>Mouse.Synchronize()</c> 는 WPF 가 "마우스가 우리 창 위에 있다"고 알고 있을 때만 도는
/// 보정이라 이 상황을 못 구한다. 그래서 커서 자리를 매번 새로 읽어 직접 따진다.
/// </summary>
internal static class CursorProbe
{
    /// <summary>
    /// 커서가 <paramref name="element"/> 가 그린 자리 위에 있으면 그 요소 안 좌표.
    /// 아니면 null. 화면에 안 올라간 요소나 커서 자리를 못 읽는 경우도 null 이다.
    /// 어차피 그때는 위에 있다고 볼 근거가 없으니 같이 묶는다.
    /// </summary>
    public static Point? HitPoint(UIElement element)
    {
        if (!GetCursorPos(out NativePoint cursor))
        {
            return null;
        }

        if (PresentationSource.FromVisual(element) is null)
        {
            return null;
        }

        // 화면 좌표는 물리 픽셀이고 요소 안 좌표는 DIP 다. PointFromScreen 이 그 사이를
        // 배율과 렌더 변환까지 얹어 옮겨준다. 꾸겨지고 늘어난 자세 그대로 따지게 된다.
        Point local = element.PointFromScreen(new Point(cursor.X, cursor.Y));
        if (double.IsNaN(local.X) || double.IsNaN(local.Y))
        {
            return null;
        }

        return VisualTreeHelper.HitTest(element, local) is null ? null : local;
    }

    // LibraryImport 로 쓰면 프로젝트 전체에 AllowUnsafeBlocks 를 켜야 한다. 한 번 부르자고 켤 일은 아니다.
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
