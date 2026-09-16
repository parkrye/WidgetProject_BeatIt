using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BeatIt.Core;

namespace BeatIt.Controls;

/// <summary>
/// 콤보 수를 튀어오르게 띄운다. 콤보가 쌓일수록 크고 붉어진다.
/// 테마에 숫자 그림이 있으면 그걸 쓰고, 없으면 테마 폰트로, 그것도 없으면 기본 글꼴로 그린다.
/// </summary>
public partial class ComboDisplay : UserControl
{
    private const double LabelRatio = 0.3;

    private static readonly Color[] Tiers =
    [
        Color.FromRgb(0xFF, 0xFF, 0xFF),
        Color.FromRgb(0xFF, 0xE0, 0x66),
        Color.FromRgb(0xFF, 0xA5, 0x2B),
        Color.FromRgb(0xFF, 0x5C, 0x2B),
        Color.FromRgb(0xFF, 0x2B, 0x5C),
    ];

    private readonly FontFamily _fallbackFont;
    private Theme _theme = Theme.Empty();
    private double _size = 52;

    public ComboDisplay()
    {
        InitializeComponent();
        _fallbackFont = CountText.FontFamily;
    }

    public void SetTheme(Theme theme) => _theme = theme;

    public void SetSize(double size) => _size = Math.Max(12, size);

    /// <summary>기본 위치(위젯 위쪽 가운데)에서 얼마나 밀어 놓을지.</summary>
    public void SetOffset(double x, double y)
    {
        Offset.X = x;
        Offset.Y = y;
    }

    /// <summary>콤보 2 이상부터 보여준다. 1은 그냥 한 대 때린 것뿐이라 굳이 안 띄운다.</summary>
    public void Show(int combo)
    {
        if (combo < 2)
        {
            Hide();
            return;
        }

        Brush tint = new SolidColorBrush(Tiers[Math.Min(combo / 8, Tiers.Length - 1)]);
        double size = _size + Math.Min(combo, 40) * 0.9;
        RenderCount(combo, size, tint);
        RenderLabel(size, tint);

        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        Pop(combo);
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

    private void RenderCount(int combo, double size, Brush tint)
    {
        if (_theme.Digits is null)
        {
            DigitsPanel.Visibility = Visibility.Collapsed;
            CountText.Visibility = Visibility.Visible;
            CountText.Text = combo.ToString();
            CountText.FontSize = size;
            CountText.FontFamily = _theme.Font ?? _fallbackFont;
            CountText.Foreground = tint;
            return;
        }

        CountText.Visibility = Visibility.Collapsed;
        DigitsPanel.Visibility = Visibility.Visible;
        DigitsPanel.Children.Clear();

        foreach (char digit in combo.ToString())
        {
            DigitsPanel.Children.Add(new Image
            {
                Source = _theme.Digits[digit - '0'],
                Height = size,
                Stretch = Stretch.Uniform,
            });
        }
    }

    private void RenderLabel(double size, Brush tint)
    {
        if (_theme.Label is null)
        {
            LabelImage.Visibility = Visibility.Collapsed;
            LabelText.Visibility = Visibility.Visible;
            LabelText.FontSize = size * LabelRatio;
            LabelText.FontFamily = _theme.Font ?? _fallbackFont;
            LabelText.Foreground = tint;
            return;
        }

        LabelText.Visibility = Visibility.Collapsed;
        LabelImage.Visibility = Visibility.Visible;
        LabelImage.Source = _theme.Label;
        LabelImage.Height = size * LabelRatio;
    }

    private void Pop(int combo)
    {
        double overshoot = 1.35 + Math.Min(combo, 30) * 0.01;
        DoubleAnimation pop = new(overshoot, 1, TimeSpan.FromMilliseconds(420))
        {
            EasingFunction = new ElasticEase { Oscillations = 2, Springiness = 4, EasingMode = EasingMode.EaseOut },
        };
        PopScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        PopScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
    }
}
