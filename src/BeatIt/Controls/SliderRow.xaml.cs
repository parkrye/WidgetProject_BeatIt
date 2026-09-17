using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace BeatIt.Controls;

/// <summary>
/// 이름 · 슬라이더 · 지금 값으로 된 설정 한 줄.
/// 설정 창에 이런 줄이 서른 개 가까이 되는데, XAML 에 Grid 세 칸을 매번 펴 두면
/// 값 하나 늘릴 때마다 열다섯 줄을 베끼게 된다. 줄의 생김새는 여기 한 군데에만 둔다.
/// </summary>
public partial class SliderRow : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(SliderRow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty LabelWidthProperty =
        DependencyProperty.Register(nameof(LabelWidth), typeof(GridLength), typeof(SliderRow), new PropertyMetadata(new GridLength(120)));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(SliderRow), new PropertyMetadata(string.Empty, OnDisplayChanged));

    public static readonly DependencyProperty DecimalsProperty =
        DependencyProperty.Register(nameof(Decimals), typeof(int), typeof(SliderRow), new PropertyMetadata(0, OnDisplayChanged));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(SliderRow), new PropertyMetadata(0.0));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(SliderRow), new PropertyMetadata(100.0));

    public static readonly DependencyProperty StepProperty =
        DependencyProperty.Register(nameof(Step), typeof(double), typeof(SliderRow), new PropertyMetadata(1.0));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(SliderRow),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public static readonly DependencyProperty DisplayProperty =
        DependencyProperty.Register(nameof(Display), typeof(string), typeof(SliderRow), new PropertyMetadata(string.Empty));

    public SliderRow() => InitializeComponent();

    /// <summary>슬라이더를 끌 때마다 울린다. 설정 창은 이걸 받아 위젯에 바로 비춰본다.</summary>
    public event EventHandler? ValueChanged;

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>이름 칸의 폭. 한 탭 안의 줄들이 같은 자리에서 시작해야 읽힌다.</summary>
    public GridLength LabelWidth
    {
        get => (GridLength)GetValue(LabelWidthProperty);
        set => SetValue(LabelWidthProperty, value);
    }

    /// <summary>값 뒤에 붙는 단위. "px", "ms" 처럼.</summary>
    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <summary>값을 소수 몇 자리까지 보여줄지.</summary>
    public int Decimals
    {
        get => (int)GetValue(DecimalsProperty);
        set => SetValue(DecimalsProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>슬라이더가 끊어 움직이는 단위.</summary>
    public double Step
    {
        get => (double)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>오른쪽에 적히는 글. 값과 단위로 만든다.</summary>
    public string Display
    {
        get => (string)GetValue(DisplayProperty);
        private set => SetValue(DisplayProperty, value);
    }

    private static void OnValueChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        SliderRow row = (SliderRow)target;
        row.RefreshDisplay();
        row.ValueChanged?.Invoke(row, EventArgs.Empty);
    }

    private static void OnDisplayChanged(DependencyObject target, DependencyPropertyChangedEventArgs e) =>
        ((SliderRow)target).RefreshDisplay();

    private void RefreshDisplay()
    {
        string number = Value.ToString("F" + Decimals, CultureInfo.CurrentCulture);
        Display = Unit.Length == 0 ? number : number + " " + Unit;
    }
}
