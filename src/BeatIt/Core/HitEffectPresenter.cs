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
        Animate(image);
    }

    private Image CreateImage(Point center, double size)
    {
        ImageSource source = _theme.Effects[_random.Next(_theme.Effects.Count)];
        ScaleTransform scale = new(0.55, 0.55);
        RotateTransform rotate = new(_random.NextDouble() * 360);
        TransformGroup transform = new();
        transform.Children.Add(scale);
        transform.Children.Add(rotate);

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

    private void Animate(Image image)
    {
        ScaleTransform scale = (ScaleTransform)((TransformGroup)image.RenderTransform).Children[0];

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
    }
}
