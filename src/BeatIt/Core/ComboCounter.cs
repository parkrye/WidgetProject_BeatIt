namespace BeatIt.Core;

/// <summary>마지막 타격 이후 정해진 시간 안에 다시 때리면 콤보가 이어진다.</summary>
public sealed class ComboCounter(TimeSpan timeout)
{
    private DateTime _lastHitAt = DateTime.MinValue;

    public TimeSpan Timeout { get; set; } = timeout;

    public int Combo { get; private set; }

    /// <summary>한 대 때린 걸 기록하고 갱신된 콤보 수를 돌려준다.</summary>
    public int Register()
    {
        DateTime now = DateTime.UtcNow;
        Combo = now - _lastHitAt <= Timeout ? Combo + 1 : 1;
        _lastHitAt = now;
        return Combo;
    }

    /// <summary>시간이 지나 콤보가 끊겼는지 확인한다. 끊겼으면 true.</summary>
    public bool ExpireIfTimedOut()
    {
        if (Combo == 0 || DateTime.UtcNow - _lastHitAt <= Timeout)
        {
            return false;
        }

        Combo = 0;
        return true;
    }
}
