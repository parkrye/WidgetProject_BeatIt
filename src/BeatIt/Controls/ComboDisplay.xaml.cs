using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace BeatIt.Controls;

/// <summary>콤보 수를 튀어오르게 띄운다. 콤보가 쌓일수록 크고 붉어진다.</summary>
public partial class ComboDisplay : UserControl
{
    private static readonly Color[] Tiers =
    [
        Color.FromRgb(0xFF, 0xFF, 0xFF),
        Color.FromRgb(0xFF, 0xE0, 0x66),
        Color.FromRgb(0xFF, 0xA5, 0x2B),
        Color.FromRgb(0xFF, 0x5C, 0x2B),
        Color.FromRgb(0xFF, 0x2B, 0x5C),
    ];

    public ComboDisplay()
    {
        InitializeComponent();
    }

    /// <summary>콤보 2 이상부터 보여준다. 1은 그냥 한 대 때린 것뿐이라 굳이 안 띄운다.</summary>
    public void Show(int combo)
    {
        if (combo < 2)
        {
            Hide();
            return;
        }

        CountText.Text = combo.ToString();
        SolidColorBrush brush = new(Tiers[Math.Min(combo / 8, Tiers.Length - 1)]);
        CountText.Foreground = brush;
        LabelText.Foreground = brush;
        CountText.FontSize = 52 + Math.Min(combo, 40) * 0.9;

        BeginAnimation(OpacityProperty, null);
        Opacity = 1;

        double overshoot = 1.35 + Math.Min(combo, 30) * 0.01;
        DoubleAnimation pop = new(overshoot, 1, TimeSpan.FromMilliseconds(420))
        {
            EasingFunction = new ElasticEase { Oscillations = 2, Springiness = 4, EasingMode = EasingMode.EaseOut },
        };
        PopScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        PopScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
    }

    /// <summary>콤보가 끊겼다. 스르륵 사라진다.</summary>
    public void Hide()
    {
        if (Opacity <= 0)
        {
            return;
        }

        DoubleAnimation fade = new(0, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        };
        BeginAnimation(OpacityProperty, fade);
    }
}
