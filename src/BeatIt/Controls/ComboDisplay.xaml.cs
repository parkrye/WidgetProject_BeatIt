using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BeatIt.Core;

namespace BeatIt.Controls;

/// <summary>
/// 콤보 수를 캐릭터 머리 위에 띄운다. 창이 내준 자리의 <b>바닥에 밑변을 붙이고 위로만 자라서</b>,
/// 아무리 커져도 캐릭터를 덮지 않는다. 예전에는 창 맨 위에 매달아 아래로 자랐기 때문에
/// 콤보가 40 만 넘어도 머리를 파고들었다.
///
/// 크기와 색은 <see cref="ComboStyle"/> 이 정한 계단을 탄다. 계단이 오르는 자리에서 한 번
/// 크게 연출하고, 스펙트럼을 다 쓰면 은은한 무지개로 넘어간다.
/// 테마가 숫자 그림을 쓰면 색은 못 입힌다. 그림을 물들이면 원래 색이 뭐였는지 알 수 없어진다.
/// </summary>
public partial class ComboDisplay : UserControl
{
    /// <summary>무지개가 한 바퀴 도는 데 걸리는 시간. 빠르면 눈이 아프고 느리면 안 도는 것 같다.</summary>
    private static readonly Duration RainbowCycle = new(TimeSpan.FromSeconds(4.5));

    private readonly FontFamily _fallbackFont;
    private readonly SolidColorBrush _tint = new(Colors.White);

    private ComboStyle _style = new();
    private Theme _theme = Theme.Empty();
    private bool _rainbow;

    public ComboDisplay()
    {
        InitializeComponent();
        _fallbackFont = CountText.FontFamily;
        CountText.Foreground = _tint;
        LabelText.Foreground = _tint;
    }

    public void SetTheme(Theme theme) => _theme = theme;

    /// <summary>크기와 색의 계단을 정한 규칙. 창과 같은 것을 나눠 쓴다.</summary>
    public void SetStyle(ComboStyle style) => _style = style;

    /// <summary>기본 위치(캐릭터 머리 위 가운데)에서 얼마나 밀어 놓을지.</summary>
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

        double size = _style.SizeFor(combo);
        bool milestone = _style.IsMilestone(combo);

        RenderCount(combo, size);
        RenderLabel(size);
        Paint(combo, milestone);

        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        Pop(combo, milestone);
    }

    /// <summary>콤보가 끊겼다. 스르륵 사라진다.</summary>
    public void Hide()
    {
        StopRainbow();

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

    private void RenderCount(int combo, double size)
    {
        if (_theme.Digits is null)
        {
            DigitsPanel.Visibility = Visibility.Collapsed;
            CountText.Visibility = Visibility.Visible;
            CountText.Text = combo.ToString();
            CountText.FontSize = size;
            CountText.FontFamily = _theme.Font ?? _fallbackFont;
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

    private void RenderLabel(double size)
    {
        if (_theme.Label is null)
        {
            LabelImage.Visibility = Visibility.Collapsed;
            LabelText.Visibility = Visibility.Visible;
            LabelText.FontSize = size * ComboStyle.LabelRatio;
            LabelText.FontFamily = _theme.Font ?? _fallbackFont;
            return;
        }

        LabelText.Visibility = Visibility.Collapsed;
        LabelImage.Visibility = Visibility.Visible;
        LabelImage.Source = _theme.Label;
        LabelImage.Height = size * ComboStyle.LabelRatio;
    }

    /// <summary>
    /// 색을 입힌다. 스펙트럼을 다 쓴 뒤로는 무지개를 계속 돌리므로 계단마다 다시 걸지 않는다.
    /// 다시 걸면 매번 첫 색으로 튀어서 도는 게 끊긴다.
    /// </summary>
    private void Paint(int combo, bool milestone)
    {
        if (_style.IsRainbow(combo))
        {
            StartRainbow();
            return;
        }

        StopRainbow();
        Color target = _style.ColorFor(combo);

        if (!milestone)
        {
            _tint.Color = target;
            return;
        }

        // 계단이 오른 순간만 흰색으로 번쩍였다가 그 계단의 색으로 가라앉는다.
        ColorAnimation flash = new(Colors.White, target, new Duration(TimeSpan.FromMilliseconds(520)))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        _tint.BeginAnimation(SolidColorBrush.ColorProperty, flash);
    }

    private void StartRainbow()
    {
        if (_rainbow)
        {
            return;
        }

        _rainbow = true;

        ColorAnimationUsingKeyFrames cycle = new()
        {
            Duration = RainbowCycle,
            RepeatBehavior = RepeatBehavior.Forever,
        };

        // 여섯 조각으로 나눠 한 바퀴. 끝 색이 첫 색과 같아야 이어질 때 안 튄다.
        const int steps = 6;
        for (int step = 0; step <= steps; step++)
        {
            double progress = step / (double)steps;
            cycle.KeyFrames.Add(new LinearColorKeyFrame(ComboStyle.Rainbow(progress), KeyTime.FromPercent(progress)));
        }

        _tint.BeginAnimation(SolidColorBrush.ColorProperty, cycle);
    }

    private void StopRainbow()
    {
        if (!_rainbow)
        {
            return;
        }

        _rainbow = false;

        // 애니메이션을 떼면 마지막으로 그리던 색이 아니라 원래 값으로 돌아간다. 직접 다시 칠한다.
        Color last = _tint.Color;
        _tint.BeginAnimation(SolidColorBrush.ColorProperty, null);
        _tint.Color = last;
    }

    /// <summary>계단이 오르는 자리에서는 훨씬 크게 튄다. 그래야 50 이 사건처럼 보인다.</summary>
    private void Pop(int combo, bool milestone)
    {
        double overshoot = _style.PopFor(combo, milestone);
        Duration life = new(TimeSpan.FromMilliseconds(milestone ? 620 : 420));

        DoubleAnimation pop = new(overshoot, 1, life)
        {
            EasingFunction = new ElasticEase { Oscillations = 2, Springiness = 4, EasingMode = EasingMode.EaseOut },
        };
        PopScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        PopScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
    }
}
