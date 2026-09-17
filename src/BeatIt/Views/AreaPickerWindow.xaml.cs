using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BeatIt.Core;

namespace BeatIt.Views;

/// <summary>
/// 화면을 통째로 덮고 사각형을 끌어 그리게 하는 오버레이.
/// 창이 가상 화면과 정확히 겹쳐 있어서, 창 안 좌표에 창 위치만 더하면 곧 화면 좌표가 된다.
/// </summary>
public partial class AreaPickerWindow : Window
{
    /// <summary>이보다 작게 그리면 실수로 클릭한 걸로 본다.</summary>
    private const double MinimumSide = 40;

    private Point _anchor;
    private Rect _selection = Rect.Empty;
    private bool _dragging;

    public AreaPickerWindow(Rect? initial)
    {
        InitializeComponent();

        Rect screen = WanderArea.VirtualScreen;
        Left = screen.X;
        Top = screen.Y;
        Width = screen.Width;
        Height = screen.Height;

        if (initial is { } previous)
        {
            _selection = Rect.Intersect(previous, screen);
            if (_selection.IsEmpty)
            {
                _selection = Rect.Empty;
            }
            else
            {
                _selection.Offset(-screen.X, -screen.Y);
            }
        }

        Loaded += OnLoaded;
    }

    /// <summary>확인했을 때 그려진 영역. 화면 좌표다.</summary>
    public Rect Area { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Activate();
        Focus();
        Redraw();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        _dragging = true;
        _anchor = e.GetPosition(Root);
        _selection = Rect.Empty;
        Guide.Visibility = Visibility.Collapsed;
        CaptureMouse();
        Redraw();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_dragging)
        {
            return;
        }

        _selection = new Rect(_anchor, e.GetPosition(Root));
        Redraw();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        ReleaseMouseCapture();
        Redraw();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            return;
        }

        if (e.Key is not (Key.Enter or Key.Return) || !IsUsable(_selection))
        {
            return;
        }

        Area = new Rect(Left + _selection.X, Top + _selection.Y, _selection.Width, _selection.Height);
        DialogResult = true;
    }

    private static bool IsUsable(Rect rect) =>
        !rect.IsEmpty && rect.Width >= MinimumSide && rect.Height >= MinimumSide;

    private void Redraw()
    {
        RectangleGeometry full = new(new Rect(0, 0, ActualWidth, ActualHeight));
        bool usable = IsUsable(_selection);

        // 고른 자리만 원래 밝기로 남기고 나머지를 덮는다.
        Shade.Data = usable
            ? new CombinedGeometry(GeometryCombineMode.Exclude, full, new RectangleGeometry(_selection))
            : full;

        if (!usable)
        {
            Marquee.Visibility = Visibility.Collapsed;
            Readout.Visibility = Visibility.Collapsed;
            PlaceGuide();
            return;
        }

        Place(Marquee, _selection.X, _selection.Y);
        Marquee.Width = _selection.Width;
        Marquee.Height = _selection.Height;
        Marquee.Visibility = Visibility.Visible;

        ReadoutText.Text = $"{_selection.Width:F0} x {_selection.Height:F0}";
        Readout.Visibility = Visibility.Visible;
        Readout.UpdateLayout();

        // 위쪽에 자리가 없으면 사각형 안쪽으로 내려 붙인다.
        double readoutTop = _selection.Y - Readout.ActualHeight - 8;
        Place(Readout, _selection.X, readoutTop < 0 ? _selection.Y + 8 : readoutTop);
    }

    private void PlaceGuide()
    {
        Guide.Visibility = _dragging ? Visibility.Collapsed : Visibility.Visible;
        Guide.UpdateLayout();
        Place(Guide, (ActualWidth - Guide.ActualWidth) / 2, (ActualHeight - Guide.ActualHeight) / 2);
    }

    private static void Place(UIElement element, double left, double top)
    {
        Canvas.SetLeft(element, left);
        Canvas.SetTop(element, top);
    }
}
