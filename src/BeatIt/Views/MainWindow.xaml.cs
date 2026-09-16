using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BeatIt.Core;
using BeatIt.Models;
using BeatIt.Services;

namespace BeatIt.Views;

/// <summary>화면 위에 떠 있는 위젯 본체. 클릭은 타격, 끌면 늘어나며 따라온다.</summary>
public partial class MainWindow : Window, ISettingsPreview
{
    private const double DragThresholdPixels = 5;
    private const double PaddingRatio = 0.45;
    private const double MinPadding = 70;
    private const double HitRestSeconds = 1.6;
    private const double DragRestSeconds = 0.8;

    /// <summary>이펙트가 캐릭터를 덮지 않도록 가로 길이의 절반 이하로 잡는다.</summary>
    private const double EffectSizeRatio = 0.45;

    private readonly SettingsService _settingsService;
    private readonly HitAnimator _hitAnimator = new();
    private readonly DragStretchAnimator _dragAnimator = new();
    private readonly WanderController _wander = new();
    private readonly ComboCounter _comboCounter;
    private readonly HitEffectPresenter _effects;

    private AppSettings _settings;
    private ISpriteSource? _spriteSource;
    private Theme? _theme;
    private TimeSpan _lastRenderTime;
    private Point _grabPoint;
    private bool _pressed;
    private bool _dragging;
    private bool _positioned;

    public MainWindow(SettingsService settingsService, AppSettings settings)
    {
        InitializeComponent();

        _settingsService = settingsService;
        _settings = settings;
        _comboCounter = new ComboCounter(TimeSpan.FromMilliseconds(settings.ComboTimeoutMs));
        _effects = new HitEffectPresenter(EffectLayer);

        SpriteImage.RenderTransform = _hitAnimator.Transform;
        DragRoot.RenderTransform = _dragAnimator.Transform;
        WindowStartupLocation = WindowStartupLocation.Manual;

        ApplySettings(settings);
        RestorePosition();
        _positioned = true;

        CompositionTarget.Rendering += OnRendering;
        Closed += OnClosed;
    }

