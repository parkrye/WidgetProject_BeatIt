using System.Windows.Media;

namespace BeatIt.Core;

/// <summary>
/// 상태에 맞는 그림을 골라준다.
/// 가만히 있으면 idle 중 하나를 랜덤한 주기로 갈아 끼우고, 맞으면 beat, 끌려가면 move 로 바뀐다.
/// </summary>
public sealed class CharacterSpriteSource : ISpriteSource
{
    private const double BeatHoldSeconds = 0.55;

    private readonly Character _character;
    private readonly Random _random = new();

    private Sprite _current;
    private SpriteState _state = SpriteState.Idle;
    private double _idleRemaining;
    private double _beatRemaining;
    private double _idleMinSeconds;
    private double _idleMaxSeconds;

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
    }

    public event EventHandler? CurrentChanged;

    public ImageSource Current => _current.Current;

    public void SetIdleInterval(double minSeconds, double maxSeconds)
    {
        _idleMinSeconds = Math.Max(0.2, minSeconds);
        _idleMaxSeconds = Math.Max(_idleMinSeconds, maxSeconds);
    }

    public void OnHit()
    {
        _beatRemaining = BeatHoldSeconds;
        Switch(Pick(_character.Beat));
    }

    public void Update(double deltaSeconds, SpriteState state)
    {
        if (state == SpriteState.Moving)
        {
            EnterMoving();
            return;
        }

        if (_state == SpriteState.Moving)
        {
            // 끌고 가다 놓았다. 맞은 상태로 돌아갈 이유는 없으니 바로 idle 로.
            _state = SpriteState.Idle;
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

    private void EnterMoving()
    {
        if (_state == SpriteState.Moving)
        {
            return;
        }

        _state = SpriteState.Moving;
        _beatRemaining = 0;
        Switch(Pick(_character.Move));
    }

    private void SwitchToIdle()
    {
        _idleRemaining = NextIdleDelay();
        Switch(Pick(_character.Idle));
    }

    private double NextIdleDelay() =>
        _idleMinSeconds + _random.NextDouble() * (_idleMaxSeconds - _idleMinSeconds);

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
