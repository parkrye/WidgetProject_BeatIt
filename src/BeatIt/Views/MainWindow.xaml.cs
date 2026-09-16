using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BeatIt.Core;
using BeatIt.Models;
using BeatIt.Services;

namespace BeatIt.Views;

/// <summary>화면 위에 떠 있는 위젯 본체. 클릭은 타격, 끌면 늘어나며 따라온다.</summary>
public partial class MainWindow : Window
{
    private const double DragThresholdPixels = 5;
    private const double PaddingRatio = 0.45;
    private const double MinPadding = 70;

    private readonly SettingsService _settingsService;
    private readonly HitAnimator _hitAnimator = new();
    private readonly DragStretchAnimator _dragAnimator = new();
    private readonly ComboCounter _comboCounter;

    private AppSettings _settings;
    private ISpriteSource _spriteSource;
    private TimeSpan _lastRenderTime;
    private Point _grabPoint;
    private bool _pressed;
    private bool _dragging;

    public MainWindow(SettingsService settingsService, AppSettings settings)
    {
        InitializeComponent();

        _settingsService = settingsService;
        _settings = settings;
        _comboCounter = new ComboCounter(TimeSpan.FromMilliseconds(settings.ComboTimeoutMs));
        _spriteSource = SpriteSourceFactory.Create(settings);
        _spriteSource.CurrentChanged += OnSpriteChanged;

        SpriteImage.RenderTransform = _hitAnimator.Transform;
        DragRoot.RenderTransform = _dragAnimator.Transform;
        WindowStartupLocation = WindowStartupLocation.Manual;

        ApplySettings();
        RestorePosition();

        CompositionTarget.Rendering += OnRendering;
        Closed += OnClosed;
    }

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

        _hitAnimator.Update(delta);
        _dragAnimator.Update(delta);
        _spriteSource.Update(delta, _dragging ? SpriteState.Moving : SpriteState.Idle);
        DragRoot.RenderTransformOrigin = _dragAnimator.Anchor;

        if (_comboCounter.ExpireIfTimedOut())
        {
            Combo.Hide();
        }
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
            Hit();
            return;
        }

        _dragging = false;
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        await _settingsService.SaveAsync(_settings);
    }

    private void Hit()
    {
        int combo = _comboCounter.Register();
        _spriteSource.OnHit();
        _hitAnimator.Hit(combo);
        Combo.Show(combo);
    }

    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        _pressed = false;
        TopmostMenuItem.IsChecked = _settings.Topmost;
        LockMenuItem.IsChecked = _settings.PositionLocked;
    }

    private async void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        SettingsWindow dialog = new(_settings.Clone()) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _settings = dialog.Result;
        ApplySettings();
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
        await _settingsService.SaveAsync(_settings);
    }

    private void OnExit(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void ApplySettings()
    {
        ISpriteSource replacement = SpriteSourceFactory.Create(_settings);
        _spriteSource.CurrentChanged -= OnSpriteChanged;
        _spriteSource.Dispose();
        _spriteSource = replacement;
        _spriteSource.CurrentChanged += OnSpriteChanged;

        Topmost = _settings.Topmost;
        _comboCounter.Timeout = TimeSpan.FromMilliseconds(_settings.ComboTimeoutMs);
        SpriteImage.Source = _spriteSource.Current;
        ApplyLayout();
    }

    /// <summary>
    /// 늘어나고 흔들려도 잘리지 않도록 창을 그림보다 넉넉하게 잡는다.
    /// 세로 비율은 현재 이미지 기준이고, 나머지 장은 Uniform 으로 그 안에 맞춰 들어간다.
    /// </summary>
    private void ApplyLayout()
    {
        ImageSource image = _spriteSource.Current;
        double aspect = image.Width > 0 ? image.Height / image.Width : 1;
        double spriteWidth = Math.Max(48, _settings.WidgetWidth);
        double spriteHeight = spriteWidth * aspect;

        SpriteImage.Width = spriteWidth;
        SpriteImage.Height = spriteHeight;
        Width = spriteWidth + Math.Max(MinPadding, spriteWidth * PaddingRatio) * 2;
        Height = spriteHeight + Math.Max(MinPadding, spriteHeight * PaddingRatio) * 2;
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

    private void OnSpriteChanged(object? sender, EventArgs e) => SpriteImage.Source = _spriteSource.Current;

    private void OnClosed(object? sender, EventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        _spriteSource.CurrentChanged -= OnSpriteChanged;
        _spriteSource.Dispose();
    }
}