    /// <summary>설정 창이 값을 만질 때마다 그대로 비춰준다. 저장은 확인을 눌렀을 때만.</summary>
    public void Preview(AppSettings settings) => ApplySettings(settings);

    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs args)
        {
            return;
        }

        double delta = (args.RenderingTime - _lastRenderTime).TotalSeconds;
        _lastRenderTime = args.RenderingTime;
        if (delta is <= 0 or > 0.5)
        {
            return;
        }

        Walk(delta);
        _hitAnimator.Update(delta);
        _dragAnimator.Update(delta);
        _spriteSource!.Update(delta, _dragging || _wander.IsMoving ? SpriteState.Moving : SpriteState.Idle);
        DragRoot.RenderTransformOrigin = _dragAnimator.Anchor;

        if (_comboCounter.ExpireIfTimedOut())
        {
            Combo.Hide();
        }
    }

    /// <summary>혼자 돌아다니는 몫만큼 창을 옮긴다. 걸을 때도 몸이 살짝 늘어난다.</summary>
    private void Walk(double delta)
    {
        if (_pressed || _dragging)
        {
            _wander.Suspend(DragRestSeconds);
            return;
        }

        Rect bounds = new(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            Math.Max(1, SystemParameters.VirtualScreenWidth - Width),
            Math.Max(1, SystemParameters.VirtualScreenHeight - Height));

        Vector step = _wander.Update(delta, new Point(Left, Top), bounds);
        if (step == default)
        {
            return;
        }

        Left += step.X;
        Top += step.Y;
        _dragAnimator.Grab(new Point(0.5, 0.35));
        _dragAnimator.Pull(step * 0.4);
    }

    private void OnSpriteMouseDown(object sender, MouseButtonEventArgs e)
    {
        _pressed = true;
        _dragging = false;
        _grabPoint = e.GetPosition(this);
        SpriteImage.CaptureMouse();
    }

    private void OnSpriteMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        Vector delta = e.GetPosition(this) - _grabPoint;
        if (!_dragging)
        {
            if (_settings.PositionLocked || delta.Length < DragThresholdPixels)
            {
                return;
            }

            _dragging = true;
            _dragAnimator.Grab(Normalize(_grabPoint));
        }

        // 창은 커서를 그대로 따라가고, 안쪽 그림만 뒤처지면서 늘어난다.
        Left += delta.X;
        Top += delta.Y;
        _dragAnimator.Pull(delta);
    }

    private async void OnSpriteMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        SpriteImage.ReleaseMouseCapture();

        if (!_dragging)
        {
            Hit(e.GetPosition(this));
            return;
        }

        _dragging = false;
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        await _settingsService.SaveAsync(_settings);
    }

    private void Hit(Point where)
    {
        int combo = _comboCounter.Register();
        _spriteSource!.OnHit();
        _hitAnimator.Hit(combo);
        Combo.Show(combo);
        _effects.Spawn(where, SpriteImage.Width * EffectSizeRatio, combo);
        _wander.Suspend(HitRestSeconds);
    }

    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        _pressed = false;
        WanderMenuItem.IsChecked = _settings.Wander;
        TopmostMenuItem.IsChecked = _settings.Topmost;
        LockMenuItem.IsChecked = _settings.PositionLocked;
    }

    private async void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        AppSettings original = _settings.Clone();
        SettingsWindow dialog = new(_settings.Clone(), this) { Owner = this };

        if (dialog.ShowDialog() != true)
        {
            ApplySettings(original);
            return;
        }

        ApplySettings(dialog.Result);
        await _settingsService.SaveAsync(_settings);
    }

    private async void OnToggleWander(object sender, RoutedEventArgs e)
    {
        _settings.Wander = WanderMenuItem.IsChecked;
        _wander.Enabled = _settings.Wander && !_settings.PositionLocked;
        await _settingsService.SaveAsync(_settings);
    }

    private async void OnToggleTopmost(object sender, RoutedEventArgs e)
    {
        _settings.Topmost = TopmostMenuItem.IsChecked;
        Topmost = _settings.Topmost;
        await _settingsService.SaveAsync(_settings);
    }

    private async void OnToggleLock(object sender, RoutedEventArgs e)
    {
        _settings.PositionLocked = LockMenuItem.IsChecked;
        _wander.Enabled = _settings.Wander && !_settings.PositionLocked;
        await _settingsService.SaveAsync(_settings);
    }

    private void OnExit(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    /// <summary>바뀐 것만 다시 만든다. 슬라이더를 끄는 동안 이미지를 매번 다시 읽으면 버벅인다.</summary>
    private void ApplySettings(AppSettings next)
    {
        bool rebuildCharacter = _spriteSource is null || !SamePath(next.CharacterPath, _settings.CharacterPath);
        bool rebuildTheme = _theme is null || !SamePath(next.ThemePath, _settings.ThemePath);
        _settings = next;

        if (rebuildCharacter)
        {
            ReplaceSpriteSource();
        }

        if (rebuildTheme)
        {
            _theme = LoadTheme(next.ThemePath);
            Combo.SetTheme(_theme);
            _effects.SetTheme(_theme);
        }

        _spriteSource!.SetIdleInterval(next.IdleMinMs / 1000.0, next.IdleMaxMs / 1000.0);
        _comboCounter.Timeout = TimeSpan.FromMilliseconds(next.ComboTimeoutMs);
        _wander.Enabled = next.Wander && !next.PositionLocked;
        _effects.Enabled = next.EffectsEnabled;
        Combo.SetSize(next.ComboSize);
        Combo.SetOffset(next.ComboOffsetX, next.ComboOffsetY);
        Topmost = next.Topmost;
        SpriteImage.Source = _spriteSource.Current;
        ApplyLayout();
    }

    private void ReplaceSpriteSource()
    {
        ISpriteSource replacement = SpriteSourceFactory.Create(_settings);
        if (_spriteSource is not null)
        {
            _spriteSource.CurrentChanged -= OnSpriteChanged;
            _spriteSource.Dispose();
        }

        _spriteSource = replacement;
        _spriteSource.CurrentChanged += OnSpriteChanged;
    }

    private static Theme LoadTheme(string? path)
    {
        Theme? chosen = path is null ? null : Theme.Load(path);
        if (chosen is not null)
        {
            return chosen;
        }

        string? fallback = ThemeLibrary.DefaultPath;
        return (fallback is null ? null : Theme.Load(fallback)) ?? Theme.Empty();
    }

    /// <summary>
    /// 늘어나고 흔들려도 잘리지 않도록 창을 그림보다 넉넉하게 잡는다. 콤보를 밀어둔 만큼도 더 확보한다.
    /// 세로 비율은 현재 이미지 기준이고, 나머지 장은 Uniform 으로 그 안에 맞춰 들어간다.
    /// </summary>
    private void ApplyLayout()
    {
        ImageSource image = _spriteSource!.Current;
        double aspect = image.Width > 0 ? image.Height / image.Width : 1;
        double spriteWidth = Math.Max(48, _settings.WidgetWidth);
        double spriteHeight = spriteWidth * aspect;

        SpriteImage.Width = spriteWidth;
        SpriteImage.Height = spriteHeight;

        double width = spriteWidth + (Math.Max(MinPadding, spriteWidth * PaddingRatio) + Math.Abs(_settings.ComboOffsetX)) * 2;
        double height = spriteHeight + (Math.Max(MinPadding, spriteHeight * PaddingRatio) + Math.Abs(_settings.ComboOffsetY)) * 2;

        // 크기를 실시간으로 바꿀 때 가운데가 제자리에 있어야 커지고 작아지는 게 자연스럽다.
        if (_positioned)
        {
            Left -= (width - Width) / 2;
            Top -= (height - Height) / 2;
        }

        Width = width;
        Height = height;
    }

    private void RestorePosition()
    {
        if (_settings.WindowLeft is { } left && _settings.WindowTop is { } top)
        {
            Left = left;
            Top = top;
            return;
        }

        Left = SystemParameters.WorkArea.Right - Width - 40;
        Top = SystemParameters.WorkArea.Bottom - Height - 40;
    }

    private Point Normalize(Point point)
    {
        double width = DragRoot.ActualWidth > 0 ? DragRoot.ActualWidth : Width;
        double height = DragRoot.ActualHeight > 0 ? DragRoot.ActualHeight : Height;
        return new Point(point.X / width, point.Y / height);
    }

    private static bool SamePath(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }

    private void OnSpriteChanged(object? sender, EventArgs e) => SpriteImage.Source = _spriteSource!.Current;

    private void OnClosed(object? sender, EventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        if (_spriteSource is null)
        {
            return;
        }

        _spriteSource.CurrentChanged -= OnSpriteChanged;
        _spriteSource.Dispose();
    }
}
