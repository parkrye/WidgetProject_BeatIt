using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace BeatIt.Core;

/// <summary>때린 자리에 테마 이펙트를 하나 띄웠다가 지운다.</summary>
public sealed class HitEffectPresenter(Canvas host)
{
    private static readonly Duration Life = new(TimeSpan.FromMilliseconds(430));

    private readonly Random _random = new();
    private Theme _theme = Theme.Empty();

    /// <summary>꺼두면 때려도 아무것도 안 뜬다.</summary>
    public bool Enabled { get; set; } = true;

    public void SetTheme(Theme theme) => _theme = theme;

    /// <summary>꺼져 있거나 테마에 이펙트가 없으면 아무 일도 안 한다.</summary>
    public void Spawn(Point center, double size, int combo)
    {
        if (!Enabled || _theme.Effects.Count == 0)
        {
            return;
        }

        double scale = size * Math.Min(1 + (combo - 1) * 0.04, 1.6);
        Image image = CreateImage(center, scale);
        host.Children.Add(image);
        Animate(image, default);
    }

    /// <summary>
    /// 캐릭터 둘레로 한 바퀴 터뜨린다. 콤보 계단이 오를 때만 쓴다.
    /// 시작 각도를 매번 돌려두지 않으면 항상 같은 자리에서 같은 모양으로 터져 금방 질린다.
    /// </summary>
    public void Burst(Point center, double size, int count, double radius)
    {
        if (!Enabled || _theme.Effects.Count == 0 || count <= 0)
        {
            return;
        }

        double start = _random.NextDouble() * Math.Tau;
        for (int index = 0; index < count; index++)
        {
            double angle = start + (index * Math.Tau / count);
            Vector outward = new(Math.Cos(angle), Math.Sin(angle));

            Image image = CreateImage(center + (outward * radius), size);
            host.Children.Add(image);
            Animate(image, outward * radius * 0.9);
        }
    }

    private Image CreateImage(Point center, double size)
    {
        ImageSource source = _theme.Effects[_random.Next(_theme.Effects.Count)];
        ScaleTransform scale = new(0.55, 0.55);
        RotateTransform rotate = new(_random.NextDouble() * 360);
        TranslateTransform drift = new(0, 0);
        TransformGroup transform = new();
        transform.Children.Add(scale);
        transform.Children.Add(rotate);
        transform.Children.Add(drift);

        Image image = new()
        {
            Source = source,
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = transform,
        };

        Canvas.SetLeft(image, center.X - size / 2);
        Canvas.SetTop(image, center.Y - size / 2);
        return image;
    }

    /// <param name="drift">살아 있는 동안 바깥으로 밀려나는 거리. 0 이면 제자리에서 피었다 진다.</param>
    private void Animate(Image image, Vector drift)
    {
        TransformGroup transform = (TransformGroup)image.RenderTransform;
        ScaleTransform scale = (ScaleTransform)transform.Children[0];

        DoubleAnimation grow = new(0.55, 1.25, Life)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        DoubleAnimation fade = new(1, 0, Life)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        };

        // 이펙트는 하나씩 독립적으로 살았다 사라지니 스토리보드가 알맞다.
        fade.Completed += (_, _) => host.Children.Remove(image);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        image.BeginAnimation(UIElement.OpacityProperty, fade);

        if (drift == default)
        {
            return;
        }

        TranslateTransform slide = (TranslateTransform)transform.Children[2];
        CubicEase ease = new() { EasingMode = EasingMode.EaseOut };
        slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, drift.X, Life) { EasingFunction = ease });
        slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, drift.Y, Life) { EasingFunction = ease });
    }
}
