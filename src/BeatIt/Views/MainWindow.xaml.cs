using System.Diagnostics;
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
    /// <summary>이펙트가 캐릭터를 덮지 않도록 가로 길이의 절반 이하로 잡는다.</summary>
    private const double EffectSizeRatio = 0.45;

    /// <summary>이만큼 움직여야 이동 방향을 다시 따진다. 프레임마다 따지면 그림이 깜빡인다.</summary>
    private const double MoveDirectionDistance = 6;

    /// <summary>콤보 계단이 오를 때 캐릭터 둘레로 터뜨리는 이펙트 장수.</summary>
    private const int MilestoneBurstCount = 8;

    /// <summary>던질 속도를 낼 때 되돌아보는 시간. 짧으면 손 떨림이 섞이고 길면 방향이 뭉개진다.</summary>
    private const double SpeedWindowSeconds = 0.06;

    /// <summary>놓기 전에 이만큼 손이 멈춰 있었으면 던질 뜻이 없는 것으로 본다.</summary>
    private const double SettleSeconds = 0.12;

    /// <summary>
    /// 창을 실제로 옮기는 주기. 투명 창은 옮길 때마다 화면 합성이 통째로 다시 도는데,
    /// 걷는 속도에서는 이 정도만 옮겨도 눈에 똑같고 값은 절반이다.
    /// </summary>
    private const double MoveIntervalSeconds = 1.0 / 30;

    private readonly SettingsService _settingsService;
    private readonly HitAnimator _hitAnimator = new();
    private readonly DragStretchAnimator _dragAnimator = new();
    private readonly WanderController _wander = new();
    private readonly ThrowMotion _throw = new();

    /// <summary>끌고 가는 손의 속도를 재는 시계. 놓는 순간 그 속도가 던지는 속도가 된다.</summary>
    private readonly Stopwatch _dragClock = new();
    private readonly ComboCounter _comboCounter;
    private readonly ComboStyle _comboStyle = new();
    private readonly HitEffectPresenter _effects;

    private AppSettings _settings;
    private ISpriteSource? _spriteSource;
    private Theme? _theme;
    private TimeSpan _lastRenderTime;
    private FacingDirection _moveDirection = FacingDirection.Default;
    private Vector _motion;
    private Vector _pendingStep;
    private double _sinceMove;
    /// <summary>
    /// 지금 자리를 비워둔 콤보 계단. 이게 바뀔 때만 창을 다시 잡는다.
    /// -1 이면 콤보가 안 떠 있어서 아무것도 안 비워둔 상태다. 콤보를 안 쌓는 대부분의 시간
    /// 동안 큰 창을 들고 있으면, 걸어다닐 때 옮기고 그리는 값이 그만큼 비싸진다.
    /// </summary>
    private int _comboTier = -1;

    private Vector _dragVelocity;
    private double _lastDragSeconds;
    private Point _grabPoint;
    private bool _pressed;
    private bool _dragging;
    private bool _positioned;
    private bool _dialogOpen;

    /// <summary>
    /// 커서에 닿은 걸 한 번 처리해도 되는 상태. 닿아 있는 내내 처리하면 커서를 얹어두는
    /// 것만으로 콤보가 저 혼자 쌓인다. 한 번 커서를 벗어나야 다시 켜진다.
    /// 뿌린 손은 놓은 자리에 그대로 있어서 날기 시작할 때는 늘 커서 밑이기도 하다.
    /// </summary>
    private bool _catchArmed;

    /// <summary>이번 프레임에 커서가 닿아 있는 캐릭터 안 좌표. 안 닿았으면 null.</summary>
    private Point? _cursorHit;

    public MainWindow(SettingsService settingsService, AppSettings settings)
    {
        InitializeComponent();

        _settingsService = settingsService;
        _settings = settings;
        _comboCounter = new ComboCounter(TimeSpan.FromMilliseconds(settings.ComboTimeoutMs));
        _effects = new HitEffectPresenter(EffectLayer);
        Combo.SetStyle(_comboStyle);

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

    /// <summary>캐릭터 관리 창을 열기 전에 소리 파일을 놓아준다. 안 그러면 잠겨서 못 뺀다.</summary>
    public void ReleaseAudio() => _spriteSource?.ReleaseAudio();

    /// <summary>테마 관리 창을 열기 전에 글꼴을 놓아준다. 닫고 나면 통째로 다시 읽는다.</summary>
    public void ReleaseTheme()
    {
        _theme = Theme.Empty();
        Combo.SetTheme(_theme);
        _effects.SetTheme(_theme);
    }

    /// <summary>관리 창에서 파일을 고쳤다. 경로가 그대로여도 통째로 다시 읽는다.</summary>
    public void ReloadAssets() => ApplySettings(_settings, force: true);

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

        Fly(delta);

        // 날아간 자리에서 커서와 닿았는지 먼저 본다. 닿았으면 걷지도 않아야 한다.
        TouchByCursor();
        Walk(delta);
        _hitAnimator.Update(delta);
        _dragAnimator.Update(delta);
        SpriteState state = _dragging || _wander.IsMoving || _throw.IsFlying ? SpriteState.Moving : SpriteState.Idle;
        _spriteSource!.Update(delta, state, _moveDirection);
        DragRoot.RenderTransformOrigin = _dragAnimator.Anchor;

        if (_comboCounter.ExpireIfTimedOut())
        {
            Combo.Hide();
            ReserveForCombo(0);
        }
    }

    /// <summary>혼자 돌아다니는 몫만큼 창을 옮긴다. 걸을 때도 몸이 살짝 늘어난다.</summary>
    private void Walk(double delta)
    {
        if (!_wander.Enabled)
        {
            _pendingStep = default;
            _sinceMove = 0;
            return;
        }

        if (Busy)
        {
            _wander.Suspend(_settings.DragRestMs / 1000.0);
            _pendingStep = default;
            _sinceMove = 0;
            return;
        }

        // 옮기는 건 미뤄도 목적지 계산은 매 프레임 한다. 아직 안 옮긴 몫을 태워서 물어봐야
        // 같은 자리를 두 번 걷지 않는다.
        Point intended = new(Left + _pendingStep.X, Top + _pendingStep.Y);
        Rect area = WanderArea.Resolve(_settings.WanderArea, CustomArea(), this, Center + _pendingStep);
        _pendingStep += _wander.Update(delta, intended, WanderArea.Travel(area, new Size(Width, Height)));

        // 쉬는 동안에도 시간을 쌓으면 빚이 남아서, 다시 걷기 시작할 때 한동안 매 프레임 옮긴다.
        // 옮길 몫이 없으면 시계도 같이 멈춘다.
        if (_pendingStep == default)
        {
            _sinceMove = 0;
            return;
        }

        _sinceMove += delta;
        if (_sinceMove < MoveIntervalSeconds)
        {
            return;
        }

        _sinceMove = 0;
        Vector step = _pendingStep;
        _pendingStep = default;

        Left += step.X;
        Top += step.Y;
        TrackMotion(step);
        _dragAnimator.Grab(new Point(0.5, 0.35));
        _dragAnimator.Pull(step * 0.4);
    }

    /// <summary>
    /// 던져진 몫만큼 창을 옮긴다. 걷는 것과 달리 프레임을 건너뛰지 않는다.
    /// 날아가는 건 순식간이라 30fps 로 줄이면 뚝뚝 끊겨 보이고, 어차피 곧 멈춘다.
    /// </summary>
    private void Fly(double delta)
    {
        if (!_throw.IsFlying)
        {
            return;
        }

        Rect area = WanderArea.Resolve(_settings.WanderArea, CustomArea(), this, Center);
        Vector step = _throw.Update(delta, new Point(Left, Top), WanderArea.Travel(area, new Size(Width, Height)), out Bump bump);

        Left += step.X;
        Top += step.Y;
        TrackMotion(step);
        _dragAnimator.Grab(new Point(0.5, 0.5));
        _dragAnimator.Pull(step * 0.25);

        if (bump.Happened)
        {
            Strike(bump);
        }

        if (!_throw.IsFlying)
        {
            // 멈춘 자리가 다음에 켤 때의 자리다. 나는 동안 매 프레임 적을 일은 아니다.
            _ = SavePositionAsync();
        }
    }

    /// <summary>
    /// 커서에 닿았는지 보고, 닿았으면 설정대로 처리한다. 매 프레임 한 번 묻고 그 답을
    /// <see cref="_cursorHit"/> 에 남겨서, 걸어도 되는지 따지는 쪽도 같은 답을 본다.
    ///
    /// 닿았는지는 <see cref="CursorProbe"/> 에 묻는다. 커서는 가만히 있고 창만 움직이는
    /// 동안에는 <c>IsMouseOver</c> 가 갱신되지 않아서, 그걸 믿으면 벗어난 줄도 닿은 줄도 모른다.
    ///
    /// 서는 쪽(기본)은 날아가던 것만 세운다. 걷다가 커서를 만나면 멈춰 서는 것과 같은
    /// 이유로, 커서 밑을 지나가 버리면 조준한 클릭이 허공을 때린다.
    /// 커서를 벽으로 쓰는 쪽은 날아오면 튕겨내고, 날지 않을 때 닿으면 한 대 때린 것으로 친다.
    /// 커서가 벽이면 스쳐도 맞는 게 앞뒤가 맞는다.
    /// </summary>
    private void TouchByCursor()
    {
        _cursorHit = CursorProbe.HitPoint(SpriteImage);
        if (_cursorHit is not { } where)
        {
            _catchArmed = true;
            return;
        }

        // 잡고 있는 동안은 손이 위에 있는 게 당연하다. 끌려오다 몸통이 뒤처져 커서를
        // 벗어났다 다시 들어오는 것까지 때린 걸로 치면, 끌기만 해도 콤보가 쌓인다.
        // 놓고 나서도 한 번 비켰다 와야 다음 대가 들어간다.
        if (_pressed || _dialogOpen)
        {
            _catchArmed = false;
            return;
        }

        if (!_catchArmed)
        {
            return;
        }

        if (!_settings.ThrowBounceOffCursor)
        {
            _throw.Stop();
            return;
        }

        if (_throw.IsFlying)
        {
            BounceOffCursor(where);
            return;
        }

        HitByCursor(where);
    }

    /// <summary>
    /// 날지 않고 있는데 커서가 닿았다. 클릭과 똑같이 한 대 먹인다. 닿은 자리를 그대로 넘겨서
    /// 그쪽 <c>beat</c> 그림이 뜨고 이펙트도 거기서 튄다.
    /// </summary>
    private void HitByCursor(Point where)
    {
        _catchArmed = false;
        Hit(SpriteImage.TranslatePoint(where, this), Facing.FromHit(where, SpriteImage.RenderSize));
    }

    /// <summary>
    /// 커서를 벽처럼 쳐서 튕겨낸다. <paramref name="where"/> 는 커서가 닿은 캐릭터 안 좌표다.
    /// 때린 자리는 클릭과 똑같이 따지므로 그쪽 <c>beat</c> 그림이 뜨고 이펙트도 닿은 자리에서 튄다.
    /// 튕겨낸 뒤에는 다시 잠가둔다. 커서에서 멀어지는 중이니 곧 벗어나는데, 그전까지 매 프레임
    /// 튕기려 들면 커서에 들러붙은 채로 콤보만 쌓인다.
    /// </summary>
    private void BounceOffCursor(Point where)
    {
        Size size = SpriteImage.RenderSize;
        Vector away = new Point(size.Width / 2, size.Height / 2) - where;

        Bump bump = _throw.BounceOff(away, Facing.FromHit(where, size));
        if (!bump.Happened)
        {
            return;
        }

        _catchArmed = false;
        Strike(bump, SpriteImage.TranslatePoint(where, this));
    }

    /// <summary>
    /// 벽에 박았다. 맞은 것과 똑같이 치므로 <b>콤보도 오른다.</b>
    /// 꾸겨지는 세기만 콤보가 아니라 박은 세기에서 온다. 살살 굴러가 닿은 것과
    /// 던져 박은 것이 같이 꾸겨지면 던진 맛이 안 산다.
    /// </summary>
    private void Strike(Bump bump) => Strike(bump, EdgeToward(bump.Side));

    /// <summary>박은 자리를 아는 경우. <paramref name="where"/> 는 창 안 좌표다.</summary>
    private void Strike(Bump bump, Point where)
    {
        int combo = _comboCounter.Register();
        _spriteSource!.OnHit(bump.Side);
        _hitAnimator.Bump(bump.Strength);
        _effects.Spawn(where, SpriteImage.Width * EffectSizeRatio, combo);

        // 자리를 먼저 넓히고 띄운다. 거꾸로 하면 계단이 오른 첫 프레임에 숫자가 머리를 파고든다.
        ReserveForCombo(combo);
        Combo.Show(combo);

        if (_comboStyle.IsMilestone(combo))
        {
            Celebrate();
        }
    }

    /// <summary>벽에 닿은 쪽의 캐릭터 가장자리. 이펙트가 부딪힌 자리에서 튀어야 한다.</summary>
    private Point EdgeToward(FacingDirection side)
    {
        Point center = new(Width / 2, Height / 2);
        double halfWidth = SpriteImage.Width / 2;
        double halfHeight = SpriteImage.Height / 2;

        return side switch
        {
            FacingDirection.Left => new Point(center.X - halfWidth, center.Y),
            FacingDirection.Right => new Point(center.X + halfWidth, center.Y),
            FacingDirection.Up => new Point(center.X, center.Y - halfHeight),
            FacingDirection.Down => new Point(center.X, center.Y + halfHeight),
            _ => center,
        };
    }

    /// <summary>
    /// 지금 걸으면 안 되는 상황. 잡고 있거나, 설정 창이 떠 있거나, 커서가 올라와 있을 때다.
    /// 커서 밑에서 걸어 나가면 조준한 클릭이 허공을 때린다.
    /// 커서가 올라와 있는지는 <see cref="TouchByCursor"/> 가 이번 프레임에 물어둔 답을 쓴다.
    /// </summary>
    private bool Busy =>
        _pressed
        || _dragging
        || _throw.IsFlying
        || _dialogOpen
        || _cursorHit is not null
        || SpriteImage.ContextMenu?.IsOpen == true;

    /// <summary>
    /// 움직인 거리를 모았다가 일정 거리를 넘으면 그때 방향을 정한다.
    /// 한 프레임 이동량은 1px도 안 될 만큼 작아서 그대로 쓰면 방향이 계속 뒤집힌다.
    /// </summary>
    private void TrackMotion(Vector step)
    {
        _motion += step;
        if (_motion.Length < MoveDirectionDistance)
        {
            return;
        }

        _moveDirection = Facing.FromMotion(_motion, _moveDirection);
        _motion = default;
    }

    /// <summary>창 한가운데. 어느 모니터에 올라가 있는지 물어볼 때 쓴다.</summary>
    private Point Center => new(Left + (Width / 2), Top + (Height / 2));

    /// <summary>직접 그려둔 영역. 한 번도 안 그렸으면 빈 사각형이라 화면 전체로 떨어진다.</summary>
    private Rect CustomArea() =>
        _settings.CustomWanderArea is { } area
            ? new Rect(area.Left, area.Top, area.Width, area.Height)
            : Rect.Empty;

    private void OnSpriteMouseDown(object sender, MouseButtonEventArgs e)
    {
        // 날아가는 걸 공중에서 낚아챌 수 있어야 한다. 잡았는데 계속 날면 손에서 빠져나간다.
        _throw.Stop();

        _pressed = true;
        _dragging = false;
        _motion = default;
        _dragVelocity = default;
        _lastDragSeconds = 0;
        _dragClock.Restart();
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
        TrackMotion(delta);
        TrackSpeed(delta);
        _dragAnimator.Pull(delta);
    }

    /// <summary>
    /// 끌고 가는 손의 속도를 모은다. 한 프레임 값을 그대로 쓰면 마지막 한 번이 유난히 짧거나
    /// 길게 잡혔을 때 엉뚱한 속도로 날아가서, 최근 것에 무게를 싣는 지수 평균으로 다듬는다.
    /// </summary>
    private void TrackSpeed(Vector delta)
    {
        double now = _dragClock.Elapsed.TotalSeconds;
        double elapsed = now - _lastDragSeconds;
        _lastDragSeconds = now;

        if (elapsed < 0.0005)
        {
            return;
        }

        double weight = Math.Clamp(elapsed / SpeedWindowSeconds, 0, 1);
        _dragVelocity = (_dragVelocity * (1 - weight)) + (delta / elapsed * weight);
    }

    /// <summary>
    /// 놓을 때 쓸 속도. 놓기 직전에 손을 멈췄으면 던질 뜻이 없는 것이라 0 으로 본다.
    /// 이게 없으면 끌어다 조심히 내려놓아도 직전에 모아둔 속도로 날아가 버린다.
    /// </summary>
    private Vector ReleaseVelocity() =>
        _dragClock.Elapsed.TotalSeconds - _lastDragSeconds > SettleSeconds ? default : _dragVelocity;

    private async void OnSpriteMouseUp(object sender, MouseButtonEventArgs e)
    {
        bool pressed = _pressed;
        bool dragged = ReleaseGrab();

        if (!pressed)
        {
            return;
        }

        if (!dragged)
        {
            Hit(e.GetPosition(this), Facing.FromHit(e.GetPosition(SpriteImage), SpriteImage.RenderSize));
            return;
        }

        Fling();
        if (_throw.IsFlying)
        {
            // 아직 자리를 안 잡았다. 멈춘 자리를 저장해야 다음에 켤 때 거기 뜬다.
            return;
        }

        await SavePositionAsync();
    }

    /// <summary>
    /// 캡처를 우클릭이나 다른 창에 뺏기면 여기로 온다. 누른 상태를 그대로 두면
    /// 위젯이 커서를 따라다니거나, 눌린 줄 알고 영영 안 걷는다.
    /// </summary>
    private async void OnSpriteLostCapture(object sender, MouseEventArgs e)
    {
        // 끌던 중에 뺏겼으면 놓는 이벤트가 안 온다. 옮겨둔 자리는 여기서 저장해야 남는다.
        if (!ReleaseGrab())
        {
            return;
        }

        Fling();
        if (_throw.IsFlying)
        {
            return;
        }

        await SavePositionAsync();
    }

    /// <summary>놓은 속도로 던진다. 커서 밑에서 출발하므로 잡히는 건 커서를 벗어난 뒤부터다.</summary>
    private void Fling()
    {
        _catchArmed = false;
        _throw.Launch(ReleaseVelocity());
    }

    /// <summary>
    /// 붙잡은 상태를 되돌리고 캡처를 놓는다. 끌던 중이었으면 true.
    /// 캡처를 푸는 것 자체가 이 함수를 다시 부르지만, 그때는 이미 지워져 있어 false 를 돌려준다.
    /// </summary>
    private bool ReleaseGrab()
    {
        bool dragged = _dragging;

        _pressed = false;
        _dragging = false;
        _motion = default;

        if (SpriteImage.IsMouseCaptured)
        {
            SpriteImage.ReleaseMouseCapture();
        }

        return dragged;
    }

    private Task SavePositionAsync()
    {
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        return _settingsService.SaveAsync(_settings);
    }

    private void Hit(Point where, FacingDirection direction)
    {
        int combo = _comboCounter.Register();
        _spriteSource!.OnHit(direction);
        _hitAnimator.Hit(combo);
        _effects.Spawn(where, SpriteImage.Width * EffectSizeRatio, combo);
        _wander.Suspend(_settings.HitRestMs / 1000.0);

        // 자리를 먼저 넓히고 띄운다. 거꾸로 하면 계단이 오른 첫 프레임에 숫자가 머리를 파고든다.
        ReserveForCombo(combo);
        Combo.Show(combo);

        if (_comboStyle.IsMilestone(combo))
        {
            Celebrate();
        }
    }

    /// <summary>
    /// 콤보 계단이 올랐다. 캐릭터 둘레로 이펙트를 한 바퀴 터뜨린다.
    /// 숫자가 커지고 색이 바뀌는 건 콤보 표시가 알아서 하고, 여기서는 둘레만 맡는다.
    /// </summary>
    private void Celebrate() =>
        _effects.Burst(
            new Point(Width / 2, Height / 2),
            SpriteImage.Width * EffectSizeRatio,
            MilestoneBurstCount,
            SpriteImage.Width * 0.55);

    /// <summary>
    /// 콤보가 자란 만큼 머리 위 자리를 넓힌다. 계단이 바뀔 때만 창을 다시 잡는다.
    /// 최대 배율에 맞춰 늘 크게 잡아두면, 콤보를 안 쌓는 대부분의 시간 동안 화면 절반만 한
    /// 투명 창이 떠 있게 된다. 투명 창은 클 수록 옮기고 그리는 값이 비싸다.
    /// </summary>
    private void ReserveForCombo(int combo)
    {
        // 콤보 1 은 안 띄운다. 안 띄우는 걸 자리까지 잡아두면 한 대 때릴 때마다 창이 커진다.
        int tier = combo < 2 ? -1 : _comboStyle.TierOf(combo);
        if (tier == _comboTier)
        {
            return;
        }

        _comboTier = tier;
        ApplyLayout();
    }

    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        ReleaseGrab();
        WanderMenuItem.IsChecked = _settings.Wander;
        TopmostMenuItem.IsChecked = _settings.Topmost;
        LockMenuItem.IsChecked = _settings.PositionLocked;
    }

    private async void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        AppSettings original = _settings.Clone();
        SettingsWindow dialog = new(_settings.Clone(), this) { Owner = this };

        // 설정 창이 떠 있는 동안은 걷지 않는다. 투명 창이 그 위를 매 프레임 지나가면
        // 설정 창이 통째로 다시 그려져서 슬라이더까지 밀린다.
        _dialogOpen = true;
        bool confirmed;
        try
        {
            confirmed = dialog.ShowDialog() == true;
        }
        finally
        {
            _dialogOpen = false;
        }

        if (!confirmed)
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

    /// <summary>
    /// 바뀐 것만 다시 만든다. 슬라이더를 끄는 동안 이미지를 매번 다시 읽으면 버벅인다.
    /// <paramref name="force"/> 면 경로가 그대로여도 다시 읽는다. 폴더 안이 달라졌을 때 쓴다.
    /// </summary>
    private void ApplySettings(AppSettings next, bool force = false)
    {
        bool rebuildCharacter = force || _spriteSource is null || !SamePath(next.CharacterPath, _settings.CharacterPath);
        bool rebuildTheme = force || _theme is null || !SamePath(next.ThemePath, _settings.ThemePath);
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
        _spriteSource.SetIdleSoundInterval(next.IdleSoundMinMs / 1000.0, next.IdleSoundMaxMs / 1000.0);
        _spriteSource.SetVolume(next.SoundVolume, next.SoundMuted);
        _spriteSource.SetBeatHold(next.BeatHoldMs / 1000.0);
        _comboCounter.Timeout = TimeSpan.FromMilliseconds(next.ComboTimeoutMs);
        _wander.Enabled = next.Wander && !next.PositionLocked;
        _wander.SetPace(
            next.WanderSpeedMin,
            next.WanderSpeedMax,
            next.WanderRestMinMs / 1000.0,
            next.WanderRestMaxMs / 1000.0);
        _hitAnimator.Power = next.HitPower;
        _hitAnimator.Tilt = next.HitTilt;
        _hitAnimator.ComboGain = next.HitComboGain;
        _throw.Enabled = next.ThrowEnabled;
        _throw.SpeedScale = next.ThrowSpeedScale;
        _throw.MaxSpeed = next.ThrowMaxSpeed;
        _throw.Bounce = next.ThrowBounce;
        _throw.Friction = next.ThrowFriction;
        _throw.StopSpeed = next.ThrowStopSpeed;
        if (!next.ThrowEnabled)
        {
            _throw.Stop();
        }

        _dragAnimator.Lag = next.DragLag;
        _dragAnimator.MaxStretch = next.DragStretch;
        _dragAnimator.SetStiffness(next.DragSpring);
        _effects.Enabled = next.EffectsEnabled;
        _comboStyle.BaseSize = next.ComboSize;
        _comboStyle.Milestone = next.ComboMilestone;
        _comboStyle.Growth = next.ComboGrowth;
        _comboStyle.MaxScale = next.ComboMaxScale;
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

        // 콤보는 캐릭터 머리 위에 밑변을 붙이고 위로 자란다. 그만큼을 위 여백으로 확보해야
        // 숫자가 안 잘리고, 확보한 여백이 곧 콤보가 앉을 자리가 된다.
        double comboHeight = _comboTier < 0 ? 0 : _comboStyle.ReserveFor(_comboTier);
        double padding = Math.Max(
            Math.Max(MinPadding, spriteHeight * PaddingRatio),
            comboHeight + _settings.ComboGap);

        double width = spriteWidth + ((Math.Max(MinPadding, spriteWidth * PaddingRatio) + Math.Abs(_settings.ComboOffsetX)) * 2);
        double height = spriteHeight + ((padding + Math.Abs(_settings.ComboOffsetY)) * 2);

        // 위 여백에서 틈만큼을 뺀 자리가 콤보의 바닥이다. 창이 세로로 대칭이라 위 여백은 (창 - 그림) / 2 다.
        Combo.Height = Math.Max(0, ((height - spriteHeight) / 2) - _settings.ComboGap);

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
