using System.IO;
using System.Windows.Media;

namespace BeatIt.Core;

/// <summary>
/// 한 가지 행동에 붙은 소리들. 울릴 때마다 그중 하나를 무작위로 고른다.
/// 파일마다 <see cref="MediaPlayer"/> 를 미리 열어두고 재생할 때 되감기만 한다. 매번 열면 첫 소리가 늦게 난다.
/// </summary>
public sealed class SoundBank : IDisposable
{
    private readonly List<MediaPlayer> _players = [];
    private readonly Random _random = new();

    private double _volume = 1;
    private bool _muted;

    private SoundBank(IReadOnlyList<string> paths)
    {
        foreach (string path in paths)
        {
            Open(path);
        }
    }

    /// <summary>소리가 하나도 없는 묶음. 재생해도 아무 일도 안 난다.</summary>
    public static SoundBank Empty { get; } = new([]);

    public int Count => _players.Count;

    public static SoundBank Load(IReadOnlyList<string> paths) => paths.Count == 0 ? Empty : new SoundBank(paths);

    public void SetVolume(double volume, bool muted)
    {
        _volume = Math.Clamp(volume, 0, 1);
        _muted = muted;

        foreach (MediaPlayer player in _players)
        {
            player.Volume = _volume;
            player.IsMuted = _muted;
        }
    }

    /// <summary>하나 골라 처음부터 튼다. 같은 게 다시 걸리면 되감아서 다시 낸다.</summary>
    public void Play()
    {
        if (_muted || _volume <= 0 || _players.Count == 0)
        {
            return;
        }

        MediaPlayer player = _players[_random.Next(_players.Count)];
        player.Position = TimeSpan.Zero;
        player.Play();
    }

    public void Dispose()
    {
        foreach (MediaPlayer player in _players)
        {
            player.MediaFailed -= OnMediaFailed;
            player.Close();
        }

        _players.Clear();
    }

    private void Open(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        MediaPlayer player = new();
        player.MediaFailed += OnMediaFailed;

        try
        {
            player.Open(new Uri(path, UriKind.Absolute));
        }
        catch (UriFormatException)
        {
            player.MediaFailed -= OnMediaFailed;
            player.Close();
            return;
        }

        player.Volume = _volume;
        player.IsMuted = _muted;
        _players.Add(player);
    }

    /// <summary>
    /// 코덱이 없는 형식(ogg 처럼)은 여기로 떨어진다. 목록에서 빼야 무작위로 골랐을 때 조용히 지나가지 않는다.
    /// 여는 건 비동기라 생성자에서는 알 수 없고, 이 신호가 와야 알 수 있다.
    /// </summary>
    private void OnMediaFailed(object? sender, ExceptionEventArgs e)
    {
        if (sender is not MediaPlayer player)
        {
            return;
        }

        player.MediaFailed -= OnMediaFailed;
        _players.Remove(player);
        player.Close();
    }
}

/// <summary>캐릭터가 들고 있는 행동별 소리 묶음.</summary>
public sealed class CharacterAudio : IDisposable
{
    public CharacterAudio(SoundBank idle, SoundBank move, SoundBank beat)
    {
        Idle = idle;
        Move = move;
        Beat = beat;
    }

    /// <summary>소리가 하나도 없는 캐릭터용.</summary>
    public static CharacterAudio Silent { get; } = new(SoundBank.Empty, SoundBank.Empty, SoundBank.Empty);

    public SoundBank Idle { get; }

    public SoundBank Move { get; }

    public SoundBank Beat { get; }

    public int Count => Idle.Count + Move.Count + Beat.Count;

    public void SetVolume(double volume, bool muted)
    {
        Idle.SetVolume(volume, muted);
        Move.SetVolume(volume, muted);
        Beat.SetVolume(volume, muted);
    }

    public void Dispose()
    {
        Idle.Dispose();
        Move.Dispose();
        Beat.Dispose();
    }
}
