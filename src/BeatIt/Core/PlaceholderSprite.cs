using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BeatIt.Core;

/// <summary>이미지를 아직 안 넣었을 때 대신 맞아줄 기본 샌드백을 그린다.</summary>
public static class PlaceholderSprite
{
    private const int Width = 200;
    private const int Height = 260;

    public static ImageSource Create()
    {
        DrawingVisual visual = new();
        using (DrawingContext context = visual.RenderOpen())
        {
            Draw(context);
        }

        RenderTargetBitmap bitmap = new(Width, Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static void Draw(DrawingContext context)
    {
        LinearGradientBrush body = new(
            Color.FromRgb(0xE8, 0x5D, 0x3A),
            Color.FromRgb(0xA8, 0x32, 0x1C),
            new Point(0, 0),
            new Point(1, 1));
        Pen outline = new(new SolidColorBrush(Color.FromRgb(0x3A, 0x18, 0x10)), 5);
        outline.Freeze();

        SolidColorBrush strap = new(Color.FromRgb(0x2C, 0x2C, 0x33));
        strap.Freeze();
        context.DrawRoundedRectangle(strap, null, new Rect(80, 6, 40, 30), 6, 6);
        context.DrawRoundedRectangle(body, outline, new Rect(30, 30, 140, 210), 48, 48);
        context.DrawRoundedRectangle(strap, null, new Rect(30, 92, 140, 16), 4, 4);

        SolidColorBrush ink = new(Color.FromRgb(0x24, 0x10, 0x0C));
        ink.Freeze();
        context.DrawEllipse(ink, null, new Point(78, 150), 9, 12);
        context.DrawEllipse(ink, null, new Point(122, 150), 9, 12);

        StreamGeometry mouth = new();
        using (StreamGeometryContext geometry = mouth.Open())
        {
            geometry.BeginFigure(new Point(78, 192), false, false);
            geometry.QuadraticBezierTo(new Point(100, 176), new Point(122, 192), true, false);
        }

        mouth.Freeze();
        context.DrawGeometry(null, new Pen(ink, 6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, mouth);
    }
}
