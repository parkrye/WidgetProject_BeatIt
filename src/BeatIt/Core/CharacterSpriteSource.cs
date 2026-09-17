using System.Windows.Media;

namespace BeatIt.Core;

/// <summary>
/// 상태와 방향에 맞는 그림을 골라주고, 그 행동에 붙은 소리를 울린다.
/// 가만히 있으면 idle 중 하나를 랜덤한 주기로 갈아 끼우고, 맞으면 beat, 끌려가거나 걸어가면 move 로 바뀐다.
/// </summary>
public sealed class CharacterSpriteSource : ISpriteSource
{
    private readonly Character _character;
    private readonly Random _random = new();

    private Sprite _current;
    private SpriteState _state = SpriteState.Idle;
    private IReadOnlyList<Sprite>? _moveCandidates;
    private double _idleRemaining;
    private double _beatRemaining;
    private double _idleMinSeconds;
    private double _idleMaxSeconds;
    private double _idleSoundRemaining;
    private double _idleSoundMinSeconds = 10;
    private double _idleSoundMaxSeconds = 30;
    private double _beatHoldSeconds = 0.55;

    public CharacterSpriteSource(Character character, double idleMinSeconds, double idleMaxSeconds)
    {
        _character = character;
        SetIdleInterval(idleMinSeconds, idleMaxSeconds);

        foreach (Sprite sprite in _character.All)
        {
            sprite.CurrentChanged += OnSpriteFrameChanged;
        }

        _current = _character.Idle[0];
        _current.Play();
        _idleRemaining = NextIdleDelay();
        _idleSoundRemaining = NextIdleSoundDelay();
    }

    public event EventHandler? CurrentChanged;

    public ImageSource Current => _current.Current;

    public void SetIdleInterval(double minSeconds, double maxSeconds)
    {
        _idleMinSeconds = Math.Max(0.2, minSeconds);
        _idleMaxSeconds = Math.Max(_idleMinSeconds, maxSeconds);
    }

    public void SetIdleSoundInterval(double minSeconds, double maxSeconds)
    {
        _idleSoundMinSeconds = Math.Max(1, minSeconds);
        _idleSoundMaxSeconds = Math.Max(_idleSoundMinSeconds, maxSeconds);
    }

    public void SetVolume(double volume, bool muted) => _character.Audio.SetVolume(volume, muted);

    public void SetBeatHold(double seconds) => _beatHoldSeconds = Math.Max(0.05, seconds);

    public void ReleaseAudio() => _character.ReleaseAudio();

    public void OnHit(Aim aim)
    {
        _beatRemaining = _beatHoldSeconds;

        // 맞았으면 대기 소리를 낼 때가 아니다. 다음 대기까지 미뤄둔다.
        _idleSoundRemaining = NextIdleSoundDelay();
        Switch(Pick(_character.Beat.For(aim)));
        _character.Audio.Beat.Play();
    }

    public void Update(double deltaSeconds, SpriteState state, Aim aim)
    {
        if (state == SpriteState.Moving)
        {
            EnterMoving(aim);
            return;
        }

        if (_state == SpriteState.Moving)
        {
            // 끌고 가다 놓았다. 맞은 상태로 돌아갈 이유는 없으니 바로 idle 로.
            _state = SpriteState.Idle;
            _moveCandidates = null;
            _beatRemaining = 0;
            SwitchToIdle();
            return;
        }

        if (_beatRemaining > 0)
        {
            _beatRemaining -= deltaSeconds;
            if (_beatRemaining <= 0)
            {
                SwitchToIdle();
            }

            return;
        }

        TickIdleSound(deltaSeconds);

        _idleRemaining -= deltaSeconds;
        if (_idleRemaining <= 0)
        {
            SwitchToIdle();
        }
    }

    public void Dispose()
    {
        foreach (Sprite sprite in _character.All)
        {
            sprite.CurrentChanged -= OnSpriteFrameChanged;
        }

        _character.Dispose();
    }

    /// <summary>
    /// 가는 쪽이 바뀌면 그쪽 그림으로 갈아 끼운다.
    /// 방향 폴더가 없어 어차피 같은 후보로 떨어지면 아무것도 안 한다. 걸을 때마다 그림이 새로 뽑히면 산만하다.
    /// </summary>
    private void EnterMoving(Aim aim)
    {
        IReadOnlyList<Sprite> next = _character.Move.For(aim);
        if (_state == SpriteState.Moving && ReferenceEquals(next, _moveCandidates))
        {
            return;
        }

        bool started = _state != SpriteState.Moving;
        _state = SpriteState.Moving;
        _moveCandidates = next;
        _beatRemaining = 0;
        _idleSoundRemaining = NextIdleSoundDelay();
        Switch(Pick(next));

        // 걷는 소리는 움직이기 시작할 때 한 번만. 방향이 꺾일 때마다 다시 울리면 시끄럽다.
        if (started)
        {
            _character.Audio.Move.Play();
        }
    }

    private void SwitchToIdle()
    {
        _idleRemaining = NextIdleDelay();
        Switch(Pick(_character.Idle));
    }

    private void TickIdleSound(double deltaSeconds)
    {
        if (_character.Audio.Idle.Count == 0)
        {
            return;
        }

        _idleSoundRemaining -= deltaSeconds;
        if (_idleSoundRemaining > 0)
        {
            return;
        }

        _idleSoundRemaining = NextIdleSoundDelay();
        _character.Audio.Idle.Play();
    }

    private double NextIdleDelay() =>
        _idleMinSeconds + (_random.NextDouble() * (_idleMaxSeconds - _idleMinSeconds));

    private double NextIdleSoundDelay() =>
        _idleSoundMinSeconds + (_random.NextDouble() * (_idleSoundMaxSeconds - _idleSoundMinSeconds));

    /// <summary>같은 그림이 연달아 나오면 바뀐 티가 안 나서, 장수가 넉넉하면 현재 그림은 피한다.</summary>
    private Sprite Pick(IReadOnlyList<Sprite> candidates)
    {
        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        int index = _random.Next(candidates.Count);
        if (!ReferenceEquals(candidates[index], _current))
        {
            return candidates[index];
        }

        return candidates[(index + 1 + _random.Next(candidates.Count - 1)) % candidates.Count];
    }

    private void Switch(Sprite sprite)
    {
        if (ReferenceEquals(sprite, _current))
        {
            sprite.Play();
            CurrentChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        _current.Pause();
        _current = sprite;
        _current.Play();
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSpriteFrameChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _current))
        {
            CurrentChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
